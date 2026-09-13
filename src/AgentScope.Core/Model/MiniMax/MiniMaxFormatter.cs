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
using AgentScope.Core.Formatter.OpenAI;
using AgentScope.Core.Formatter.OpenAI.Dto;

namespace AgentScope.Core.Model.MiniMax;

/// <summary>
/// MiniMax OpenAI 兼容 Chat Completions 的消息格式化器。
/// 对应 Java: io.agentscope.extensions.model.openai.compat.minimax.MiniMaxFormatter
///
/// MiniMax API 特性：
/// - 使用 reasoning_split 参数拆分推理内容
/// - 使用 max_completion_tokens 替代 max_tokens
/// - 不支持 tool_choice
/// - 不支持 frequency_penalty / presence_penalty / thinking_budget 等参数
/// </summary>
public class MiniMaxFormatter : OpenAIBaseFormatter
{
    /// <summary>
    /// 初始化 MiniMaxFormatter。
    /// </summary>
    /// <param name="modelName">MiniMax 模型名称。</param>
    public MiniMaxFormatter(string modelName) : base(modelName)
    {
    }

    /// <summary>
    /// 应用 MiniMax 特定的选项调整。
    /// </summary>
    protected override void ApplyOptions(OpenAIRequest request, GenerateOptions options)
    {
        // 先应用 reasoning_split
        ApplyReasoningSplit(request);

        base.ApplyOptions(request, options);

        // 移除 MiniMax 不支持的参数
        request.FrequencyPenalty = null;
        request.PresencePenalty = null;
        request.Seed = null;

        // map max_tokens -> max_completion_tokens
        if (options.MaxCompletionTokens.HasValue)
        {
            request.MaxCompletionTokens = options.MaxCompletionTokens;
        }
        else if (options.MaxTokens.HasValue)
        {
            request.MaxCompletionTokens = options.MaxTokens;
        }
        request.MaxTokens = null;
    }

    /// <summary>
    /// MiniMax 不支持 tool_choice，设为 null。
    /// </summary>
    protected override object ConvertToolChoice(object toolChoice)
    {
        return null!;
    }

    /// <summary>
    /// 添加 reasoning_split 参数以将思考内容拆分为 OpenAI 兼容格式。
    /// </summary>
    private static void ApplyReasoningSplit(OpenAIRequest request)
    {
        // OpenAIRequest 中无 ExtraParams，可通过模型 metadata 或自定义方式处理
        // 此处预留 reasoning_split 的逻辑入口
    }
}
