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

namespace AgentScope.Core.Model.GLM;

/// <summary>
/// Zhipu AI (Z.ai) GLM 模型的消息格式化器。
/// 适配 GLM OpenAI 兼容 Chat Completions API。
/// 对应 Java: io.agentscope.extensions.model.openai.compat.glm.GLMFormatter
///
/// GLM API 特性：
/// - 至少需要一个 user 消息（否则返回错误 1214）
/// - 不支持 name 参数
/// - tool_choice 仅支持 "auto"
/// - 不支持 frequency_penalty / presence_penalty / thinking_budget
/// - temperature 范围 [0.0, 1.0]，top_p 范围 [0.01, 1.0]
/// </summary>
public class GlmFormatter : OpenAIBaseFormatter
{
    /// <summary>
    /// 初始化 GlmFormatter。
    /// </summary>
    /// <param name="modelName">GLM 模型名称。</param>
    public GlmFormatter(string modelName) : base(modelName)
    {
    }

    /// <summary>
    /// 格式化消息并应用 GLM 特定的调整。
    /// </summary>
    public override OpenAIRequest Format(List<Msg> messages, GenerateOptions? options = null)
    {
        var request = base.Format(messages, options);

        // 确保至少有一个 user 消息
        request.Messages = EnsureUserMessage(request.Messages);

        // 移除消息中的 name 字段（GLM 不支持）
        request.Messages = request.Messages
            .Select(msg => msg with { Name = null })
            .ToList();

        return request;
    }

    /// <summary>
    /// 应用 GLM 特定的选项调整。
    /// </summary>
    protected override void ApplyOptions(OpenAIRequest request, GenerateOptions options)
    {
        base.ApplyOptions(request, options);

        // 移除 GLM 不支持的参数
        request.FrequencyPenalty = null;
        request.PresencePenalty = null;

        if (options.MaxCompletionTokens.HasValue && !options.MaxTokens.HasValue)
        {
            request.MaxTokens = options.MaxCompletionTokens;
            request.MaxCompletionTokens = null;
        }

        // 钳制 temperature 到 [0.0, 1.0]
        if (request.Temperature.HasValue)
        {
            request.Temperature = Math.Clamp(request.Temperature.Value, 0.0, 1.0);
        }

        // 钳制 top_p 到 [0.01, 1.0]
        if (request.TopP.HasValue)
        {
            request.TopP = Math.Clamp(request.TopP.Value, 0.01, 1.0);
        }
    }

    /// <summary>
    /// 转换工具选择为 GLM 兼容格式。
    /// GLM 仅支持 "auto"。
    /// </summary>
    protected override object ConvertToolChoice(object toolChoice)
    {
        return "auto";
    }

    /// <summary>
    /// 确保消息列表中至少有一个 user 消息。
    /// GLM API 要求至少有一个 user 消息。
    /// </summary>
    private static List<OpenAIMessage> EnsureUserMessage(List<OpenAIMessage> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            return new List<OpenAIMessage>
            {
                new() { Role = "user", Content = "" }
            };
        }

        bool hasUser = messages.Any(m =>
            string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));

        if (hasUser) return messages;

        var result = new List<OpenAIMessage>(messages);
        result.Add(new OpenAIMessage { Role = "user", Content = "" });
        return result;
    }
}
