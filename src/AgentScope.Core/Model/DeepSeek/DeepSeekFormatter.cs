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
using System.Linq;
using AgentScope.Core.Formatter.OpenAI;
using AgentScope.Core.Formatter.OpenAI.Dto;
using AgentScope.Core.Message;

namespace AgentScope.Core.Model.DeepSeek;

/// <summary>
/// DeepSeek 模型的消息格式化器（deepseek-v4-flash, deepseek-v4-pro 等）。
/// 适配 DeepSeek OpenAI 兼容 Chat Completions API。
/// 对应 Java: io.agentscope.extensions.model.openai.compat.deepseek.DeepSeekFormatter
///
/// DeepSeek API 特性：
/// - 思考模式下需要保留包含工具调用的段的 reasoning_content
/// - 不支持 strict 参数
/// - 支持 name 字段
/// </summary>
public class DeepSeekFormatter : OpenAIBaseFormatter
{
    private readonly bool _appendEmptyUserIfEndsWithAssistant;

    /// <summary>
    /// 初始化 DeepSeekFormatter。
    /// </summary>
    /// <param name="modelName">DeepSeek 模型名称。</param>
    /// <param name="appendEmptyUserIfEndsWithAssistant">
    /// 如果对话以 assistant 消息结尾时是否追加空 user 消息。
    /// </param>
    public DeepSeekFormatter(string modelName, bool appendEmptyUserIfEndsWithAssistant = false)
        : base(modelName)
    {
        _appendEmptyUserIfEndsWithAssistant = appendEmptyUserIfEndsWithAssistant;
    }

    /// <summary>
    /// 格式化消息并应用 DeepSeek 特定的修复。
    /// </summary>
    public override OpenAIRequest Format(List<Msg> messages, GenerateOptions? options = null)
    {
        var request = base.Format(messages, options);

        // 应用 DeepSeek 消息修复（思考模式下推理内容处理）
        request.Messages = ApplyDeepSeekFixes(request.Messages);

        // 如果需要，在末尾追加空 user 消息
        if (_appendEmptyUserIfEndsWithAssistant)
        {
            request.Messages = AppendEmptyUserIfNeeded(request.Messages);
        }

        return request;
    }

    /// <summary>
    /// 应用 DeepSeek 特定的消息格式修复。
    /// 在思考模式下，保留包含工具调用的段的 reasoning_content。
    /// </summary>
    private static List<OpenAIMessage> ApplyDeepSeekFixes(List<OpenAIMessage> messages)
    {
        if (messages == null || messages.Count == 0) return messages;

        int lastUserIndex = FindLastUserIndex(messages);
        bool thinkingMode = messages.Any(m => !string.IsNullOrEmpty(m.ReasoningContent));
        bool[]? segHasTool = thinkingMode ? ComputeSegmentToolFlags(messages) : null;

        var result = new List<OpenAIMessage>(messages.Count);
        for (int i = 0; i < messages.Count; i++)
        {
            bool isCurrentTurn = i >= lastUserIndex;
            bool needReasoning = thinkingMode
                ? (isCurrentTurn || (segHasTool != null && segHasTool[i]))
                : isCurrentTurn;

            result.Add(FixMessage(messages[i], needReasoning));
        }
        return result;
    }

    /// <summary>
    /// 计算每个消息段是否包含工具调用。
    /// </summary>
    private static bool[] ComputeSegmentToolFlags(List<OpenAIMessage> messages)
    {
        var flags = new bool[messages.Count];
        int prevUser = -1;
        for (int i = 0; i <= messages.Count; i++)
        {
            if (i == messages.Count || string.Equals(messages[i].Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                if (prevUser >= 0)
                {
                    bool hasTool = false;
                    for (int j = prevUser + 1; j < i && !hasTool; j++)
                    {
                        hasTool = messages[j].ToolCalls != null && messages[j].ToolCalls.Count > 0;
                    }
                    if (hasTool)
                    {
                        for (int j = prevUser + 1; j < i; j++)
                        {
                            flags[j] = true;
                        }
                    }
                }
                prevUser = i;
            }
        }
        return flags;
    }

    /// <summary>
    /// 如果对话以 assistant 消息结尾，追加一个空的 user 消息。
    /// </summary>
    private static List<OpenAIMessage> AppendEmptyUserIfNeeded(List<OpenAIMessage> messages)
    {
        if (messages.Count == 0) return messages;

        var last = messages[^1];
        if (string.Equals(last.Role, "assistant", StringComparison.OrdinalIgnoreCase))
        {
            var result = new List<OpenAIMessage>(messages);
            result.Add(new OpenAIMessage { Role = "user", Content = "" });
            return result;
        }
        return messages;
    }

    /// <summary>
    /// 查找最后一个 user 消息的索引。
    /// </summary>
    private static int FindLastUserIndex(List<OpenAIMessage> messages)
    {
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (string.Equals(messages[i].Role, "user", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    /// <summary>
    /// 修复单个消息，在不需要时移除 reasoning_content。
    /// </summary>
    private static OpenAIMessage FixMessage(OpenAIMessage msg, bool needReasoning)
    {
        bool hasReasoning = !string.IsNullOrEmpty(msg.ReasoningContent);
        bool shouldRemoveReasoning = hasReasoning && !needReasoning;

        if (!shouldRemoveReasoning) return msg;

        return new OpenAIMessage
        {
            Role = msg.Role,
            Content = msg.Content,
            Name = msg.Name,
            ToolCalls = msg.ToolCalls,
            ToolCallId = msg.ToolCallId
        };
    }
}
