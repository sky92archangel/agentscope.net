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

namespace AgentScope.Core.Model;

/// <summary>
/// Mapping of known model context window sizes across different providers.
/// Used to determine the maximum number of tokens a model can process in a single request.
/// Supports prefix matching (longest matching prefix wins) for model families.
/// Corresponds to Java: io.agentscope.core.model.ModelContextWindows
/// 各厂商模型上下文窗口大小映射。
/// 用于确定模型在单个请求中可处理的最大 Token 数。
/// 支持前缀匹配（最长匹配前缀优先）以匹配模型系列。
/// 对应 Java: io.agentscope.core.model.ModelContextWindows
/// </summary>
public static class ModelContextWindows
{
    // ===== DeepSeek 模型 / DeepSeek models =====
    /// <summary>
    /// DeepSeek 模型上下文窗口映射（前缀匹配，最长前缀优先）。
    /// </summary>
    public static readonly Dictionary<string, int> DEEPSEEK = new()
    {
        ["deepseek-chat-v4"] = 1_000_000,   // deepseek-chat-v4-* 系列 1M context
        ["deepseek-reasoner-v4"] = 8_192,    // deepseek-reasoner-v4 8K context
        ["deepseek-v4-flash"] = 1_000_000,
        ["deepseek-v4-pro"] = 1_000_000,
    };

    // ===== GLM (Zhipu AI) 模型 / GLM models =====
    /// <summary>
    /// GLM 模型上下文窗口映射（前缀匹配，最长前缀优先）。
    /// </summary>
    public static readonly Dictionary<string, int> GLM = new()
    {
        ["glm-5.2"] = 1_000_000,       // GLM-5.2 系列 1M context
        ["glm-5.1"] = 200_000,          // GLM-5.1 系列 200K context
        ["glm-5-turbo"] = 200_000,
        ["glm-5"] = 200_000,            // GLM-5 系列 200K context
        ["glm-4.7-flashx"] = 200_000,
        ["glm-4.7-flash"] = 200_000,
        ["glm-4.7"] = 200_000,
        ["glm-4.6"] = 200_000,
        ["glm-4.5-airx"] = 128_000,
        ["glm-4.5-air"] = 128_000,
        ["glm-4-flashx"] = 128_000,
        ["glm-4-flash"] = 128_000,
        ["glm-4-long"] = 1_000_000,
    };

    // ===== Kimi (Moonshot AI) 模型 / Kimi models =====
    /// <summary>
    /// Kimi 模型上下文窗口映射（前缀匹配，最长前缀优先）。
    /// </summary>
    public static readonly Dictionary<string, int> KIMI = new()
    {
        ["kimi-k3"] = 1_048_576,              // kimi-k3 系列 1M context
        ["kimi-k2.7-code-highspeed"] = 262_144,
        ["kimi-k2.7-code"] = 262_144,
        ["kimi-k2.6"] = 262_144,
        ["kimi-k2.5"] = 262_144,
        ["moonshot-v1-128k-vision-preview"] = 131_072,
        ["moonshot-v1-128k"] = 131_072,
        ["moonshot-v1-32k-vision-preview"] = 32_768,
        ["moonshot-v1-32k"] = 32_768,
        ["moonshot-v1-8k-vision-preview"] = 8_192,
        ["moonshot-v1-8k"] = 8_192,
    };

    // ===== MiniMax 模型 / MiniMax models =====
    /// <summary>
    /// MiniMax 模型上下文窗口映射（前缀匹配，最长前缀优先）。
    /// </summary>
    public static readonly Dictionary<string, int> MINIMAX = new()
    {
        ["minimax-m3"] = 1_000_000,
        ["minimax-m2.7-highspeed"] = 204_800,
        ["minimax-m2.7"] = 204_800,
        ["minimax-m2.5-highspeed"] = 204_800,
        ["minimax-m2.5"] = 204_800,
        ["minimax-m2.1-highspeed"] = 204_800,
        ["minimax-m2.1"] = 204_800,
        ["minimax-m2"] = 204_800,
        ["minimax-t2"] = 204_800,            // MiniMax T2 系列
    };

    // ===== DashScope / Qwen 模型 / DashScope / Qwen models =====
    /// <summary>
    /// DashScope/Qwen 模型上下文窗口映射（前缀匹配，最长前缀优先）。
    /// </summary>
    public static readonly Dictionary<string, int> DASHSCOPE = new()
    {
        // Qwen3 系列
        ["qwen3-235b"] = 131_072,
        ["qwen3-32b"] = 131_072,
        ["qwen3-30b"] = 131_072,
        ["qwen3-14b"] = 131_072,
        ["qwen3-8b"] = 131_072,
        ["qwen3-4b"] = 131_072,
        ["qwen3-1.7b"] = 32_768,
        ["qwen3-0.6b"] = 32_768,
        // Qwen3.7 系列
        ["qwen3.7"] = 131_072,
        // Qwen-Max / Plus / Turbo / Long
        ["qwen-max"] = 32_768,
        ["qwen-plus"] = 131_072,
        ["qwen-turbo"] = 1_000_000,
        ["qwen-long"] = 10_000_000,
        // Qwen2.5 系列
        ["qwen2.5"] = 131_072,
        // QVQ / QwQ
        ["qvq"] = 131_072,
        ["qwq"] = 131_072,
    };

    /// <summary>
    /// 已知模型的上下文窗口大小字典（统一索引，兼容旧代码）。
    /// Key: model identifier (supports wildcard suffix *), Value: context window size in tokens.
    /// 键：模型标识符（支持通配符后缀 *），值：上下文窗口大小（Token 数）。
    /// </summary>
    public static readonly Dictionary<string, int> KnownWindows = new()
    {
        // OpenAI models / OpenAI 模型
        ["gpt-4o"] = 128000,
        ["gpt-4o-mini"] = 128000,
        ["gpt-4-turbo"] = 128000,
        ["gpt-4"] = 8192,
        ["gpt-3.5-turbo"] = 16384,
        ["o1*"] = 200000,
        ["o3-mini*"] = 200000,

        // Anthropic Claude models / Anthropic Claude 模型
        ["claude-sonnet-4-5-20250929"] = 200000,
        ["claude-sonnet-4*"] = 200000,
        ["claude-opus-4*"] = 200000,
        ["claude-3.5-sonnet*"] = 200000,
        ["claude-3-haiku*"] = 200000,

        // DeepSeek models / DeepSeek 模型
        ["deepseek-chat"] = 65536,
        ["deepseek-reasoner"] = 65536,
        ["deepseek-chat-v4*"] = 1000000,
        ["deepseek-reasoner-v4"] = 8192,

        // Gemini models / Gemini 模型
        ["gemini-2.0-flash*"] = 1048576,
        ["gemini-1.5-pro*"] = 1048576,
        ["gemini-1.5-flash*"] = 1048576,

        // DashScope / Qwen models / DashScope / Qwen 模型
        ["qwen-turbo"] = 131072,
        ["qwen-plus"] = 131072,
        ["qwen-max"] = 131072,
        ["qwen3*"] = 131072,
        ["qwq*"] = 131072,

        // Kimi / Moonshot models / Kimi 模型
        ["kimi-k3*"] = 1048576,
        ["kimi-k2*"] = 262144,
        ["moonshot-v1-128k*"] = 131072,
        ["moonshot-v1-32k*"] = 32768,
        ["moonshot-v1-8k*"] = 8192,

        // GLM / Zhipu models / GLM 模型
        ["glm-5.2*"] = 1000000,
        ["glm-5.1*"] = 200000,
        ["glm-5*"] = 200000,
        ["glm-4.7*"] = 200000,
        ["glm-4.6*"] = 200000,
        ["glm-4.5*"] = 128000,
        ["glm-4*"] = 128000,

        // MiniMax models / MiniMax 模型
        ["minimax-m3*"] = 1000000,
        ["minimax-m2*"] = 204800,
        ["minimax-t2*"] = 204800,

        // Ollama common models / Ollama 常用模型
        ["llama3*"] = 8192,
        ["mistral*"] = 8192,
        ["qwen2*"] = 32768,
    };

    /// <summary>
    /// Gets the context window size for the specified model.
    /// Returns null for unknown models.
    /// Supports wildcard matching (patterns ending with *).
    /// 获取指定模型的上下文窗口大小，未知模型返回 null。
    /// 支持通配符匹配（以 * 结尾的模式）。
    /// </summary>
    /// <param name="modelId">Model identifier (e.g., "gpt-4o", "claude-3") / 模型标识符。</param>
    /// <returns>Context window size in tokens, or null if unknown / 上下文窗口大小（Token 数），未知则返回 null。</returns>
    public static int? GetWindowSize(string modelId)
    {
        if (string.IsNullOrEmpty(modelId)) return null;

        // Try exact match first / 先尝试精确匹配
        if (KnownWindows.TryGetValue(modelId, out var size))
        {
            return size;
        }

        // Try wildcard pattern matching / 尝试通配符模式匹配
        foreach (var (pattern, window) in KnownWindows)
        {
            if (pattern.EndsWith("*") && modelId.StartsWith(pattern.TrimEnd('*')))
            {
                return window;
            }
        }

        return null;
    }

    /// <summary>
    /// 使用前缀匹配查找指定模型的上下文窗口大小。
    /// 最长匹配前缀优先。
    /// </summary>
    /// <param name="modelName">模型名称。</param>
    /// <param name="knownModels">已知模型字典。</param>
    /// <returns>上下文窗口大小，未找到则返回 0。</returns>
    public static int Lookup(string modelName, Dictionary<string, int> knownModels)
    {
        if (string.IsNullOrEmpty(modelName) || knownModels == null) return 0;

        string lower = modelName.ToLowerInvariant();
        int bestLen = 0;
        int bestValue = 0;

        foreach (var (prefix, window) in knownModels)
        {
            if (lower.StartsWith(prefix) && prefix.Length > bestLen)
            {
                bestLen = prefix.Length;
                bestValue = window;
            }
        }
        return bestValue;
    }
}
