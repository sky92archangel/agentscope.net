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
using AgentScope.Core.Model.OpenAI;

namespace AgentScope.Core.Model.MiniMax;

/// <summary>
/// MiniMax 模型提供者，基于 OpenAI 兼容 API。
/// MiniMax 提供 OpenAI 兼容的 Chat Completions 接口。
/// 对应 Java: io.agentscope.extensions.model.openai.compat.minimax.MiniMaxModelProvider
///
/// 可用模型:
/// - minimax-m3: 最新旗舰模型 (1M context)
/// - minimax-m2.7: 高性能模型
/// - minimax-m2.5/m2.1/m2: 标准模型
///
/// 环境变量:
/// - MINIMAX_API_KEY: MiniMax API 密钥
/// - MINIMAX_MODEL: 模型名称（默认: minimax-m3）
/// </summary>
public class MiniMaxModel : OpenAIModel
{
    /// <summary>
    /// MiniMax API 默认基础 URL。
    /// </summary>
    public const string DefaultBaseUrl = "https://api.minimaxi.com/v1";

    /// <summary>
    /// 默认 MiniMax 模型名称。
    /// </summary>
    public const string DefaultModel = "minimax-m3";

    /// <summary>
    /// 预定义的 MiniMax 模型标识符。
    /// </summary>
    public static class Models
    {
        /// <summary>最新旗舰模型</summary>
        public const string M3 = "minimax-m3";
        /// <summary>M2.7 高性能模型</summary>
        public const string M27 = "minimax-m2.7";
        /// <summary>M2.5 标准模型</summary>
        public const string M25 = "minimax-m2.5";
        /// <summary>M2.1 标准模型</summary>
        public const string M21 = "minimax-m2.1";
        /// <summary>M2 基础模型</summary>
        public const string M2 = "minimax-m2";
    }

    /// <summary>
    /// 初始化 <see cref="MiniMaxModel"/> 类的新实例。
    /// </summary>
    /// <param name="modelName">模型名称（默认: minimax-m3）。</param>
    /// <param name="apiKey">API 密钥（可选，未提供则读取 MINIMAX_API_KEY 环境变量）。</param>
    public MiniMaxModel(
        string modelName = DefaultModel,
        string? apiKey = null)
        : base(
            modelName,
            apiKey ?? Environment.GetEnvironmentVariable("MINIMAX_API_KEY"),
            DefaultBaseUrl)
    {
    }

    /// <summary>
    /// 创建一个新的 MiniMaxModelBuilder 实例。
    /// </summary>
    public static new MiniMaxModelBuilder Builder()
    {
        return new MiniMaxModelBuilder();
    }
}

/// <summary>
/// 用于创建 MiniMaxModel 实例的流畅构建器。
/// </summary>
public class MiniMaxModelBuilder
{
    private string _modelName = MiniMaxModel.DefaultModel;
    private string? _apiKey;

    /// <summary>设置模型名称。</summary>
    public MiniMaxModelBuilder ModelName(string modelName)
    {
        _modelName = modelName;
        return this;
    }

    /// <summary>使用 minimax-m3 旗舰模型。</summary>
    public MiniMaxModelBuilder UseM3()
    {
        _modelName = MiniMaxModel.Models.M3;
        return this;
    }

    /// <summary>设置 API 密钥。</summary>
    public MiniMaxModelBuilder ApiKey(string apiKey)
    {
        _apiKey = apiKey;
        return this;
    }

    /// <summary>构建 MiniMaxModel 实例。</summary>
    public MiniMaxModel Build()
    {
        return new MiniMaxModel(_modelName, _apiKey);
    }
}
