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

namespace AgentScope.Core.Tool;

/// <summary>
/// 工具组：按功能分组工具，支持动态激活/禁用和范围管理。
/// 用于权限边界控制与能力隔离。
///
/// 对标: Java io.agentscope.core.tool.ToolGroup
/// </summary>
public class ToolGroup
{
    private readonly HashSet<string> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>组名，唯一标识。</summary>
    public string Name { get; }

    /// <summary>组描述 (LLM 可读)。</summary>
    public string Description { get; }

    /// <summary>当前是否激活。</summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 作用域：Meta (Agent 可管理) 或 External (仅开发者管理)。
    /// 默认为 Meta 以保持向后兼容。
    /// </summary>
    public ToolGroupScope Scope { get; }

    public ToolGroup(string name, string description = "", bool isActive = true,
                     ToolGroupScope scope = ToolGroupScope.Meta)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? "";
        IsActive = isActive;
        Scope = scope;
    }

    public void AddTool(string toolName)
    {
        if (!string.IsNullOrWhiteSpace(toolName))
            _tools.Add(toolName.Trim());
    }

    public void RemoveTool(string toolName)
    {
        if (toolName != null)
            _tools.Remove(toolName);
    }

    public bool ContainsTool(string toolName)
        => toolName != null && _tools.Contains(toolName);

    public IReadOnlySet<string> GetTools() => _tools;
}
