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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AgentScope.Core.Formatter;
using AgentScope.Core.Message;

namespace AgentScope.Core.Tool;

/// <summary>
/// 工具中心门面：统一注册、分组、激活、Schema 聚合、执行与元工具管理。
///
/// 内部组合:
/// - ToolRegistry: 工具注册表 (双层: ITool + RegisteredToolFunction 元数据)
/// - ToolGroupManager: 组管理器 (META/EXTERNAL 双范围)
/// - ToolSchemaProvider: Schema 提供者 (按组过滤 + ExtendedModel 合并)
/// - MetaToolFactory: 元工具工厂 (reset_equipped_tools)
/// - ToolExecutor: 执行引擎 (重试/超时/并行/外部工具)
///
/// 对标: Java io.agentscope.core.tool.Toolkit
/// </summary>
public class Toolkit
{
    // ---------- 内部组件 ----------

    internal ToolRegistry Registry { get; } = new();
    internal ToolGroupManager GroupManager { get; } = new();
    internal ToolSchemaProvider SchemaProvider { get; }

    private MetaToolFactory? _metaToolFactory;
    private bool _metaToolRegistered;

    /// <summary>已注册的 IToolkitAware 组件列表。</summary>
    private readonly List<IToolkitAware> _awareComponents = new();

    // ---------- 构造 ----------

    public Toolkit()
    {
        SchemaProvider = new ToolSchemaProvider(Registry, GroupManager);
    }

    // ---------- 属性 ----------

    /// <summary>获取所有已注册的工具 (只读)。</summary>
    public IReadOnlyCollection<ITool> AllTools
        => Registry.GetToolNames().Select(n => Registry.GetTool(n)).Where(t => t != null).Cast<ITool>().ToList();

    /// <summary>获取所有工具组 (只读)。</summary>
    public IReadOnlyCollection<ToolGroup> Groups => GroupManager.GetAllGroups().ToList();

    /// <summary>获取工具组管理器实例。</summary>
    public ToolGroupManager GetGroupManager() => GroupManager;

    // ---------- 注册 API ----------

    /// <summary>
    /// 注册一个工具，可选附带元数据和工具组。
    /// </summary>
    public Toolkit AddTool(ITool tool, string? group = null,
                           RegisteredToolFunction? registered = null)
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));

        Registry.RegisterTool(tool.Name, tool, registered);

        if (!string.IsNullOrEmpty(group))
            GroupManager.AddToolToGroup(group!, tool.Name);

        return this;
    }

    /// <summary>
    /// 注册一个工具并关联 RegisteredToolFunction 元数据。
    /// </summary>
    public Toolkit RegisterToolWithMetadata(ITool tool, RegisteredToolFunction registered)
    {
        if (tool == null) throw new ArgumentNullException(nameof(tool));
        if (registered == null) throw new ArgumentNullException(nameof(registered));

        Registry.RegisterTool(tool.Name, tool, registered);
        return this;
    }

    /// <summary>
    /// 通过反射扫描对象上的 [Tool] 方法注册工具。
    /// </summary>
    public Toolkit RegisterTool(object toolObject)
    {
        if (toolObject is null) throw new ArgumentNullException(nameof(toolObject));

        var type = toolObject.GetType();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        foreach (var method in methods)
        {
            var toolAttr = method.GetCustomAttribute<ToolAttribute>();
            if (toolAttr == null) continue;

            var toolName = toolAttr.Name ?? method.Name;
            var description = toolAttr.Description ?? $"[Tool] {type.Name}.{method.Name}";

            var schema = BuildReflectiveSchema(method, toolName, description);
            var wrapper = new ReflectiveTool(toolName, description, schema, toolObject, method,
                                             toolAttr.ExternalTool);

            Registry.RegisterTool(toolName, wrapper);
        }

        return this;
    }

    /// <summary>
    /// 泛型版本：扫描类型 T 上的静态 [Tool] 方法。
    /// </summary>
    public Toolkit RegisterTool<T>() where T : class
    {
        var type = typeof(T);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

        foreach (var method in methods)
        {
            var toolAttr = method.GetCustomAttribute<ToolAttribute>();
            if (toolAttr == null) continue;

            var toolName = toolAttr.Name ?? $"{type.Name}.{method.Name}";
            var description = toolAttr.Description ?? $"[Tool] {type.Name}.{method.Name}";

            var schema = BuildReflectiveSchema(method, toolName, description);
            var wrapper = new ReflectiveTool(toolName, description, schema, null, method,
                                             toolAttr.ExternalTool);

            Registry.RegisterTool(toolName, wrapper);
        }

        return this;
    }

    // ---------- 工具组 API ----------

    public Toolkit AddGroup(ToolGroup group)
    {
        if (group is null) throw new ArgumentNullException(nameof(group));
        GroupManager.RegisterGroup(group);
        return this;
    }

    public Toolkit AddSkillGroup(SkillToolGroup skillGroup)
    {
        if (skillGroup is null) throw new ArgumentNullException(nameof(skillGroup));

        foreach (var tool in skillGroup.Tools)
        {
            Registry.RegisterTool(tool.Name, tool);
            GroupManager.AddToolToGroup(skillGroup.Name, tool.Name);
        }

        // 创建对应的 ToolGroup (如果尚未存在)
        if (GroupManager.GetToolGroup(skillGroup.Name) == null)
        {
            GroupManager.CreateGroup(skillGroup.Name, "", skillGroup.IsActive, skillGroup.Scope);
        }

        return this;
    }

    public Toolkit ActivateGroup(string name)
    {
        GroupManager.ActivateGroup(name ?? "");
        return this;
    }

    public Toolkit DeactivateGroup(string name)
    {
        GroupManager.DeactivateGroup(name ?? "");
        return this;
    }

    // ---------- 元工具 API ----------

    /// <summary>
    /// 注册 reset_equipped_tools 元工具。
    /// Agent 可通过对话动态管理 META 范围工具组的开关。
    /// 幂等：只注册一次。
    /// </summary>
    public Toolkit RegisterMetaTool()
    {
        if (_metaToolRegistered) return this;
        _metaToolFactory ??= new MetaToolFactory(GroupManager);
        var metaTool = _metaToolFactory.CreateResetEquippedToolsTool();
        if (Registry.GetTool(metaTool.Name) == null)
        {
            Registry.RegisterTool(metaTool.Name, metaTool);
        }
        _metaToolRegistered = true;
        return this;
    }

    // ---------- Schema API ----------

    /// <summary>获取当前激活的工具 Schema 列表。</summary>
    public List<ToolSchema> GetActiveToolSchemas()
    {
        return SchemaProvider.GetSchemas();
    }

    /// <summary>基于指定组列表获取 Schema (无状态变体)。</summary>
    public List<ToolSchema> GetActiveToolSchemas(ICollection<string>? activeGroups)
    {
        return SchemaProvider.GetSchemas(activeGroups);
    }

    // ---------- 查询 API ----------

    public ITool? Resolve(string name) => Registry.GetTool(name);

    public RegisteredToolFunction? GetRegisteredTool(string name)
        => Registry.GetRegistered(name);

    /// <summary>获取当前激活的工具列表 (按组过滤)。</summary>
    public IReadOnlyList<ITool> GetActiveTools()
    {
        var toolsByName = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Registry.GetToolNames())
        {
            var tool = Registry.GetTool(name);
            if (tool != null) toolsByName[name] = tool;
        }
        return GroupManager.FilterActiveTools(toolsByName).Values.ToList();
    }

    // ---------- 执行 API ----------

    /// <summary>
    /// 串行执行工具调用列表。
    /// 保留作为向后兼容。
    /// </summary>
    public async Task<List<ToolResultBlock>> CallToolsAsync(
        List<ToolUseBlock> toolCalls,
        ExecutionConfig? config = null)
    {
        var results = new List<ToolResultBlock>();
        foreach (var call in toolCalls)
        {
            var tool = Registry.GetTool(call.Name);
            if (tool == null)
            {
                results.Add(new ToolResultBlock
                {
                    Id = call.Id, Output = $"Unknown tool: {call.Name}", IsError = true
                });
                continue;
            }

            // 外部工具短路
            if (tool.IsExternal)
            {
                results.Add(new ToolResultBlock
                {
                    Id = call.Id, Output = "suspended", IsError = false, IsSuspended = true
                });
                continue;
            }

            try
            {
                // 合并预设参数
                var mergedParams = MergePresetParameters(call.Name, call.Input);
                var executor = new ToolExecutor();
                var result = await executor.ExecuteAsync(tool, mergedParams);
                results.Add(new ToolResultBlock
                {
                    Id = call.Id,
                    Output = result.Success ? result.Result : result.Error,
                    IsError = !result.Success,
                    IsSuspended = result.IsSuspended
                });
            }
            catch (ToolSuspendException)
            {
                results.Add(new ToolResultBlock
                {
                    Id = call.Id, Output = "suspended", IsError = false, IsSuspended = true
                });
            }
            catch (System.Exception ex)
            {
                results.Add(new ToolResultBlock
                {
                    Id = call.Id, Output = ex.Message, IsError = true
                });
            }
        }
        return results;
    }

    /// <summary>
    /// 批量执行工具调用 (支持并行/外部工具短路/超时/重试)。
    /// </summary>
    public async Task<List<ToolResultBlock>> ExecuteToolsAsync(
        List<ToolUseBlock> toolCalls,
        bool parallel = false,
        ExecutionConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var executor = new ToolExecutor();
        return await executor.ExecuteAllAsync(
            this, toolCalls, parallel, config, cancellationToken);
    }

    // ---------- IToolkitAware ----------

    /// <summary>注册一个需要 Toolkit 重新绑定通知的组件。</summary>
    public Toolkit RegisterAwareComponent(IToolkitAware component)
    {
        if (component != null && !_awareComponents.Contains(component))
            _awareComponents.Add(component);
        return this;
    }

    /// <summary>注销一个 IToolkitAware 组件。</summary>
    public Toolkit UnregisterAwareComponent(IToolkitAware component)
    {
        if (component != null) _awareComponents.Remove(component);
        return this;
    }

    // ---------- 深拷贝 ----------

    /// <summary>
    /// 深拷贝 Toolkit。用于 Agent 状态隔离。
    /// 拷贝后会通知所有 IToolkitAware 组件绑定新实例。
    /// </summary>
    public Toolkit DeepCopy()
    {
        var copy = new Toolkit();

        // 复制注册表
        Registry.CopyTo(copy.Registry);

        // 复制组管理器
        copy.GroupManager.CopyFrom(GroupManager);

        // 复制元工具状态
        copy._metaToolRegistered = _metaToolRegistered;

        // 通知 aware 组件绑定新实例
        foreach (var aware in _awareComponents)
        {
            copy.RegisterAwareComponent(aware);
            try { aware.RebindToolkit(copy); }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"IToolkitAware.RebindToolkit failed: {ex.Message}");
            }
        }

        return copy;
    }

    /// <summary>保留向后兼容的简单拷贝。</summary>
    public Toolkit Copy() => DeepCopy();

    // ---------- 内部方法 ----------

    private Dictionary<string, object> MergePresetParameters(
        string toolName, Dictionary<string, object>? callInput)
    {
        var registered = Registry.GetRegistered(toolName);
        if (registered == null || registered.PresetParameters.Count == 0)
            return callInput ?? new Dictionary<string, object>();

        var merged = new Dictionary<string, object>(callInput ?? new());
        foreach (var kv in registered.PresetParameters)
        {
            // 预设参数优先 (框架注入的不可被 LLM 覆盖)
            merged[kv.Key] = kv.Value;
        }
        return merged;
    }

    private static Dictionary<string, object> BuildReflectiveSchema(
        MethodInfo method, string toolName, string description)
    {
        var parameters = new Dictionary<string, object>();
        var required = new List<string>();
        var methodParams = method.GetParameters();

        foreach (var param in methodParams)
        {
            var paramAttr = param.GetCustomAttribute<ToolParamAttribute>();
            var paramName = paramAttr?.Name ?? param.Name ?? "arg";
            var paramDesc = paramAttr?.Description ?? $"Parameter {paramName}";
            var isRequired = paramAttr?.Required ?? true;

            parameters[paramName] = new Dictionary<string, object>
            {
                ["type"] = GetJsonType(param.ParameterType),
                ["description"] = paramDesc
            };
            if (isRequired) required.Add(paramName);
        }

        return new Dictionary<string, object>
        {
            ["name"] = toolName,
            ["description"] = description,
            ["parameters"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = parameters,
                ["required"] = required
            }
        };
    }

    private static string GetJsonType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long) ||
            type == typeof(double) || type == typeof(float) ||
            type == typeof(decimal)) return "number";
        if (type == typeof(bool)) return "boolean";
        if (type.IsArray || (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(List<>))) return "array";
        return "string";
    }

    // ---------- 内部包装器 ----------

    internal class ReflectiveTool : ToolBase
    {
        private readonly object? _instance;
        private readonly MethodInfo _method;
        private readonly Dictionary<string, object> _schema;
        private readonly bool _external;

        public override bool IsExternal => _external;

        public ReflectiveTool(string name, string description, Dictionary<string, object> schema,
                              object? instance, MethodInfo method, bool external = false)
            : base(name, description)
        {
            _schema = schema;
            _instance = instance;
            _method = method;
            _external = external;
        }

        public override Dictionary<string, object> GetSchema() => _schema;

        public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                if (_external)
                    return ToolResult.Suspended(Name);

                var methodParams = _method.GetParameters();
                var args = new object?[methodParams.Length];

                for (int i = 0; i < methodParams.Length; i++)
                {
                    var paramName = methodParams[i].Name ?? $"arg{i}";
                    if (parameters.TryGetValue(paramName, out var val))
                        args[i] = ConvertValue(val, methodParams[i].ParameterType);
                    else
                        args[i] = methodParams[i].DefaultValue;
                }

                var result = _method.Invoke(_instance, args);

                if (result is Task taskResult)
                {
                    await taskResult.ConfigureAwait(false);
                    var resultProp = taskResult.GetType().GetProperty("Result");
                    if (resultProp != null)
                        return ToolResult.Ok(resultProp.GetValue(taskResult)!);
                    return ToolResult.Ok("ok");
                }

                return ToolResult.Ok(result!);
            }
            catch (TargetInvocationException tie)
            {
                if (tie.InnerException is ToolSuspendException)
                    throw tie.InnerException;
                return ToolResult.Fail(tie.InnerException?.Message ?? tie.Message);
            }
            catch (System.Exception ex)
            {
                return ToolResult.Fail(ex.Message);
            }
        }

        private static object? ConvertValue(object val, Type targetType)
        {
            if (targetType == typeof(string)) return val?.ToString();
            if (targetType == typeof(int)) return Convert.ToInt32(val);
            if (targetType == typeof(long)) return Convert.ToInt64(val);
            if (targetType == typeof(double)) return Convert.ToDouble(val);
            if (targetType == typeof(bool)) return Convert.ToBoolean(val);
            return val;
        }
    }
}
