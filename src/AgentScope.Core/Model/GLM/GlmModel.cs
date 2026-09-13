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

namespace AgentScope.Core.Model.GLM;

/// <summary>
/// Zhipu AI (Z.ai) GLM 模型提供者，基于 OpenAI 兼容 API。
/// GLM 模型通过 OpenAI 兼容的 Chat Completions 端点提供服务。
/// 对应 Java: io.agentscope.extensions.model.openai.compat.glm.GLMModelProvider
///
/// 可用模型:
/// - glm-5.2: 最新旗舰模型 (1M context)
/// - glm-5.1: 高性能模型 (200K context)
/// - glm-5: 标准模型 (200K context)
/// - glm-4.7/4.6: 上一代模型
///
/// 环境变量:
/// - ZAI_API_KEY / GLM_API_KEY / ZHIPUAI_API_KEY: GLM API 密钥
/// - GLM_MODEL: 模型名称（默认: glm-5.2）
/// </summary>
public class GlmModel : OpenAIModel
{
    /// <summary>
    /// Zhipu AI GLM API 默认基础 URL。
    /// </summary>
    public const string DefaultBaseUrl = "https://open.bigmodel.cn/api/paas/v4";

    /// <summary>
    /// 默认 GLM 模型名称。
    /// </summary>
    public const string DefaultModel = "glm-5.2";

    /// <summary>
    /// 预定义的 GLM 模型标识符。
    /// </summary>
    public static class Models
    {
        /// <summary>最新旗舰模型</summary>
        public const string Glm52 = "glm-5.2";
        /// <summary>高性能模型</summary>
        public const string Glm51 = "glm-5.1";
        /// <summary>标准模型</summary>
        public const string Glm5 = "glm-5";
        /// <summary>GLM-4.7 模型</summary>
        public const string Glm47 = "glm-4.7";
    }

    /// <summary>
    /// 初始化 <see cref="GlmModel"/> 类的新实例。
    /// </summary>
    /// <param name="modelName">模型名称（默认: glm-5.2）。</param>
    /// <param name="apiKey">API 密钥（可选，未提供则依次读取 ZAI_API_KEY / GLM_API_KEY / ZHIPUAI_API_KEY 环境变量）。</param>
    public GlmModel(
        string modelName = DefaultModel,
        string? apiKey = null)
        : base(
            modelName,
            apiKey ?? ResolveApiKey(),
            DefaultBaseUrl)
    {
    }

    /// <summary>
    /// 从环境变量中解析 GLM API 密钥。
    /// </summary>
    private static string? ResolveApiKey()
    {
        return Environment.GetEnvironmentVariable("ZAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GLM_API_KEY")
            ?? Environment.GetEnvironmentVariable("ZHIPUAI_API_KEY");
    }

    /// <summary>
    /// 创建一个新的 GlmModelBuilder 实例。
    /// </summary>
    public static new GlmModelBuilder Builder()
    {
        return new GlmModelBuilder();
    }
}

/// <summary>
/// 用于创建 GlmModel 实例的流畅构建器。
/// </summary>
public class GlmModelBuilder
{
    private string _modelName = GlmModel.DefaultModel;
    private string? _apiKey;

    /// <summary>设置模型名称。</summary>
    public GlmModelBuilder ModelName(string modelName)
    {
        _modelName = modelName;
        return this;
    }

    /// <summary>使用 glm-5.2 旗舰模型。</summary>
    public GlmModelBuilder UseGlm52()
    {
        _modelName = GlmModel.Models.Glm52;
        return this;
    }

    /// <summary>使用 glm-5.1 高性能模型。</summary>
    public GlmModelBuilder UseGlm51()
    {
        _modelName = GlmModel.Models.Glm51;
        return this;
    }

    /// <summary>设置 API 密钥。</summary>
    public GlmModelBuilder ApiKey(string apiKey)
    {
        _apiKey = apiKey;
        return this;
    }

    /// <summary>构建 GlmModel 实例。</summary>
    public GlmModel Build()
    {
        return new GlmModel(_modelName, _apiKey);
    }
}
