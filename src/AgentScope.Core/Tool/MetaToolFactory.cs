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
using System.Text;
using System.Threading.Tasks;

namespace AgentScope.Core.Tool;

/// <summary>
/// 创建元工具 (meta tool)，让 Agent 在运行时通过对话动态管理 META 范围工具组的激活状态。
///
/// 核心工具: reset_equipped_tools
/// - Schema 中只包含 ToolGroupScope.Meta 范围的组
/// - 使用替换语义: 不在 to_activate 中的 META 组全部停用
/// - EXTERNAL 组对元工具不可见且不受影响
///
/// 对标: Java io.agentscope.core.tool.MetaToolFactory
/// </summary>
internal class MetaToolFactory
{
    private readonly ToolGroupManager _groupManager;

    internal MetaToolFactory(ToolGroupManager groupManager)
    {
        _groupManager = groupManager ?? throw new ArgumentNullException(nameof(groupManager));
    }

    /// <summary>创建 reset_equipped_tools 元工具。</summary>
    public ITool CreateResetEquippedToolsTool()
    {
        return new ResetEquippedToolsTool(_groupManager);
    }

    private class ResetEquippedToolsTool : ToolBase
    {
        private readonly ToolGroupManager _groupManager;
        private readonly Dictionary<string, object> _schema;

        public ResetEquippedToolsTool(ToolGroupManager groupManager)
            : base("reset_equipped_tools", BuildDescription(groupManager))
        {
            _groupManager = groupManager;
            _schema = BuildSchema(groupManager);
        }

        public override bool IsExternal => false;

        public override Dictionary<string, object> GetSchema() => _schema;

        public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                var toActivate = ParseToActivate(parameters);

                // 验证：所有组必须存在且为 Meta 范围
                foreach (var name in toActivate)
                {
                    var group = _groupManager.GetToolGroup(name);
                    if (group == null)
                        return Task.FromResult(ToolResult.Fail(
                            $"Tool group '{name}' does not exist."));
                    if (group.Scope != ToolGroupScope.Meta)
                        return Task.FromResult(ToolResult.Fail(
                            $"Group '{name}' is not manageable by this tool " +
                            $"(scope={group.Scope})."));
                }

                // 替换语义
                _groupManager.ReplaceMetaActiveGroups(toActivate);

                var result = BuildResultMessage(toActivate);
                return Task.FromResult(ToolResult.Ok(result));
            }
            catch (System.Exception ex)
            {
                return Task.FromResult(ToolResult.Fail(ex.Message));
            }
        }

        private static List<string> ParseToActivate(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("to_activate", out var raw))
                return new List<string>();

            if (raw is IEnumerable<string> strs)
                return strs.ToList();

            if (raw is System.Text.Json.JsonElement je
                && je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return je.EnumerateArray()
                    .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            return new List<string>();
        }

        private string BuildResultMessage(List<string> toActivate)
        {
            if (toActivate.Count == 0)
                return "All tool groups are currently deactivated.";

            var sb = new StringBuilder();
            sb.AppendLine(
                $"The currently activated tool group(s): {string.Join(", ", toActivate)}.");

            var withDesc = toActivate
                .Select(n => _groupManager.GetToolGroup(n))
                .Where(g => g != null && !string.IsNullOrEmpty(g!.Description))
                .Select(g => g!)
                .ToList();

            if (withDesc.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<tool-instructions>");
                foreach (var g in withDesc)
                    sb.AppendLine($"<group name=\"{g.Name}\">{g.Description}</group>");
                sb.AppendLine("</tool-instructions>");
            }

            return sb.ToString();
        }

        private static string BuildDescription(ToolGroupManager groupManager)
        {
            return
                "Reset your equipped tools based on your current task requirements. " +
                "Specify the FINAL list of tool groups you need; " +
                "groups NOT in this list will be deactivated.\n\n" +
                groupManager.GetNotes();
        }

        private static Dictionary<string, object> BuildSchema(ToolGroupManager groupManager)
        {
            var metaNames = groupManager.GetMetaGroupNames().ToList();

            var items = new Dictionary<string, object>
            {
                ["type"] = "string"
            };
            if (metaNames.Count > 0)
                items["enum"] = metaNames.Cast<object>().ToList();

            var properties = new Dictionary<string, object>
            {
                ["to_activate"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = items,
                    ["description"] =
                        "The FINAL list of tool group names to keep active. " +
                        "Groups not in this list will be deactivated."
                }
            };

            return new Dictionary<string, object>
            {
                ["name"] = "reset_equipped_tools",
                ["description"] =
                    "Reset your equipped tools based on your current task requirements.",
                ["parameters"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = new List<string> { "to_activate" }
                }
            };
        }
    }
}
