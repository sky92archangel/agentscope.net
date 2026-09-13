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

using AgentScope.Core.Formatter;

namespace AgentScope.Core.Tool;

/// <summary>
/// 工具 Schema 提供者：根据激活组过滤工具并生成 ToolSchema 列表。
///
/// 过滤规则:
/// - 未分组的工具始终包含
/// - 分组的工具仅当其所在组至少有一个激活时才包含
/// - Schema 中优先使用 ExtendedModel 合并后的参数 (通过 RegisteredToolFunction)
///
/// 对标: Java io.agentscope.core.tool.ToolSchemaProvider
/// </summary>
public class ToolSchemaProvider
{
    private readonly ToolRegistry _registry;
    private readonly ToolGroupManager _groupManager;

    internal ToolSchemaProvider(ToolRegistry registry, ToolGroupManager groupManager)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _groupManager = groupManager ?? throw new ArgumentNullException(nameof(groupManager));
    }

    /// <summary>基于当前激活组生成 Schema 列表。</summary>
    public List<ToolSchema> GetSchemas()
    {
        return BuildSchemas(_groupManager.GetActiveToolNames());
    }

    /// <summary>无状态变体：使用外部传入的 activeGroups 解析。</summary>
    public List<ToolSchema> GetSchemas(ICollection<string>? activeGroups)
    {
        return BuildSchemas(_groupManager.GetActiveToolNames(activeGroups));
    }

    private List<ToolSchema> BuildSchemas(HashSet<string> activeTools)
    {
        var schemas = new List<ToolSchema>();

        foreach (var registered in _registry.GetAllRegistered().Values)
        {
            var tool = registered.Tool;
            var toolName = tool.Name;

            // 过滤：已分组但不在 activeTools 中的跳过
            if (_groupManager.IsGroupedTool(toolName) && !activeTools.Contains(toolName))
                continue;

            // 使用 ExtendedModel 合并后的扩展参数
            var parameters = registered.GetExtendedSchema();
            var schema = parameters;

            schemas.Add(new ToolSchema
            {
                Name = toolName,
                Description = tool.Description,
                Parameters = schema.TryGetValue("parameters", out var p)
                    && p is Dictionary<string, object> dict ? dict : null
            });
        }

        return schemas;
    }
}
