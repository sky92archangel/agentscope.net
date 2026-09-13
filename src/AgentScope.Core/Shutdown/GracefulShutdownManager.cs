// Copyright 2024-2026 the original author or authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgentScope.Core.Shutdown;

/// <summary>
/// 优雅关闭管理器，追踪所有活跃 Agent 请求，支持安全中止
/// 对应 Java: io.agentscope.core.shutdown.GracefulShutdownManager
/// </summary>
public class GracefulShutdownManager : IDisposable
{
    // ── 单例 ──────────────────────────────────────────────────────────
    private static readonly Lazy<GracefulShutdownManager> _instance = new(() => new());
    public static GracefulShutdownManager Instance => _instance.Value;

    // ── 字段 ──────────────────────────────────────────────────────────
    private readonly ConcurrentDictionary<string, ShutdownRequest> _activeRequests = new();
    private readonly ConcurrentDictionary<string, ActiveRequestContext> _activeContexts = new();
    private readonly CancellationTokenSource _globalCts = new();
    private readonly object _timeoutLock = new();
    private volatile ShutdownState _state = ShutdownState.Running;
    private volatile bool _disposed;
    private Thread? _timeoutMonitorThread;
    private TimeSpan _requestTimeout = TimeSpan.FromSeconds(300); // 默认 5 分钟超时

    // 中断标记字典：agentName -> (sessionId -> bool)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _shutdownInterrupted = new();

    // ── 属性 ──────────────────────────────────────────────────────────
    public ShutdownState State => _state;
    public CancellationToken Token => _globalCts.Token;
    public TimeSpan RequestTimeout
    {
        get => _requestTimeout;
        set => _requestTimeout = value;
    }

    /// <summary>当前活跃请求数量（用于外部监控 / 等待完成）。</summary>
    public int ActiveRequestCount => _activeContexts.Count;

    /// <summary>所有活跃的 ActiveRequestContext 快照。</summary>
    public IReadOnlyCollection<ActiveRequestContext> ActiveContexts =>
        _activeContexts.Values.ToList().AsReadOnly();

    // ── 构造 ──────────────────────────────────────────────────────────
    public GracefulShutdownManager()
    {
        StartTimeoutMonitor();
    }

    // ── ActiveRequestContext 注册/注销 ────────────────────────────────

    /// <summary>
    /// 注册一个活跃请求上下文，并启动该请求的超时追踪。
    /// 对应 Java: registerActiveRequestContext(ActiveRequestContext context)
    /// </summary>
    public void RegisterRequest(ActiveRequestContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (_state != ShutdownState.Running)
        {
            context.CancellationTokenSource?.Cancel();
            throw new AgentShuttingDownException("Agent 正在关闭，拒绝新请求");
        }

        _activeContexts[context.RequestId] = context;
        // 兼容旧版 RegisterRequest(object) 的 ShutdownRequest 映射
        _activeRequests[context.RequestId] = new ShutdownRequest
        {
            RequestId = context.RequestId,
            AgentName = context.AgentName,
            RegisteredAt = context.StartTime
        };
    }

    /// <summary>
    /// 注销活跃请求上下文，停止超时追踪。
    /// 对应 Java: unregisterActiveRequestContext(String requestId)
    /// </summary>
    public void UnregisterRequest(string requestId)
    {
        if (string.IsNullOrEmpty(requestId)) return;
        _activeContexts.TryRemove(requestId, out _);
        _activeRequests.TryRemove(requestId, out _);
    }

    // ── 兼容旧版 RegisterRequest(object) ─────────────────────────────

    /// <summary>注册一个活跃请求，返回 requestId（旧版兼容）。</summary>
    public string RegisterRequest(object agent)
    {
        var requestId = Guid.NewGuid().ToString();
        var ctx = new ActiveRequestContext
        {
            RequestId = requestId,
            AgentName = agent.GetType().Name,
            StartTime = DateTime.UtcNow
        };
        _activeContexts[requestId] = ctx;
        _activeRequests[requestId] = new ShutdownRequest
        {
            RequestId = requestId,
            AgentName = ctx.AgentName,
            RegisteredAt = ctx.StartTime
        };
        return requestId;
    }

    // ── 中断标记管理 ──────────────────────────────────────────────────

    /// <summary>
    /// 检查指定 Agent 在指定会话中是否曾被中断关闭。
    /// 恢复时调用，决定是否需要重新初始化。
    /// 对应 Java: checkAndClearShutdownInterrupted(String agentName, String sessionId)
    /// </summary>
    public bool WasShutdownInterrupted(string agentName, string sessionId)
    {
        if (string.IsNullOrEmpty(agentName) || string.IsNullOrEmpty(sessionId))
            return false;

        if (_shutdownInterrupted.TryGetValue(agentName, out var sessions))
        {
            return sessions.ContainsKey(sessionId);
        }
        return false;
    }

    /// <summary>
    /// 清除指定 Agent/会话的中断标记（恢复确认后调用）。
    /// 对应 Java: checkAndClearShutdownInterrupted(String agentName, String sessionId)
    /// </summary>
    public void ClearShutdownInterrupted(string agentName, string sessionId)
    {
        if (string.IsNullOrEmpty(agentName) || string.IsNullOrEmpty(sessionId))
            return;

        if (_shutdownInterrupted.TryGetValue(agentName, out var sessions))
        {
            sessions.TryRemove(sessionId, out _);
        }
    }

    /// <summary>
    /// 检查并清除中断标记（检查 + 清除原子操作）。
    /// 返回 true 表示该 Agent/Session 之前被中断过。
    /// 对应 Java: checkAndClearShutdownInterrupted(String agentName, String sessionId)
    /// </summary>
    public bool CheckAndClearShutdownInterrupted(string agentName, string sessionId)
    {
        if (string.IsNullOrEmpty(agentName) || string.IsNullOrEmpty(sessionId))
            return false;

        if (_shutdownInterrupted.TryGetValue(agentName, out var sessions))
        {
            return sessions.TryRemove(sessionId, out _);
        }
        return false;
    }

    /// <summary>
    /// 标记指定 Agent/会话为中断关闭（由超时或关闭触发）。
    /// </summary>
    private void MarkShutdownInterrupted(string agentName, string sessionId)
    {
        if (string.IsNullOrEmpty(agentName) || string.IsNullOrEmpty(sessionId))
            return;

        var sessions = _shutdownInterrupted.GetOrAdd(agentName, _ => new ConcurrentDictionary<string, bool>());
        sessions[sessionId] = true;
    }

    // ── 超时监控 ──────────────────────────────────────────────────────

    /// <summary>
    /// 启动超时监控线程（每秒检查一次）。
    /// 对应 Java: startTimeoutMonitor()
    /// </summary>
    private void StartTimeoutMonitor()
    {
        lock (_timeoutLock)
        {
            if (_timeoutMonitorThread != null && _timeoutMonitorThread.IsAlive)
                return;

            _timeoutMonitorThread = new Thread(TimeoutMonitorLoop)
            {
                IsBackground = true,
                Name = "ShutdownTimeoutMonitor"
            };
            _timeoutMonitorThread.Start();
        }
    }

    /// <summary>
    /// 超时监控循环体：每秒遍历所有活跃请求，检查是否超时。
    /// 超时的请求会被强制中断并保存状态。
    /// </summary>
    private void TimeoutMonitorLoop()
    {
        while (!_disposed && _state != ShutdownState.Completed)
        {
            try
            {
                if (_state == ShutdownState.Running || _state == ShutdownState.ShuttingDown)
                {
                    EnforceTimedOutRequests();
                }
            }
            catch
            {
                // 监控线程不容许未捕获异常
            }

            try
            {
                Thread.Sleep(1000);
            }
            catch (ThreadInterruptedException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 遍历所有活跃请求，对超过超时阈值的执行强制中断。
    /// 对应 Java: enforceTimeoutAndInterrupt(ActiveRequestContext context)
    /// </summary>
    private void EnforceTimedOutRequests()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _activeContexts.ToArray())
        {
            var ctx = kv.Value;
            if (ctx == null) continue;

            var elapsed = now - ctx.StartTime;
            if (elapsed >= _requestTimeout)
            {
                EnforceTimeoutAndInterrupt(ctx);
            }
        }
    }

    /// <summary>
    /// 对超时请求执行强制中断：取消 CancellationTokenSource 并标记中断。
    /// 对应 Java: enforceTimeoutAndInterrupt(ActiveRequestContext context)
    /// </summary>
    private void EnforceTimeoutAndInterrupt(ActiveRequestContext ctx)
    {
        // 取消令牌，通知正在执行的请求
        try
        {
            ctx.CancellationTokenSource?.Cancel();
        }
        catch
        {
            // 忽略取消时的异常
        }

        // 标记中断以便恢复时检测
        if (!string.IsNullOrEmpty(ctx.AgentName) && !string.IsNullOrEmpty(ctx.SessionId))
        {
            MarkShutdownInterrupted(ctx.AgentName, ctx.SessionId);
        }

        // 触发保存（如果配置了状态持久化）
        SaveOnInterruptObserved(ctx);
    }

    /// <summary>
    /// 中断发生后保存状态。
    /// 对应 Java: saveOnInterruptObserved(ActiveRequestContext context)
    /// </summary>
    private void SaveOnInterruptObserved(ActiveRequestContext ctx)
    {
        // 由中间件或外部处理器在 finally 块中实际执行状态持久化。
        // 此处仅触发标记和日志式回调（可扩展事件）。
        // 实际持久化在 GracefulShutdownMiddleware.OnAgentAsync 的 finally 中完成。
    }

    // ── 关闭流程 ──────────────────────────────────────────────────────

    /// <summary>确保仍在接受请求，否则抛出 AgentShuttingDownException</summary>
    public void EnsureAcceptingRequests()
    {
        if (_state == ShutdownState.ShuttingDown || _state == ShutdownState.Completed)
        {
            throw new AgentShuttingDownException("Agent 正在关闭，不再接受新请求");
        }
    }

    /// <summary>发起关闭</summary>
    public void InitiateShutdown()
    {
        if (_state == ShutdownState.Running)
        {
            _state = ShutdownState.ShuttingDown;
            _globalCts.Cancel();

            // 中断所有活跃请求
            foreach (var kv in _activeContexts.ToArray())
            {
                EnforceTimeoutAndInterrupt(kv.Value);
            }
        }
    }

    /// <summary>完成关闭</summary>
    public void Complete()
    {
        _state = ShutdownState.Completed;
        _activeContexts.Clear();
        _activeRequests.Clear();
    }

    /// <summary>
    /// 等待所有活跃请求完成（最多 waitTimeout）。
    /// 超时后对剩余请求执行强制中断。
    /// </summary>
    public void WaitForCompletion(TimeSpan waitTimeout)
    {
        var start = DateTime.UtcNow;
        while (_activeContexts.Count > 0 && DateTime.UtcNow - start < waitTimeout)
        {
            Thread.Sleep(100);
        }

        // 超时后剩余请求强制中断
        if (_activeContexts.Count > 0)
        {
            foreach (var kv in _activeContexts.ToArray())
            {
                EnforceTimeoutAndInterrupt(kv.Value);
            }
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _globalCts.Cancel();
            _globalCts.Dispose();
            _state = ShutdownState.Completed;
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public enum ShutdownState
{
    Running,
    ShuttingDown,
    Completed
}

public class ShutdownRequest
{
    public string RequestId { get; set; } = "";
    public string AgentName { get; set; } = "";
    public DateTime RegisteredAt { get; set; }
    public object? State { get; set; }
}

/// <summary>
/// Agent 关闭时抛出的异常
/// </summary>
public class AgentShuttingDownException : System.Exception
{
    public AgentShuttingDownException(string message) : base(message) { }
}
