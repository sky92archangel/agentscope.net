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

using System.Collections.Concurrent;
using AgentScope.Core.Agent;

namespace AgentScope.Harness.Subagent.Tasks;

/// <summary>
/// Agent 工厂：按 agentId/租户/context 属性路由到正确的 HarnessAgent 实例。
/// 对应 Java: io.agentscope.extension.agentprotocol.AgentFactory
/// </summary>
public sealed class AgentFactory
{
    private readonly ConcurrentDictionary<string, Func<IAgent>> _registrations = new();

    /// <summary>
    /// 注册一个 agentId 到工厂方法的映射。
    /// </summary>
    public void Register(string agentId, Func<IAgent> factory)
    {
        _registrations[agentId] = factory;
    }

    /// <summary>
    /// 按 agentId 创建（或路由到）对应的 Agent 实例。
    /// </summary>
    public IAgent Create(string agentId)
    {
        if (_registrations.TryGetValue(agentId, out var factory))
            return factory();
        throw new InvalidOperationException($"Unknown agent: {agentId}");
    }

    /// <summary>
    /// 移除指定 agentId 的注册。
    /// </summary>
    public bool Unregister(string agentId) => _registrations.TryRemove(agentId, out _);

    /// <summary>
    /// 获取所有已注册的 agentId 列表。
    /// </summary>
    public ICollection<string> RegisteredAgentIds => _registrations.Keys;
}
