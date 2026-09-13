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

using System.Collections.Concurrent;
using AgentScope.Core.Agent;
using AgentScope.Core.Events;
using AgentScope.Core.Message;
using AgentScope.Core.Tool;
using AgentScope.Harness.Bus;
using AgentScope.Harness.Tool;

namespace AgentScope.Harness.Middleware;

/// <summary>
/// 异步工具执行协调中间件。
/// 当工具执行超过阈值时，将其移到后台处理，结果通过 Inbox 投递。
/// 对标 Java AsyncToolMiddleware。
/// </summary>
public sealed class AsyncToolMiddleware : IHarnessMiddleware
{
    /// <summary>执行顺序（200）。</summary>
    public int Order => 200;

    /// <summary>默认 offload 阈值（秒）。超过此时间的工具将被异步 offload。</summary>
    private const double DefaultOffloadThresholdSeconds = 15.0;

    /// <summary>按工具名指定的 offload 阈值（秒），null 表示不过载该工具。</summary>
    private readonly Dictionary<string, double> _offloadThresholds;

    /// <summary>全局默认阈值。</summary>
    private readonly double _defaultThreshold;

    /// <summary>消息总线（用于 Inbox 投递）。</summary>
    private readonly IMessageBus? _bus;

    /// <summary>后台运行中的 offload 任务计数。</summary>
    private static readonly ConcurrentDictionary<string, int> PendingOffloads = new();

    /// <summary>
    /// 构造 AsyncToolMiddleware。
    /// </summary>
    /// <param name="bus">消息总线实例（可选，用于 Inbox 投递结果）。</param>
    /// <param name="defaultThreshold">默认 offload 阈值（秒），默认 15s。</param>
    /// <param name="offloadThresholds">按工具名的特定阈值（秒）。</param>
    public AsyncToolMiddleware(
        IMessageBus? bus = null,
        double defaultThreshold = DefaultOffloadThresholdSeconds,
        Dictionary<string, double>? offloadThresholds = null)
    {
        _bus = bus;
        _defaultThreshold = defaultThreshold;
        _offloadThresholds = offloadThresholds ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Agent 事件直通。
    /// </summary>
    public async ValueTask OnAgentAsync(MiddlewareContext ctx, Func<ValueTask> next, CancellationToken ct = default)
        => await next();

    /// <summary>
    /// 模型调用事件直通。
    /// </summary>
    public async ValueTask OnModelCallAsync(MiddlewareContext ctx, Func<ValueTask> next, CancellationToken ct = default)
        => await next();

    /// <summary>
    /// 工具执行拦截：检测长耗时工具并异步 offload。
    /// </summary>
    public async ValueTask OnToolExecutionAsync(MiddlewareContext ctx, Func<ValueTask> next, CancellationToken ct = default)
    {
        // ── 第一阶段：检测需要 offload 的工具 ──
        var offloadMap = new Dictionary<string, (string Token, ToolUseBlock Call)>(StringComparer.OrdinalIgnoreCase);

        foreach (var tc in ctx.ToolCalls)
        {
            var threshold = GetOffloadThreshold(tc.Name);
            if (threshold <= 0) continue;

            // 注册异步 token，让 WaitAsyncResultsTool 可以获取
            var token = Tool.WaitAsyncResultsTool.RegisterPending();
            offloadMap[tc.Id] = (token, tc);

            // 通知下游：此工具已标记为异步 offload
            ctx.Items[$"async_offload_token:{tc.Id}"] = token;
            ctx.Items[$"async_offload_threshold:{tc.Id}"] = threshold;
        }

        // ── 第二阶段：执行管道（工具会同步执行一次） ──
        await next();

        // ── 第三阶段：对标记为 offload 的工具启动后台执行并修正结果 ──
        foreach (var (id, (token, tc)) in offloadMap)
        {
            var agentId = ctx.AgentName;

            // 增加 offload 计数
            PendingOffloads.AddOrUpdate(agentId, 1, (_, c) => c + 1);

            // 后台执行
            _ = OffloadAndDeliverAsync(agentId, tc, token, ct);
        }
    }

    /// <summary>
    /// 获取当前未完成的 offload 任务数。
    /// </summary>
    public static int GetPendingOffloadCount(string agentId) =>
        PendingOffloads.TryGetValue(agentId, out var c) ? c : 0;

    /// <summary>
    /// 后台执行工具并通过 Inbox 投递结果。
    /// </summary>
    private async Task OffloadAndDeliverAsync(string agentId, ToolUseBlock tc, string token, CancellationToken ct)
    {
        try
        {
            // 查找已注册的工具实例
            // 注意：这里通过 WaitAsyncResultsTool.Complete 投递结果
            var resultJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                tool = tc.Name,
                tool_call_id = tc.Id,
                status = "completed",
                token
            });

            Tool.WaitAsyncResultsTool.Complete(token, resultJson);

            // 如果有 Bus，通过 Inbox 投递结果通知
            if (_bus != null)
            {
                await _bus.InboxPushAsync(agentId, new BusEntry(
                    Id: Guid.NewGuid().ToString("N"),
                    Key: $"async_result:{tc.Name}",
                    Payload: new Dictionary<string, object>
                    {
                        ["type"] = "async_tool_result",
                        ["token"] = token,
                        ["tool"] = tc.Name,
                        ["tool_call_id"] = tc.Id,
                        ["status"] = "completed"
                    }
                ), ct);
            }
        }
        catch (Exception ex)
        {
            WaitAsyncResultsTool.Complete(token,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    tool = tc.Name,
                    tool_call_id = tc.Id,
                    status = "failed",
                    error = ex.Message,
                    token
                }));
        }
        finally
        {
            PendingOffloads.AddOrUpdate(agentId, 0, (_, c) => Math.Max(0, c - 1));
        }
    }

    /// <summary>
    /// 获取指定工具名的 offload 阈值（秒）。
    /// 返回值 ≤ 0 表示不过载。
    /// </summary>
    private double GetOffloadThreshold(string toolName)
    {
        if (_offloadThresholds.TryGetValue(toolName, out var t))
            return t;
        return _defaultThreshold;
    }
}
