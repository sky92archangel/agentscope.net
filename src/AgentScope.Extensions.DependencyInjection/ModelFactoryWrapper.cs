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

using AgentScope.Core;
using AgentScope.Core.Model;

namespace AgentScope.Extensions.DependencyInjection;

/// <summary>
/// 模型工厂服务接口，包装静态 <see cref="ModelFactory"/> 便于 DI 注入和测试。
/// 对标 Java agentscope-spring-boot-starters 的 ModelFactory Bean 注入。
/// </summary>
public interface IModelFactory
{
    /// <summary>创建模型实例。</summary>
    IModel Create(string provider, string modelName, string apiKey, string? baseUrl = null);

    /// <summary>从配置字典创建模型实例。</summary>
    IModel Create(Dictionary<string, string> config);
}

/// <summary>
/// 默认实现，委托给 <see cref="ModelFactory"/> 静态方法。
/// </summary>
public class ModelFactoryWrapper : IModelFactory
{
    public IModel Create(string provider, string modelName, string apiKey, string? baseUrl = null)
        => ModelFactory.Create(provider, modelName, apiKey, baseUrl);

    public IModel Create(Dictionary<string, string> config)
        => ModelFactory.Create(config);
}
