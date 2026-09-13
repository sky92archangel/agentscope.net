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

using System.Runtime.CompilerServices;
using AgentScope.Core.Agent;
using AgentScope.Core.Events;
using AgentScope.Core.Message;

namespace AgentScope.Core.A2A.Server.Executor.Runner;

/// <summary>
/// 默认 AgentRunner 实现。对标 Java ReActAgentWithBuilderRunner。
/// 每次调用使用 Builder 创建新 Agent 实例；taskId 缓存与中断语义由基类承担。
/// StreamAsync 中增加 delta→artifact 转换逻辑（AgentEvent → A2A Artifact）。
/// </summary>
public sealed class ReActAgentWithBuilderRunner(Func<IAgent> agentFactory, string name, string description)
    : BaseReActAgentRunner
{
    public override string AgentName => name;
    public override string AgentDescription => description;

    protected override IAgent BuildAgent() => agentFactory();

    /// <summary>
    /// 流式执行并附加 artifact 元数据。对标 Java ReActAgentWithBuilderRunner.stream。
    /// AgentEvent → A2A Artifact 转换：
    /// - ActingChunk/ActingStart → TextArtifact
    /// - ReasoningChunk/ReasoningStart → ThinkingArtifact
    /// - ToolCallChunk/ToolCallStart → ToolCallArtifact
    /// </summary>
    public override async IAsyncEnumerable<Event> StreamAsync(
        IReadOnlyList<Msg> messages, AgentRequestOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in base.StreamAsync(messages, options, ct))
        {
            // 根据 Event 类型确定 A2A artifact_type
            var artifactType = evt.Type switch
            {
                EventType.ReasoningStart or EventType.ReasoningChunk or EventType.ReasoningFinish => "thinking",
                EventType.ToolCallStart or EventType.ToolCallChunk or EventType.ToolCallFinish => "tool_call",
                EventType.ActingStart or EventType.ActingChunk or EventType.ActingFinish => "text",
                EventType.SummaryStart or EventType.SummaryChunk or EventType.SummaryFinish => "text",
                _ => null
            };

            if (artifactType != null)
            {
                var meta = new Dictionary<string, object>(evt.Metadata) { ["artifact_type"] = artifactType };
                yield return new Event(evt.Type, evt.Message, evt.IsLast, meta);
            }
            else
            {
                yield return evt;
            }
        }
    }
}
