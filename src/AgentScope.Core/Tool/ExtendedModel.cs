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
/// 向工具参数 Schema 添加额外属性的扩展模型接口。
///
/// 用于 MCP 工具注册等场景，将远程工具返回的额外参数 Schema
/// 与本地工具的 base Schema 动态合并。
///
/// 对标: Java io.agentscope.core.tool.ExtendedModel
/// </summary>
public interface ExtendedModel
{
    /// <summary>额外属性键值对 (属性名 -> JSON Schema 片段)。</summary>
    IReadOnlyDictionary<string, object> AdditionalProperties { get; }

    /// <summary>额外必填字段列表。</summary>
    IReadOnlyList<string> AdditionalRequired { get; }

    /// <summary>
    /// 合并此扩展模型与工具的 base Schema。
    /// 如果扩展属性与 base 属性重名则抛 InvalidOperationException。
    /// </summary>
    Dictionary<string, object> MergeWithBaseSchema(Dictionary<string, object> baseSchema)
    {
        if (baseSchema == null)
            throw new ArgumentNullException(nameof(baseSchema));

        // 提取 parameters 子结构
        if (!baseSchema.TryGetValue("parameters", out var paramsObj) ||
            paramsObj is not Dictionary<string, object> parameters)
        {
            // 无 parameters 节点，直接构建
            var result = new Dictionary<string, object>(baseSchema);
            result["parameters"] = BuildExtendedParametersSchema();
            return result;
        }

        var mergedParams = new Dictionary<string, object>(parameters);

        // ---- properties 合并 ----
        var baseProps = parameters.TryGetValue("properties", out var bp)
            && bp is Dictionary<string, object> basePropsDict
            ? new Dictionary<string, object>(basePropsDict)
            : new Dictionary<string, object>();

        // 冲突检测
        var conflicts = AdditionalProperties.Keys
            .Where(k => baseProps.ContainsKey(k))
            .ToList();
        if (conflicts.Count > 0)
            throw new InvalidOperationException(
                $"Extended model has conflicting properties with base schema: " +
                string.Join(", ", conflicts));

        foreach (var kv in AdditionalProperties)
            baseProps[kv.Key] = kv.Value;

        mergedParams["properties"] = baseProps;

        // ---- required 合并 ----
        var baseRequired = parameters.TryGetValue("required", out var br)
            ? ParseRequiredList(br)
            : new HashSet<string>();

        foreach (var r in AdditionalRequired)
            baseRequired.Add(r);

        if (baseRequired.Count > 0)
            mergedParams["required"] = baseRequired.ToList();

        var resultSchema = new Dictionary<string, object>(baseSchema);
        resultSchema["parameters"] = mergedParams;
        return resultSchema;
    }

    private Dictionary<string, object> BuildExtendedParametersSchema()
    {
        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>(AdditionalProperties),
            ["required"] = AdditionalRequired.ToList()
        };
    }

    private static HashSet<string> ParseRequiredList(object? required)
    {
        var set = new HashSet<string>();
        if (required is IList<object> list)
            foreach (var item in list)
                if (item?.ToString() is { } s) set.Add(s);
        return set;
    }
}
