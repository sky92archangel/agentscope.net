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
using System.Threading.Tasks;

namespace AgentScope.Core.Tool;

/// <summary>
/// 仅有 Schema 定义、没有执行逻辑的外部工具占位。
///
/// 当 Model 调用 SchemaOnlyTool 时，框架将其标记为 "suspended" 并暂停 Agent 的 ReAct 循环，
/// 等待外部系统执行并返回结果。Executor 短路逻辑:
///   如果 tool.IsExternal == true，直接返回 ToolResult.Suspended()，不调用 ExecuteAsync。
///
/// 对标: Java io.agentscope.core.tool.SchemaOnlyTool
/// </summary>
public class SchemaOnlyTool : ToolBase
{
    private readonly Dictionary<string, object> _schema;

    public SchemaOnlyTool(
        string name,
        string description,
        Dictionary<string, object> parameters)
        : base(name, description)
    {
        _schema = new Dictionary<string, object>
        {
            ["name"] = name,
            ["description"] = description,
            ["parameters"] = parameters ?? new Dictionary<string, object>()
        };
    }

    public SchemaOnlyTool(
        string name,
        string description,
        Dictionary<string, object> parameters,
        bool strict)
        : this(name, description, parameters)
    {
        _schema["strict"] = strict;
    }

    public SchemaOnlyTool(
        string name,
        string description,
        Dictionary<string, object> parameters,
        bool strict,
        bool readOnly)
        : this(name, description, parameters, strict)
    {
        _schema["readOnly"] = readOnly;
    }

    /// <summary>外部工具：永远为 true。Executor 发现此标志后短路返回 suspended。</summary>
    public override bool IsExternal => true;

    public override Dictionary<string, object> GetSchema() => _schema;

    /// <summary>
    /// 备用防御。正常路径下 ToolExecutor 会在 IsExternal=true 时短路，
    /// 此方法不应被直接调用。
    /// </summary>
    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        throw new ToolSuspendException(
            $"SchemaOnlyTool '{Name}' is external and cannot be executed locally.",
            suspendId: Name);
    }
}
