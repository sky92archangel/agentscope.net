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
using System.Text.RegularExpressions;
using AgentScope.Core.Agent;
using AgentScope.Core.Tool;

namespace AgentScope.Core.Permission;

/// <summary>
/// Permission mode enumeration controlling the overall permission behavior.
/// 权限模式枚举，控制整体权限行为。
/// Corresponds to Java: io.agentscope.core.permission.PermissionMode
/// 对应 Java: io.agentscope.core.permission.PermissionMode
/// </summary>
public enum PermissionMode
{
    /// <summary>
    /// Default mode - ask for user confirmation when no rules match.
    /// 默认模式 - 无规则匹配时询问用户确认。
    /// </summary>
    Default,

    /// <summary>
    /// Accept edits mode - more permissive for file modification tools.
    /// 接受编辑模式 - 对文件修改工具更宽松。
    /// </summary>
    AcceptEdits,

    /// <summary>
    /// Explore mode - allow read-only operations without confirmation.
    /// 探索模式 - 允许只读操作无需确认。
    /// </summary>
    Explore,

    /// <summary>
    /// Bypass mode - bypass all permission checks (full trust).
    /// 绕过模式 - 绕过所有权限检查（完全信任）。
    /// </summary>
    Bypass,

    /// <summary>
    /// Don't ask mode - automatically allow all operations.
    /// 不询问模式 - 自动允许所有操作。
    /// </summary>
    DontAsk
}

/// <summary>
/// Permission behavior enumeration defining the action to take for a matching rule.
/// 权限行为枚举，定义匹配规则时要采取的操作。
/// Corresponds to Java: io.agentscope.core.permission.PermissionBehavior
/// 对应 Java: io.agentscope.core.permission.PermissionBehavior
/// </summary>
public enum PermissionBehavior
{
    /// <summary>
    /// Allow the operation. 允许操作。
    /// </summary>
    Allow,

    /// <summary>
    /// Deny the operation. 拒绝操作。
    /// </summary>
    Deny,

    /// <summary>
    /// Ask the user for confirmation. 询问用户确认。
    /// </summary>
    Ask,

    /// <summary>
    /// Pass through without decision (let next rule decide).
    /// 透传，不做决策（让下一条规则决定）。
    /// </summary>
    Passthrough
}

/// <summary>
/// A permission rule that maps a wildcard pattern to a behavior.
/// 将通配符模式映射到行为的权限规则。
/// </summary>
/// <param name="Pattern">Wildcard pattern for matching tool names. 用于匹配工具名称的通配符模式。</param>
/// <param name="Behavior">The permission behavior to apply. 要应用的权限行为。</param>
public record PermissionRule(string Pattern, PermissionBehavior Behavior);

/// <summary>
/// The result of a permission evaluation.
/// 权限评估的结果。
/// </summary>
/// <param name="Behavior">The decided permission behavior. 决定的权限行为。</param>
/// <param name="Reason">Human-readable reason for the decision. 决策的人类可读原因。</param>
/// <param name="SuggestedRules">Optional suggested rules for the user to add. 可选的建议用户添加的规则。</param>
/// <param name="UpdatedInput">Optional modified input data. 可选的修改后的输入数据。</param>
public record PermissionDecision(
    PermissionBehavior Behavior,
    string Reason,
    List<string>? SuggestedRules = null,
    Dictionary<string, object>? UpdatedInput = null);

/// <summary>
/// Snapshot of the permission engine's current state for serialization or inspection.
/// 权限引擎当前状态的快照，用于序列化或检查。
/// </summary>
/// <param name="Mode">The current permission mode. 当前权限模式。</param>
/// <param name="WorkingDirectory">The working directory. 工作目录。</param>
/// <param name="AllowRules">List of allow rules. 允许规则列表。</param>
/// <param name="DenyRules">List of deny rules. 拒绝规则列表。</param>
/// <param name="AskRules">List of ask rules. 询问规则列表。</param>
public record PermissionContextState(
    PermissionMode Mode,
    string WorkingDirectory,
    List<PermissionRule> AllowRules,
    List<PermissionRule> DenyRules,
    List<PermissionRule> AskRules);

/// <summary>
/// Represents an additional working directory with its source.
/// 表示一个附加的工作目录及其来源。
/// </summary>
/// <param name="Path">The directory path. 目录路径。</param>
/// <param name="Source">The source of this directory (e.g., config file). 此目录的来源（例如配置文件）。</param>
public record AdditionalWorkingDirectory(string Path, string Source);

/// <summary>
/// Represents a tool call request to be evaluated by the permission engine.
/// 表示要由权限引擎评估的工具调用请求。
/// </summary>
public class ToolCallRequest
{
    /// <summary>
    /// The name of the tool being called.
    /// 正在调用的工具名称。
    /// </summary>
    public string ToolName { get; init; } = "";

    /// <summary>
    /// Optional arguments for the tool call.
    /// 工具调用的可选参数。
    /// </summary>
    public Dictionary<string, object>? Arguments { get; init; }
}

/// <summary>
/// Interface for permission evaluation engines.
/// 权限评估引擎的接口。
/// Corresponds to Java: io.agentscope.core.permission.IPermissionEngine
/// 对应 Java: io.agentscope.core.permission.IPermissionEngine
/// </summary>
public interface IPermissionEngine
{
    /// <summary>
    /// Evaluates a tool call request and returns a permission decision.
    /// 评估工具调用请求并返回权限决策。
    /// </summary>
    /// <param name="request">The tool call request to evaluate. 要评估的工具调用请求。</param>
    /// <returns>The permission decision. 权限决策。</returns>
    PermissionDecision Evaluate(ToolCallRequest request);
}

/// <summary>
/// 9-step priority state machine permission engine:
/// 9 步优先级状态机权限引擎：
/// deny > ask > tool-specific > allow > bypass > mode_acceptedits/explore > path_check > default
/// 
/// The engine evaluates tool call requests against a set of rules with the following priority:
/// 引擎按以下优先级评估工具调用请求：
/// 1. Deny rules (highest priority) / 拒绝规则（最高优先级）
/// 2. Ask rules / 询问规则
/// 3. Tool-specific built-in rules / 工具特定的内置规则
/// 4. Allow rules / 允许规则
/// 5. Bypass mode / 绕过模式
/// 6. AcceptEdits/Explore mode / AcceptEdits/Explore 模式
/// 7. 参数级路径检查
/// 8. AdditionalWorkingDirectory 安全检查
/// 9. Default fallback / 默认回退
/// 
/// Corresponds to Java: io.agentscope.core.permission.PermissionEngine
/// 对应 Java: io.agentscope.core.permission.PermissionEngine
/// </summary>
public class PermissionEngine : IPermissionEngine
{
    /// <summary>
    /// The current permission mode.
    /// 当前权限模式。
    /// </summary>
    private readonly PermissionMode _mode;

    /// <summary>
    /// The list of permission rules to evaluate against.
    /// 要评估的权限规则列表。
    /// </summary>
    private readonly List<PermissionRule> _rules = new();

    /// <summary>
    /// 附加工作目录列表。用于在路径安全评估时放行落在这些目录内的操作。
    /// </summary>
    private readonly List<AdditionalWorkingDirectory> _additionalWorkingDirectories = new();

    /// <summary>
    /// 只读工具名模式前缀集合。
    /// </summary>
    private static readonly HashSet<string> ReadToolPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Read", "List", "Get", "Search", "Glob", "Grep", "Find", "Lookup", "Query", "Stat",
        "Peek", "Fetch", "Check", "View", "Cat", "Head", "Tail", "Show", "Dump", "Inspect"
    };

    /// <summary>
    /// 写工具名模式前缀集合。
    /// </summary>
    private static readonly HashSet<string> WriteToolPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Write", "Edit", "Set", "Create", "Delete", "Remove", "Move", "Copy", "Patch",
        "Update", "Put", "Post", "Upload", "Save", "Store", "Add", "Insert", "Modify",
        "Rename", "Replace", "Merge", "Append", "Truncate", "Format", "Mkdir", "MkFile"
    };

    /// <summary>
    /// filesystem 工具的只读操作名称。
    /// </summary>
    private static readonly HashSet<string> FilesystemReadOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "list", "grep", "glob", "search", "find", "stat", "exists"
    };

    /// <summary>
    /// filesystem 工具的写操作名称。
    /// </summary>
    private static readonly HashSet<string> FilesystemWriteOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "write", "edit", "delete", "remove", "move", "copy", "rename", "mkdir", "mkfile",
        "append", "patch", "chmod", "chown"
    };

    /// <summary>
    /// Initializes a new instance of PermissionEngine with the specified mode.
    /// 使用指定的模式初始化 PermissionEngine 的新实例。
    /// </summary>
    /// <param name="mode">The permission mode. Defaults to Default. 权限模式。默认为 Default。</param>
    public PermissionEngine(PermissionMode mode = PermissionMode.Default) => _mode = mode;

    /// <summary>
    /// Initializes a new instance of PermissionEngine with mode and additional working directories.
    /// 使用指定的模式和附加工作目录初始化 PermissionEngine 的新实例。
    /// </summary>
    /// <param name="mode">The permission mode. 权限模式。</param>
    /// <param name="additionalWorkingDirectories">附加工作目录列表。</param>
    public PermissionEngine(PermissionMode mode, List<AdditionalWorkingDirectory> additionalWorkingDirectories)
    {
        _mode = mode;
        _additionalWorkingDirectories = additionalWorkingDirectories ?? new List<AdditionalWorkingDirectory>();
    }

    /// <summary>
    /// Adds a permission rule to the engine.
    /// 向引擎添加权限规则。
    /// </summary>
    /// <param name="pattern">Wildcard pattern for matching tool names (e.g., "File*", "Read*").
    /// 用于匹配工具名称的通配符模式（例如 "File*", "Read*"）。</param>
    /// <param name="behavior">The permission behavior for matching tools. 匹配工具的权限行为。</param>
    /// <returns>This PermissionEngine instance for fluent chaining. 此 PermissionEngine 实例，支持链式调用。</returns>
    public PermissionEngine AddRule(string pattern, PermissionBehavior behavior)
    {
        _rules.Add(new PermissionRule(pattern, behavior));
        return this;
    }

    /// <summary>
    /// Adds an additional working directory to the engine.
    /// 向引擎添加附加工作目录。
    /// </summary>
    /// <param name="path">Directory path / 目录路径</param>
    /// <param name="source">Source description / 来源说明</param>
    /// <returns>This PermissionEngine instance for fluent chaining. 此 PermissionEngine 实例，支持链式调用。</returns>
    public PermissionEngine AddAdditionalWorkingDirectory(string path, string source)
    {
        _additionalWorkingDirectories.Add(new AdditionalWorkingDirectory(path, source));
        return this;
    }

    /// <summary>
    /// Evaluates a tool call request using the 9-step priority state machine.
    /// 使用 9 步优先级状态机评估工具调用请求。
    /// </summary>
    /// <param name="request">The tool call request to evaluate. 要评估的工具调用请求。</param>
    /// <returns>A PermissionDecision with the evaluation result. 包含评估结果的 PermissionDecision。</returns>
    public PermissionDecision Evaluate(ToolCallRequest request)
    {
        // Step 1: Deny rules have the highest priority
        // 第 1 步: deny 规则最高优先
        foreach (var r in _rules)
        {
            if (r.Behavior == PermissionBehavior.Deny && Regex.IsMatch(request.ToolName, Wildcard(r.Pattern)))
            {
                return new PermissionDecision(PermissionBehavior.Deny, $"deny 规则匹配: {r.Pattern}");
            }
        }

        // Step 2: Ask rules
        // 第 2 步: ask 规则
        foreach (var r in _rules)
        {
            if (r.Behavior == PermissionBehavior.Ask && Regex.IsMatch(request.ToolName, Wildcard(r.Pattern)))
            {
                return new PermissionDecision(PermissionBehavior.Ask, $"ask 规则匹配: {r.Pattern}");
            }
        }

        // Step 3: Tool-specific default behavior (built-in safe tools are auto-allowed)
        // 第 3 步: tool-specific 默认行为（内置安全工具自动放行）
        if (request.ToolName == "CalculatorTool" || request.ToolName == "GetTimeTool")
        {
            return new PermissionDecision(PermissionBehavior.Allow, "内置安全工具自动放行");
        }

        // Step 4: Allow rules
        // 第 4 步: allow 规则
        foreach (var r in _rules)
        {
            if (r.Behavior == PermissionBehavior.Allow && Regex.IsMatch(request.ToolName, Wildcard(r.Pattern)))
            {
                return new PermissionDecision(PermissionBehavior.Allow, $"allow 规则匹配: {r.Pattern}");
            }
        }

        // Step 5: Bypass mode allows all
        // 第 5 步: bypass 模式放行
        if (_mode == PermissionMode.Bypass)
        {
            return new PermissionDecision(PermissionBehavior.Allow, "Bypass 模式: 放行");
        }

        // Step 6: AcceptEdits / Explore mode handling
        // 第 6 步: AcceptEdits / Explore 模式处理
        if (_mode == PermissionMode.AcceptEdits)
        {
            // 只读工具直接放行，写操作需要用户确认
            if (IsReadTool(request.ToolName, request.Arguments))
            {
                return new PermissionDecision(PermissionBehavior.Allow, "AcceptEdits 模式: 只读工具放行");
            }
            return new PermissionDecision(PermissionBehavior.Ask, "AcceptEdits 模式: 写操作需要确认");
        }

        if (_mode == PermissionMode.Explore)
        {
            // 只读工具放行，写操作直接拒绝
            if (IsReadTool(request.ToolName, request.Arguments))
            {
                return new PermissionDecision(PermissionBehavior.Allow, "Explore 模式: 只读操作放行");
            }
            return new PermissionDecision(PermissionBehavior.Deny, "Explore 模式: 禁止写操作");
        }

        // Step 7: 参数级路径检查 - 检测危险路径参数
        // 即使工具名不在 deny 列表，但参数包含敏感路径时也触发 Ask
        var pathCheckResult = CheckArgumentsForDangerousPaths(request.Arguments);
        if (pathCheckResult != null)
        {
            return pathCheckResult;
        }

        // Step 8: AdditionalWorkingDirectory 安全检查
        // 从 RuntimeContext 获取附加工作目录列表，检查路径是否落在允许的目录范围内
        var runtimeCheckResult = CheckRuntimeContextForPathSafety(request.Arguments);
        if (runtimeCheckResult != null)
        {
            return runtimeCheckResult;
        }

        // Step 9: Default fallback
        // 第 9 步: default 回退
        if (_mode == PermissionMode.DontAsk)
        {
            return new PermissionDecision(PermissionBehavior.Allow, "DontAsk 模式: 默认放行");
        }

        return new PermissionDecision(PermissionBehavior.Ask, "无规则匹配，需要用户确认");
    }

    /// <summary>
    /// Converts a wildcard pattern to a regular expression.
    /// 将通配符模式转换为正则表达式。
    /// '*' matches any sequence of characters.
    /// '*' 匹配任意字符序列。
    /// </summary>
    /// <param name="p">The wildcard pattern. 通配符模式。</param>
    /// <returns>The equivalent regular expression pattern. 等效的正则表达式模式。</returns>
    private static string Wildcard(string p) => "^" + Regex.Escape(p).Replace("\\*", ".*") + "$";

    /// <summary>
    /// 判断工具是否为只读工具。
    /// 包括：名称前缀匹配、filesystem 工具的只读操作、已知的只读 shell 命令。
    /// </summary>
    private static bool IsReadTool(string toolName, Dictionary<string, object>? arguments)
    {
        // filesystem 工具：根据 operation 参数判断
        if (toolName.Equals("filesystem", StringComparison.OrdinalIgnoreCase))
        {
            var op = arguments?.GetValueOrDefault("operation")?.ToString() ?? "";
            return FilesystemReadOps.Contains(op);
        }

        // shell_execute：检查命令内容是否只读
        if (toolName.Equals("shell_execute", StringComparison.OrdinalIgnoreCase))
        {
            var cmd = arguments?.GetValueOrDefault("command")?.ToString() ?? "";
            return !ToolDangerousPathConstants.ContainsDangerousCommand(cmd) && IsReadOnlyShellCommand(cmd);
        }

        // 通用前缀匹配
        foreach (var prefix in ReadToolPrefixes)
        {
            if (toolName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 判断工具是否为写工具（修改数据的操作）。
    /// </summary>
    private static bool IsWriteTool(string toolName, Dictionary<string, object>? arguments)
    {
        // filesystem 工具：根据 operation 参数判断
        if (toolName.Equals("filesystem", StringComparison.OrdinalIgnoreCase))
        {
            var op = arguments?.GetValueOrDefault("operation")?.ToString() ?? "";
            return FilesystemWriteOps.Contains(op);
        }

        // shell_execute：包含危险命令关键字的视为写操作
        if (toolName.Equals("shell_execute", StringComparison.OrdinalIgnoreCase))
        {
            var cmd = arguments?.GetValueOrDefault("command")?.ToString() ?? "";
            return ToolDangerousPathConstants.ContainsDangerousCommand(cmd);
        }

        // 通用前缀匹配
        foreach (var prefix in WriteToolPrefixes)
        {
            if (toolName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 判断 shell 命令是否为只读（不修改系统状态）。
    /// </summary>
    private static bool IsReadOnlyShellCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        var trimmed = command.TrimStart();
        // 常见只读命令
        var readCommands = new[] { "ls", "cat", "head", "tail", "echo", "pwd", "whoami",
            "date", "which", "env", "printenv", "type", "where", "find", "grep",
            "rg", "ag", "wc", "sort", "uniq", "diff", "stat", "du", "df",
            "ps", "top", "htop", "ip", "ifconfig", "netstat", "ss",
            "curl", "wget", "ping", "traceroute", "nslookup", "dig",
            "git status", "git log", "git diff", "git show" };
        foreach (var cmd in readCommands)
        {
            if (trimmed.StartsWith(cmd, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 检查工具参数中是否包含危险路径。
    /// 检测 ~/、/etc/、C:\Windows\ 等敏感路径，以及敏感文件名。
    /// 如果路径落在附加工作目录内则放行。
    /// </summary>
    private PermissionDecision? CheckArgumentsForDangerousPaths(Dictionary<string, object>? arguments)
    {
        if (arguments == null || arguments.Count == 0) return null;

        var sensitivePaths = new List<string>();
        var allowedPaths = GetAllowedWorkingDirectories();

        foreach (var kv in arguments)
        {
            var val = kv.Value?.ToString();
            if (string.IsNullOrEmpty(val)) continue;

            // 检查 ~/ 和 ~ （home 目录引用）
            if (val.Contains("~/") || val.Trim().Equals("~", StringComparison.Ordinal))
            {
                if (!IsPathInAllowedDirectories(val, allowedPaths))
                {
                    sensitivePaths.Add($"参数 '{kv.Key}' 包含 home 目录引用: {val}");
                    continue;
                }
            }

            // 检查系统敏感路径
            if (ToolDangerousPathConstants.IsSystemSensitive(val))
            {
                if (!IsPathInAllowedDirectories(val, allowedPaths))
                {
                    sensitivePaths.Add($"参数 '{kv.Key}' 指向系统敏感路径: {val}");
                    continue;
                }
            }

            // 检查敏感文件名
            if (ToolDangerousPathConstants.IsSensitiveFile(val))
            {
                sensitivePaths.Add($"参数 '{kv.Key}' 指向敏感文件: {val}");
            }
        }

        if (sensitivePaths.Count > 0)
        {
            return new PermissionDecision(
                PermissionBehavior.Ask,
                $"检测到危险路径参数:\n{string.Join("\n", sensitivePaths)}",
                SuggestedRules: new List<string> { "添加 allow 规则放行该路径或修改路径参数" });
        }

        return null;
    }

    /// <summary>
    /// 检查 RuntimeContext 中的附加工作目录与当前工具参数路径的安全性。
    /// 如果 RuntimeContext 提供了附加工作目录，且路径落在这些目录内，则允许操作。
    /// </summary>
    private PermissionDecision? CheckRuntimeContextForPathSafety(Dictionary<string, object>? arguments)
    {
        if (arguments == null || arguments.Count == 0) return null;

        var ctx = RuntimeContext.Current;
        if (ctx == null) return null;

        var allowedDirs = new List<string>();
        // 收集当前 RuntimeContext 链中的所有附加工作目录
        var current = ctx;
        while (current != null)
        {
            if (current.AdditionalWorkingDirectories != null)
            {
                foreach (var d in current.AdditionalWorkingDirectories)
                {
                    if (!string.IsNullOrEmpty(d.Path))
                        allowedDirs.Add(d.Path.Replace('\\', '/').TrimEnd('/'));
                }
            }
            current = current.Parent;
        }

        // 合并引擎自身配置的附加工作目录
        foreach (var d in _additionalWorkingDirectories)
        {
            if (!string.IsNullOrEmpty(d.Path))
                allowedDirs.Add(d.Path.Replace('\\', '/').TrimEnd('/'));
        }

        if (allowedDirs.Count == 0) return null;

        // 检查所有路径参数是否在允许的目录范围内
        foreach (var kv in arguments)
        {
            var val = kv.Value?.ToString();
            if (string.IsNullOrEmpty(val)) continue;

            var normalized = val.Replace('\\', '/').TrimEnd('/');
            if (normalized.StartsWith("/") || normalized.Contains(":"))
            {
                // 绝对路径：必须在允许目录内
                var inAllowed = allowedDirs.Any(d =>
                    normalized.StartsWith(d, StringComparison.OrdinalIgnoreCase) ||
                    d.StartsWith(normalized, StringComparison.OrdinalIgnoreCase));

                if (!inAllowed)
                {
                    return new PermissionDecision(
                        PermissionBehavior.Ask,
                        $"路径 '{val}' 不在允许的工作目录范围内，需要确认");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 获取所有允许的工作目录（包括引擎配置的附加工作目录）。
    /// </summary>
    private List<string> GetAllowedWorkingDirectories()
    {
        var dirs = new List<string>();
        foreach (var d in _additionalWorkingDirectories)
        {
            if (!string.IsNullOrEmpty(d.Path))
                dirs.Add(d.Path.Replace('\\', '/').TrimEnd('/'));
        }
        return dirs;
    }

    /// <summary>
    /// 判断路径是否在允许的目录列表内。
    /// </summary>
    private static bool IsPathInAllowedDirectories(string path, List<string> allowedDirs)
    {
        if (allowedDirs.Count == 0) return false;
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        return allowedDirs.Any(d =>
            normalized.StartsWith(d, StringComparison.OrdinalIgnoreCase));
    }
}
