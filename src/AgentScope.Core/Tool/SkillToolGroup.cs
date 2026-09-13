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
using System.Linq;

namespace AgentScope.Core.Tool;

/// <summary>
/// 技能工具组：按技能领域对工具进行逻辑分组，支持动态激活/禁用。
/// 扩展了 ToolGroup 的语义，增加技能激活触发字段。
///
/// 对标: Java io.agentscope.core.tool.SkillToolGroup
/// </summary>
public class SkillToolGroup
{
    /// <summary>组名称，用作此技能组的唯一标识。</summary>
    public string Name { get; }

    /// <summary>当前组是否激活。非激活组的工具将不可用。</summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 作用域：Meta (Agent 可管理) 或 External (仅开发者)。
    /// 技能组默认 External，因为技能加载应由代码管理。
    /// </summary>
    public ToolGroupScope Scope { get; set; }

    /// <summary>属于此技能组的工具列表。</summary>
    public List<ITool> Tools { get; }

    public SkillToolGroup(
        string name,
        IEnumerable<ITool> tools,
        bool isActive = true,
        ToolGroupScope scope = ToolGroupScope.External)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Tools = tools?.ToList() ?? new List<ITool>();
        IsActive = isActive;
        Scope = scope;
    }
}
