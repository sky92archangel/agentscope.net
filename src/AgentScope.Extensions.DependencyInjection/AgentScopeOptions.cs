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

namespace AgentScope.Extensions.DependencyInjection;

/// <summary>
/// AgentScope 配置模型，用于 <see cref="ServiceCollectionExtensions.AddAgentScope"/>。
/// 对标 Java agentscope-spring-boot-starters 的 AgentScopeProperties。
/// </summary>
public class AgentScopeOptions
{
    /// <summary>模型提供程序配置字典，key=标识名，value=JSON 配置字符串。</summary>
    public Dictionary<string, string> ModelConfigs { get; set; } = new();

    /// <summary>是否启用权限引擎（默认 false）。</summary>
    public bool EnablePermission { get; set; }

    /// <summary>默认模型标识名，对应 ModelConfigs 中的 key。</summary>
    public string? DefaultModel { get; set; }
}
