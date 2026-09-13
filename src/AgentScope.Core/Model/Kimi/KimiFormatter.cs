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
using AgentScope.Core.Formatter.OpenAI;
using AgentScope.Core.Formatter.OpenAI.Dto;
using AgentScope.Core.Message;

namespace AgentScope.Core.Model.Kimi;

/// <summary>
/// Kimi (Moonshot AI) 模型的消息格式化器。
/// 适配 Kimi OpenAI 兼容 Chat Completions API。
/// 对应 Java: io.agentscope.extensions.model.openai.compat.kimi.KimiFormatter
///
/// Kimi API 特性：
/// - kimi-* 系列模型采样参数由平台固定（temperature/top_p/n/frequency_penalty/presence_penalty）
/// - reasoning_effort 仅 kimi-k3 支持
/// - 使用 max_completion_tokens 而非 max_tokens
/// - tool_choice: auto/none 全系列支持，required 仅 kimi-k3
/// </summary>
public class KimiFormatter : OpenAIBaseFormatter
{
    /// <summary>
    /// 初始化 KimiFormatter。
    /// </summary>
    /// <param name="modelName">Kimi 模型名称。</param>
    public KimiFormatter(string modelName) : base(modelName)
    {
    }

    /// <summary>
    /// 应用 Kimi 特定的选项调整。
    /// </summary>
    protected override void ApplyOptions(OpenAIRequest request, GenerateOptions options)
    {
        base.ApplyOptions(request, options);

        string? model = request.Model;

        // kimi-* 系列固定采样参数
        if (HasFixedSamplingParams(model))
        {
            request.Temperature = null;
            request.TopP = null;
            request.FrequencyPenalty = null;
            request.PresencePenalty = null;

            // 移除 n（Kimi 固定为 1）
            // OpenAIRequest 中无 n 属性，略过
        }

        // reasoning_effort 仅 kimi-k3 支持
        if (!string.IsNullOrEmpty(request.ReasoningEffort) && !SupportsReasoningEffort(model))
        {
            request.ReasoningEffort = null;
        }

        // map max_tokens -> max_completion_tokens
        if (request.MaxTokens.HasValue && !request.MaxCompletionTokens.HasValue)
        {
            request.MaxCompletionTokens = request.MaxTokens;
            request.MaxTokens = null;
        }
    }

    /// <summary>
    /// 转换工具选择为 Kimi 兼容格式。
    /// </summary>
    protected override object ConvertToolChoice(object toolChoice)
    {
        // Kimi 支持 auto/none
        if (toolChoice is string str)
        {
            if (str == "auto" || str == "none")
                return str;
            return "auto";
        }

        // 简化处理：Kimi 不支持强制特定函数时需降级
        return "auto";
    }

    /// <summary>
    /// 判断模型是否属于 kimi-* 系列（采样参数固定）。
    /// </summary>
    private static bool HasFixedSamplingParams(string? model)
    {
        return model != null && model.StartsWith("kimi-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断模型是否支持 reasoning_effort。
    /// 仅 kimi-k3 支持。
    /// </summary>
    private static bool SupportsReasoningEffort(string? model)
    {
        return model != null && model.StartsWith("kimi-k3", StringComparison.OrdinalIgnoreCase);
    }
}
