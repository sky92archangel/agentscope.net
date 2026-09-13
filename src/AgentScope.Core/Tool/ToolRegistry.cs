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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace AgentScope.Core.Tool;

/// <summary>
/// 工具注册表，维护工具实例与元数据的双层存储。
/// 线程安全 (ConcurrentDictionary)。
///
/// 对标: Java io.agentscope.core.tool.ToolRegistry
/// </summary>
internal class ToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, RegisteredToolFunction> _registered
        = new(StringComparer.OrdinalIgnoreCase);

    // ---------- 注册 ----------

    public void RegisterTool(string name, ITool tool, RegisteredToolFunction? registered = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tool name cannot be null or blank", nameof(name));
        _tools[name] = tool ?? throw new ArgumentNullException(nameof(tool));
        if (registered != null)
            _registered[name] = registered;
    }

    // ---------- 查询 ----------

    public ITool? GetTool(string name)
        => !string.IsNullOrWhiteSpace(name) && _tools.TryGetValue(name, out var t) ? t : null;

    public RegisteredToolFunction? GetRegistered(string name)
        => !string.IsNullOrWhiteSpace(name) && _registered.TryGetValue(name, out var r) ? r : null;

    public IReadOnlyCollection<string> GetToolNames()
        => _tools.Keys.ToList();

    public IReadOnlyDictionary<string, RegisteredToolFunction> GetAllRegistered()
        => new Dictionary<string, RegisteredToolFunction>(_registered);

    public bool Contains(string name)
        => !string.IsNullOrWhiteSpace(name) && _tools.ContainsKey(name);

    // ---------- 删除 ----------

    public void RemoveTool(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _tools.TryRemove(name, out _);
            _registered.TryRemove(name, out _);
        }
    }

    /// <summary>
    /// CAS 式删除：只有当前实例与 expected 一致才删除。
    /// 避免并发场景下的 TOCTOU 竞争。
    /// </summary>
    public bool RemoveToolIfSame(string name, ITool expected)
    {
        if (string.IsNullOrWhiteSpace(name) || expected == null) return false;
        var removed = _tools.TryRemove(KeyValuePair.Create(name, expected));
        if (removed) _registered.TryRemove(name, out _);
        return removed;
    }

    // ---------- 复制 ----------

    public void CopyTo(ToolRegistry target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        foreach (var kv in _tools)
        {
            _registered.TryGetValue(kv.Key, out var reg);
            target.RegisterTool(kv.Key, kv.Value, reg);
        }
    }
}
