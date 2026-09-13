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

using System.Text;
using AgentScope.Core.Formatter;

namespace AgentScope.Core.Tool;

/// <summary>
/// 工具组管理器：注册/查询/激活组，维护工具-组反向索引。
/// 核心语义：
/// - 未分组的工具始终视为激活 (向后兼容)
/// - META 范围组可由 Agent 通过 reset_equipped_tools 管理
/// - EXTERNAL 范围组仅由开发者管理
///
/// 对标: Java io.agentscope.core.tool.ToolGroupManager
/// </summary>
public class ToolGroupManager
{
    private readonly Dictionary<string, ToolGroup> _groups
        = new(StringComparer.OrdinalIgnoreCase);

    // 反向索引: toolName -> groupNames
    private readonly Dictionary<string, HashSet<string>> _toolToGroups
        = new(StringComparer.OrdinalIgnoreCase);

    // ---------- 属性 ----------

    public bool HasGroups => _groups.Count > 0;

    // ---------- 注册 ----------

    public virtual void RegisterGroup(ToolGroup group)
    {
        if (group == null)
            throw new ArgumentNullException(nameof(group));
        if (_groups.ContainsKey(group.Name))
            throw new InvalidOperationException(
                $"Tool group '{group.Name}' already exists");
        _groups[group.Name] = group;
    }

    public virtual ToolGroup CreateGroup(
        string name,
        string description = "",
        bool active = true,
        ToolGroupScope scope = ToolGroupScope.Meta)
    {
        if (_groups.ContainsKey(name))
            throw new InvalidOperationException(
                $"Tool group '{name}' already exists");
        var group = new ToolGroup(name, description, active, scope);
        _groups[name] = group;
        return group;
    }

    // ---------- 范围感知查询 ----------

    /// <summary>获取所有 META 范围组的名称。</summary>
    public HashSet<string> GetMetaGroupNames()
    {
        return _groups.Values
            .Where(g => g.Scope == ToolGroupScope.Meta)
            .Select(g => g.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取 META 范围组的状态备注 (供 reset_equipped_tools 注入到 description)。
    /// 只返回 META 范围的组信息。
    /// </summary>
    public string GetNotes()
    {
        var metaGroups = _groups.Values.Where(g => g.Scope == ToolGroupScope.Meta).ToList();
        var activeDesc = new List<string>();
        var inactiveDesc = new List<string>();

        foreach (var g in metaGroups)
        {
            var line = string.IsNullOrEmpty(g.Description)
                ? $"- {g.Name}"
                : $"- {g.Name}: {g.Description}";
            if (g.IsActive) activeDesc.Add(line);
            else inactiveDesc.Add(line);
        }

        var sb = new StringBuilder();
        sb.AppendLine("Activated tool groups:");
        sb.AppendLine(activeDesc.Count > 0
            ? string.Join("\n", activeDesc)
            : "  (none)");
        sb.AppendLine();
        sb.AppendLine("Inactive tool groups:");
        sb.AppendLine(inactiveDesc.Count > 0
            ? string.Join("\n", inactiveDesc)
            : "  (none)");
        return sb.ToString();
    }

    /// <summary>获取所有已激活组的描述 (不限 scope)。</summary>
    public string GetActivatedNotes()
    {
        var active = _groups.Values.Where(g => g.IsActive).ToList();
        if (active.Count == 0)
            return "No tool groups are currently activated.";
        var sb = new StringBuilder("Activated tool groups:\n");
        foreach (var g in active)
            sb.AppendLine($"- {g.Name}: {g.Description}");
        return sb.ToString();
    }

    // ---------- 激活状态管理 ----------

    /// <summary>更新指定组的激活状态。</summary>
    public void UpdateGroups(List<string> groupNames, bool active)
    {
        foreach (var name in groupNames)
        {
            if (!_groups.TryGetValue(name ?? "", out var group))
                throw new InvalidOperationException(
                    $"Tool group '{name}' does not exist");
            group.IsActive = active;
        }
    }

    /// <summary>
    /// 替换语义：停用所有 META 范围组，仅激活 toActivate 中列出的组。
    /// EXTERNAL 范围组不受影响。
    /// </summary>
    public void ReplaceMetaActiveGroups(IEnumerable<string> toActivate)
    {
        var activateSet = toActivate?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                          ?? new HashSet<string>();

        // 先停用所有 META 组
        foreach (var group in _groups.Values)
        {
            if (group.Scope == ToolGroupScope.Meta && group.IsActive)
                group.IsActive = false;
        }

        // 激活指定组 (仅限 META 范围)
        foreach (var name in activateSet)
        {
            if (_groups.TryGetValue(name, out var g) && g.Scope == ToolGroupScope.Meta)
                g.IsActive = true;
        }
    }

    /// <summary>删除组，返回受影响的工具名集合。</summary>
    public HashSet<string> RemoveGroups(List<string> groupNames)
    {
        var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in groupNames ?? new List<string>())
        {
            if (!_groups.TryGetValue(name, out var group)) continue;

            foreach (var toolName in group.GetTools())
                affected.Add(toolName);

            _groups.Remove(name);
        }
        // 清理反向索引
        foreach (var toolName in affected)
            _toolToGroups.Remove(toolName);
        return affected;
    }

    // ---------- 工具-组关联 ----------

    public void AddToolToGroup(string groupName, string toolName)
    {
        if (_groups.TryGetValue(groupName ?? "", out var group))
        {
            group.AddTool(toolName);
            if (!_toolToGroups.ContainsKey(toolName ?? ""))
                _toolToGroups[toolName ?? ""] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _toolToGroups[toolName ?? ""].Add(groupName);
        }
    }

    public void RemoveToolFromGroup(string groupName, string toolName)
    {
        if (_groups.TryGetValue(groupName ?? "", out var group))
        {
            group.RemoveTool(toolName);
            if (_toolToGroups.TryGetValue(toolName ?? "", out var groups))
            {
                groups.Remove(groupName);
                if (groups.Count == 0)
                    _toolToGroups.Remove(toolName ?? "");
            }
        }
    }

    // ---------- 激活检测 ----------

    /// <summary>
    /// 检测工具是否在任一激活组中。
    /// 未分组的工具始终视为激活 (向后兼容)。
    /// </summary>
    public bool IsActiveTool(string toolName)
    {
        if (string.IsNullOrEmpty(toolName)) return false;
        if (!_toolToGroups.TryGetValue(toolName, out var groupNames))
            return true; // 未分组的工具默认激活
        return groupNames.Any(n =>
            _groups.TryGetValue(n, out var g) && g.IsActive);
    }

    /// <summary>工具是否已被分组。</summary>
    public bool IsGroupedTool(string toolName)
        => !string.IsNullOrEmpty(toolName) && _toolToGroups.ContainsKey(toolName);

    // ---------- 获取组 ----------

    public ToolGroup? GetToolGroup(string name)
        => _groups.TryGetValue(name ?? "", out var g) ? g : null;

    public IReadOnlyCollection<ToolGroup> GetAllGroups()
        => _groups.Values.ToList();

    public List<string> GetActiveGroupNames()
        => _groups.Values.Where(g => g.IsActive).Select(g => g.Name).ToList();

    /// <summary>激活指定组。</summary>
    public void ActivateGroup(string groupName)
    {
        if (_groups.TryGetValue(groupName ?? "", out var g))
            g.IsActive = true;
    }

    /// <summary>停用指定组。</summary>
    public void DeactivateGroup(string groupName)
    {
        if (_groups.TryGetValue(groupName ?? "", out var g))
            g.IsActive = false;
    }

    /// <summary>直接设置激活组列表 (清除其他所有组的激活状态)。</summary>
    public void SetActiveGroups(IEnumerable<string> groupNames)
    {
        var activeSet = groupNames?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                        ?? new HashSet<string>();
        foreach (var group in _groups.Values)
            group.IsActive = activeSet.Contains(group.Name);
    }

    // ---------- 无状态变体 (用于多 slot 独立解析) ----------

    /// <summary>获取当前激活组中的所有工具名。</summary>
    public HashSet<string> GetActiveToolNames()
    {
        return _groups.Values
            .Where(g => g.IsActive)
            .SelectMany(g => g.GetTools())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>无状态变体：用外部传入的 activeGroups 列表解析工具集。</summary>
    public HashSet<string> GetActiveToolNames(ICollection<string>? activeGroups)
    {
        if (activeGroups == null) return new HashSet<string>();
        return _groups.Values
            .Where(g => activeGroups.Contains(g.Name) && g.IsActive)
            .SelectMany(g => g.GetTools())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // ---------- Schema 过滤 ----------

    /// <summary>
    /// 根据当前激活组过滤工具表。若未注册任何分组，返回全部工具 (向后兼容)。
    /// </summary>
    public IReadOnlyDictionary<string, ITool> FilterActiveTools(
        IReadOnlyDictionary<string, ITool>? toolsByName)
    {
        if (toolsByName == null)
            return new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase);

        if (!HasGroups)
            return toolsByName;

        var activeNames = GetActiveToolNames();
        var filtered = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in activeNames)
        {
            if (toolsByName.TryGetValue(name, out var tool))
                filtered[name] = tool;
        }
        return filtered;
    }

    /// <summary>
    /// 生成当前激活组的 ToolSchema 列表。
    /// </summary>
    public List<ToolSchema> GetActiveToolSchemas(
        IReadOnlyDictionary<string, ITool>? toolsByName)
    {
        var filtered = FilterActiveTools(toolsByName);
        if (filtered.Count == 0)
            return new List<ToolSchema>();

        var list = new List<ToolSchema>();
        foreach (var (name, tool) in filtered)
        {
            var schema = tool.GetSchema();
            var ts = new ToolSchema
            {
                Name = schema.TryGetValue("name", out var n)
                    ? n?.ToString() ?? tool.Name : tool.Name,
                Description = schema.TryGetValue("description", out var d)
                    ? d?.ToString() : tool.Description,
                Parameters = schema.TryGetValue("parameters", out var p)
                    && p is Dictionary<string, object> dict ? dict : null
            };
            list.Add(ts);
        }
        return list;
    }

    // ---------- 复制 ----------

    public ToolGroupManager Copy()
    {
        var copy = new ToolGroupManager();
        foreach (var kv in _groups)
            copy._groups[kv.Key] = new ToolGroup(
                kv.Value.Name, kv.Value.Description,
                kv.Value.IsActive, kv.Value.Scope);
        foreach (var kv in _toolToGroups)
            copy._toolToGroups[kv.Key] =
                new HashSet<string>(kv.Value, StringComparer.OrdinalIgnoreCase);
        return copy;
    }

    /// <summary>
    /// 从另一个 ToolGroupManager 复制所有状态到当前实例。
    /// (用于 Toolkit.DeepCopy 场景，因为 GroupManager 属性不可重新赋值)
    /// </summary>
    public void CopyFrom(ToolGroupManager source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        _groups.Clear();
        _toolToGroups.Clear();
        foreach (var kv in source._groups)
            _groups[kv.Key] = new ToolGroup(
                kv.Value.Name, kv.Value.Description,
                kv.Value.IsActive, kv.Value.Scope);
        foreach (var kv in source._toolToGroups)
            _toolToGroups[kv.Key] =
                new HashSet<string>(kv.Value, StringComparer.OrdinalIgnoreCase);
    }
}
