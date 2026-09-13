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
/// 定义 ToolGroup 激活生命周期的管理者。
///
/// - Meta: 由 Agent 通过 reset_equipped_tools 元工具在运行时管理。采用替换语义：
///   只有 to_activate 中明确列出的组保持激活，未列出的 META 组全部停用。
/// - External: 由开发者代码管理。元工具不可见且不可操作。
///
/// 对标: Java io.agentscope.core.tool.ToolGroupScope
/// </summary>
public enum ToolGroupScope
{
    /// <summary>
    /// 由元工具 (reset_equipped_tools) 管理。
    /// Agent 可在运行时通过对话激活/停用这些组。
    /// </summary>
    Meta,

    /// <summary>
    /// 由开发者代码外部管理。
    /// 元工具无法看到或修改这些组。
    /// </summary>
    External
}
