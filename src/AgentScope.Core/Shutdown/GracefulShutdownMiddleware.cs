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
using System.Collections.Generic;
using System.Threading.Tasks;
using AgentScope.Core.Agent;
using AgentScope.Core.Events;

namespace AgentScope.Core.Shutdown;

/// <summary>
/// Graceful shutdown middleware that checks shutdown status in the Agent main invocation chain.
/// 优雅关闭中间件：在 Agent 主调用链中检查关闭状态，确保系统正在接受请求。
/// Corresponds to Java: io.agentscope.core.shutdown.GracefulShutdownMiddleware
/// </summary>
public class GracefulShutdownMiddleware : MiddlewareBase
{
    /// <summary>
    /// The graceful shutdown manager instance used to check shutdown status.
    /// 用于检查关闭状态的优雅关闭管理器实例。
    /// </summary>
    private readonly GracefulShutdownManager _manager;

    /// <summary>
    /// The configuration for graceful shutdown.
    /// 优雅关闭配置。
    /// </summary>
    private readonly GracefulShutdownConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="GracefulShutdownMiddleware"/> class.
    /// 初始化 <see cref="GracefulShutdownMiddleware"/> 类的新实例。
    /// </summary>
    /// <param name="manager">Optional shutdown manager; defaults to the singleton instance. / 可选的关闭管理器；默认为单例实例。</param>
    /// <param name="config">Optional shutdown config; defaults to GracefulShutdownConfig.Default. / 可选的关闭配置。</param>
    public GracefulShutdownMiddleware(GracefulShutdownManager? manager = null, GracefulShutdownConfig? config = null)
    {
        _manager = manager ?? GracefulShutdownManager.Instance;
        _config = config ?? GracefulShutdownConfig.Default;
    }

    /// <summary>
    /// 最外层中间件：Order = int.MaxValue 确保最先执行（进入时最先检查关闭状态）。
    /// </summary>
    public override int Order => int.MaxValue;

    /// <inheritdoc />
    public override async IAsyncEnumerable<Event> OnAgentAsync(
        AgentInput input,
        Func<AgentInput, IAsyncEnumerable<Event>> next)
    {
        // 检查是否仍接受请求，拒绝则抛出 ShutdownException
        _manager.EnsureAcceptingRequests();

        // 构造 ActiveRequestContext
        var agentName = input.Agent?.Name ?? input.Metadata?.GetValueOrDefault("agentName")?.ToString() ?? "unknown";
        var sessionId = input.Metadata?.GetValueOrDefault("sessionId")?.ToString() ?? Guid.NewGuid().ToString("N");
        var ctx = new ActiveRequestContext
        {
            RequestId = Guid.NewGuid().ToString("N"),
            AgentName = agentName,
            SessionId = sessionId,
            StartTime = DateTime.UtcNow,
            CancellationTokenSource = new CancellationTokenSource()
        };

        // 注册请求
        _manager.RegisterRequest(ctx);

        try
        {
            // 执行核心逻辑
            await foreach (var ev in next(input))
            {
                yield return ev;
            }
        }
        finally
        {
            // 检查是否被中断（超时或关闭触发）
            if (_manager.WasShutdownInterrupted(ctx.AgentName, ctx.SessionId))
            {
                // 清除中断标记（已处理）
                _manager.ClearShutdownInterrupted(ctx.AgentName, ctx.SessionId);
            }

            // 注销请求
            _manager.UnregisterRequest(ctx.RequestId);

            // 释放 CancellationTokenSource
            ctx.CancellationTokenSource?.Dispose();
        }
    }

    /// <inheritdoc />
    public override Task<ReasoningInput> OnReasoningAsync(
        ReasoningInput input,
        Func<ReasoningInput, Task<ReasoningInput>> next)
    {
        _manager.EnsureAcceptingRequests();
        return next(input);
    }

    /// <inheritdoc />
    public override Task<ActingInput> OnActingAsync(
        ActingInput input,
        Func<ActingInput, Task<ActingInput>> next)
    {
        _manager.EnsureAcceptingRequests();
        return next(input);
    }

    /// <inheritdoc />
    public override async Task<ModelCallInput> OnModelCallAsync(
        ModelCallInput input,
        Func<ModelCallInput, Task<ModelCallInput>> next)
    {
        // 检查关闭状态
        _manager.EnsureAcceptingRequests();

        // 注入 PartialReasoningPolicy 到 Options 中，供模型调用层决策
        // 不改变消息内容，仅传递策略信息
        var options = input.Options ?? new Dictionary<string, object>();
        if (!options.ContainsKey("partial_reasoning_policy"))
        {
            options["partial_reasoning_policy"] = _config.PartialReasoningPolicy.ToString();
        }

        var modifiedInput = new ModelCallInput
        {
            Messages = input.Messages,
            Options = options,
            Context = input.Context
        };

        return await next(modifiedInput).ConfigureAwait(false);
    }
}
