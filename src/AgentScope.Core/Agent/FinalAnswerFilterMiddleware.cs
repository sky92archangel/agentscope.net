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

using System.Collections.Generic;
using System.Threading.Tasks;
using AgentScope.Core.Events;

namespace AgentScope.Core.Agent;

/// <summary>
/// 最终回答过滤器中间件（流式透传版）：
/// 仅延迟"推理文本块"（ReasoningChunk）——一旦本轮出现工具调用，则丢弃已缓冲的
/// 中间轮推理文本（对应 Java 语义"检测到工具调用则抑制中间轮文本"）；
/// 其余事件（含 Summary*、Error、IsLast 终止事件）全部实时透传，绝不吞掉。
/// 对应 Java: io.agentscope.core.middleware.FinalAnswerFilterMiddleware
/// Order = int.MaxValue - 1 确保在 GracefulShutdown 内层执行。
/// </summary>
public sealed class FinalAnswerFilterMiddleware : MiddlewareBase
{
    private readonly bool _keepReasoningEvents;

    /// <summary>
    /// 最内层中间件之一：在 GracefulShutdown (int.MaxValue) 之后执行。
    /// </summary>
    public override int Order => int.MaxValue - 1;

    /// <summary>
    /// 初始化 FinalAnswerFilterMiddleware 的新实例。
    /// </summary>
    /// <param name="keepReasoningEvents">是否保留推理阶段文本（true 时仅透传不抑制）</param>
    public FinalAnswerFilterMiddleware(bool keepReasoningEvents = false)
    {
        _keepReasoningEvents = keepReasoningEvents;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<Event> OnAgentAsync(
        AgentInput input,
        Func<AgentInput, IAsyncEnumerable<Event>> next)
    {
        // 仅缓冲推理文本块；其余事件实时透传
        var pendingReasoning = new List<Event>();
        var seenToolCall = false;
        var suppressReasoning = false;

        await foreach (var ev in next(input).ConfigureAwait(false))
        {
            // 工具调用开始：中间轮推理文本确定为"中间产物"，丢弃缓冲
            if (ev.Type == EventType.ToolCallStart)
            {
                seenToolCall = true;
                suppressReasoning = true;
                pendingReasoning.Clear();
                yield return ev;
                continue;
            }

            // 汇总/终止/错误事件：最终答案通道，先冲刷缓冲再透传（绝不吞掉）
            if (ev.Type == EventType.SummaryStart ||
                ev.Type == EventType.SummaryChunk ||
                ev.Type == EventType.SummaryFinish ||
                ev.Type == EventType.Error ||
                ev.IsLast)
            {
                if (!suppressReasoning && !_keepReasoningEvents && seenToolCall)
                {
                    // 理论上不会走到（出现 Summary 时不应再保留中间文本）
                    pendingReasoning.Clear();
                }

                foreach (var buffered in pendingReasoning)
                {
                    yield return buffered;
                }

                pendingReasoning.Clear();
                suppressReasoning = false;
                yield return ev;
                continue;
            }

            // 推理文本块：延迟决策（可能被后续工具调用抑制）
            if (ev.Type == EventType.ReasoningChunk && !_keepReasoningEvents)
            {
                if (!suppressReasoning)
                {
                    pendingReasoning.Add(ev);
                }

                continue;
            }

            // 其余事件实时透传
            yield return ev;
        }

        // 流结束：若未被抑制（无工具调用的纯文本回答），冲刷缓冲的推理文本
        if (!suppressReasoning)
        {
            foreach (var buffered in pendingReasoning)
            {
                yield return buffered;
            }
        }
    }

    /// <inheritdoc />
    public override Task<ModelCallInput> OnModelCallAsync(
        ModelCallInput input,
        Func<ModelCallInput, Task<ModelCallInput>> next)
        => next(input);
}
