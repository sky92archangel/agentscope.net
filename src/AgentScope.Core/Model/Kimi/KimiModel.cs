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

namespace AgentScope.Core.Model.Kimi;

/// <summary>
/// Kimi (Moonshot AI) 模型提供者，基于 OpenAI 兼容 API。
/// Kimi API 完全兼容 OpenAI API 格式。
/// 对应 Java: io.agentscope.extensions.model.openai.compat.kimi.KimiModelProvider
///
/// 可用模型:
/// - kimi-k3: 最新旗舰思考模型 (1M context)
/// - kimi-k2.7-code: 代码优化模型
/// - kimi-k2.6/kimi-k2.5: 高性能模型
/// - moonshot-v1-*: 上一代模型
///
/// 环境变量:
/// - MOONSHOT_API_KEY / KIMI_API_KEY: Kimi API 密钥
/// - KIMI_MODEL: 模型名称（默认: kimi-k3）
/// </summary>
public class KimiModel : OpenAIModel
{
    /// <summary>
    /// Kimi API 默认基础 URL。
    /// </summary>
    public const string DefaultBaseUrl = "https://api.moonshot.cn/v1";

    /// <summary>
    /// 默认 Kimi 模型名称。
    /// </summary>
    public const string DefaultModel = "kimi-k3";

    /// <summary>
    /// 预定义的 Kimi 模型标识符。
    /// </summary>
    public static class Models
    {
        /// <summary>最新旗舰思考模型</summary>
        public const string K3 = "kimi-k3";
        /// <summary>代码优化模型</summary>
        public const string K27Code = "kimi-k2.7-code";
        /// <summary>高性能模型</summary>
        public const string K26 = "kimi-k2.6";
        /// <summary>通用模型</summary>
        public const string K25 = "kimi-k2.5";
    }

    /// <summary>
    /// 初始化 <see cref="KimiModel"/> 类的新实例。
    /// </summary>
    /// <param name="modelName">模型名称（默认: kimi-k3）。</param>
    /// <param name="apiKey">API 密钥（可选，未提供则依次读取 MOONSHOT_API_KEY / KIMI_API_KEY 环境变量）。</param>
    public KimiModel(
        string modelName = DefaultModel,
        string? apiKey = null)
        : base(
            modelName,
            apiKey ?? ResolveApiKey(),
            DefaultBaseUrl)
    {
    }

    /// <summary>
    /// 从环境变量中解析 Kimi API 密钥。
    /// </summary>
    private static string? ResolveApiKey()
    {
        return Environment.GetEnvironmentVariable("MOONSHOT_API_KEY")
            ?? Environment.GetEnvironmentVariable("KIMI_API_KEY");
    }

    /// <summary>
    /// 创建一个新的 KimiModelBuilder 实例。
    /// </summary>
    public static new KimiModelBuilder Builder()
    {
        return new KimiModelBuilder();
    }
}

/// <summary>
/// 用于创建 KimiModel 实例的流畅构建器。
/// </summary>
public class KimiModelBuilder
{
    private string _modelName = KimiModel.DefaultModel;
    private string? _apiKey;

    /// <summary>设置模型名称。</summary>
    public KimiModelBuilder ModelName(string modelName)
    {
        _modelName = modelName;
        return this;
    }

    /// <summary>使用 kimi-k3 旗舰思考模型。</summary>
    public KimiModelBuilder UseK3()
    {
        _modelName = KimiModel.Models.K3;
        return this;
    }

    /// <summary>设置 API 密钥。</summary>
    public KimiModelBuilder ApiKey(string apiKey)
    {
        _apiKey = apiKey;
        return this;
    }

    /// <summary>构建 KimiModel 实例。</summary>
    public KimiModel Build()
    {
        return new KimiModel(_modelName, _apiKey);
    }
}
