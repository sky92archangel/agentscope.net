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
/// 工具执行结果。
/// </summary>
public class ToolResult
{
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }

    /// <summary>工具已被标记为外部执行挂起 (SchemaOnlyTool 等)。</summary>
    public bool IsSuspended { get; set; }

    public static ToolResult Ok(object result)
    {
        return new ToolResult { Success = true, Result = result };
    }

    public static ToolResult Fail(string error)
    {
        return new ToolResult { Success = false, Error = error };
    }

    /// <summary>创建一个外部工具挂起结果，指示 Agent 暂停等待外部执行。</summary>
    public static ToolResult Suspended(string toolName, string? reason = null)
    {
        return new ToolResult
        {
            Success = true,
            IsSuspended = true,
            Result = new Dictionary<string, object?>
            {
                ["status"] = "suspended",
                ["tool"] = toolName,
                ["reason"] = reason ?? "Awaiting external execution"
            }
        };
    }
}

/// <summary>
/// Agent 可使用的工具接口。
///
/// 对标: Java io.agentscope.core.tool.AgentTool
/// </summary>
public interface ITool
{
    string Name { get; }

    string Description { get; }

    /// <summary>
    /// 获取工具的 JSON Schema (用于 LLM function calling)。
    /// </summary>
    Dictionary<string, object> GetSchema();

    /// <summary>
    /// 是否为外部工具。
    /// 外部工具只有 Schema 没有执行逻辑 (如 SchemaOnlyTool)，
    /// 调用时 Executor 直接返回 Suspended 信号，不执行 ExecuteAsync。
    /// </summary>
    bool IsExternal { get; }

    /// <summary>
    /// 执行工具。
    /// </summary>
    Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters);
}

/// <summary>
/// 支持协作式取消的工具契约。
/// 实现此接口的工具会接收到 ToolExecutor 的超时/取消令牌，
/// 从而在超时时真正中止底层工作，而非仅停止等待。
/// </summary>
public interface ICancellableTool : ITool
{
    Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        System.Threading.CancellationToken cancellationToken);
}

/// <summary>
/// 声明工具的并发安全性。
/// 并发安全的工具可与同批次的其他安全工具同时执行。
/// 不安全的工具始终在独立的串行槽中执行。
///
/// 对标: Java ToolBase.concurrencySafe 字段
/// </summary>
public interface IToolConcurrencySafe
{
    /// <summary>
    /// 是否可与此工具的另一个实例并发执行。
    /// true = 可并行；false = 必须串行 (例如文件写操作)。
    /// </summary>
    bool IsConcurrencySafe { get; }
}

/// <summary>
/// 工具抽象基类。
///
/// 对标: Java io.agentscope.core.tool.ToolBase
/// </summary>
public abstract class ToolBase : ITool
{
    public string Name { get; protected set; }

    public string Description { get; protected set; }

    /// <summary>是否为外部工具。默认为 false，SchemaOnlyTool 重写为 true。</summary>
    public virtual bool IsExternal => false;

    protected ToolBase(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public abstract Dictionary<string, object> GetSchema();

    public abstract Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters);
}
