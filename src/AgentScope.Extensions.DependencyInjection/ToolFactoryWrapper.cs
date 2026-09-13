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
using AgentScope.Core.Tool;

namespace AgentScope.Extensions.DependencyInjection;

/// <summary>
/// 工具工厂服务接口，包装静态 <see cref="ToolFactory"/> 便于 DI 注入和测试。
/// </summary>
public interface IToolFactory
{
    /// <summary>创建指定类型的工具实例。</summary>
    ITool Create(string toolType, Dictionary<string, object>? config = null);

    /// <summary>创建预设工具列表。</summary>
    List<ITool> CreatePreset(ToolPreset preset);
}

/// <summary>
/// 默认实现，委托给 <see cref="ToolFactory"/> 静态方法。
/// </summary>
public class ToolFactoryWrapper : IToolFactory
{
    public ITool Create(string toolType, Dictionary<string, object>? config = null)
        => ToolFactory.Create(toolType, config);

    public List<ITool> CreatePreset(ToolPreset preset)
        => ToolFactory.CreatePreset(preset);
}
