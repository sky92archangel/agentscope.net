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
using System.Collections.Immutable;

namespace AgentScope.Core.Tool;

/// <summary>
/// ITool 的元数据包装器，关联工具实例与扩展信息。
///
/// 包含:
/// - ExtendedModel: 动态注入的参数 Schema
/// - McpClientName: MCP 来源客户端名 (非 MCP 工具为 null)
/// - PresetParameters: 预设参数 (框架注入，LLM 不可修改)
///
/// 对标: Java io.agentscope.core.tool.RegisteredToolFunction
/// </summary>
public class RegisteredToolFunction
{
    public ITool Tool { get; }
    public ExtendedModel? ExtendedModel { get; }
    public string? McpClientName { get; }
    public IReadOnlyDictionary<string, object> PresetParameters { get; private set; }

    public RegisteredToolFunction(
        ITool tool,
        ExtendedModel? extendedModel = null,
        string? mcpClientName = null,
        IReadOnlyDictionary<string, object>? presetParameters = null)
    {
        Tool = tool ?? throw new ArgumentNullException(nameof(tool));
        ExtendedModel = extendedModel;
        McpClientName = mcpClientName;
        PresetParameters = presetParameters ?? ImmutableDictionary<string, object>.Empty;
    }

    /// <summary>
    /// 获取扩展后的参数 Schema：合并 base Schema 与 ExtendedModel。
    /// 若无 ExtendedModel，直接返回工具的原始 Schema。
    /// </summary>
    public Dictionary<string, object> GetExtendedSchema()
    {
        var baseSchema = Tool.GetSchema();
        if (ExtendedModel == null) return baseSchema;
        return ExtendedModel.MergeWithBaseSchema(baseSchema);
    }

    internal void UpdatePresetParameters(IReadOnlyDictionary<string, object>? newParams)
    {
        PresetParameters = newParams ?? ImmutableDictionary<string, object>.Empty;
    }
}
