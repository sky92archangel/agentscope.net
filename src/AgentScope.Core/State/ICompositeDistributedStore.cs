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

namespace AgentScope.Core.State;

/// <summary>
/// 基础 KV 存储接口。
/// 对应 Java: io.agentscope.harness.agent.BaseStore
/// </summary>
public interface IBaseStore
{
    /// <summary>
    /// 获取指定键关联的值。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>找到则返回值，否则返回 null</returns>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 设置键的值。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>设置成功返回 true</returns>
    Task<bool> SetAsync(string key, string value, CancellationToken ct = default);

    /// <summary>
    /// 删除指定键的条目。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>条目存在并已删除则返回 true，否则 false</returns>
    Task<bool> DeleteAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// 组合式分布式存储：聚合 agentState/baseStore/snapshotSpec/executionGuard 等组件。
/// 对应 Java: io.agentscope.harness.agent.DistributedStore (聚合接口)
/// </summary>
public interface ICompositeDistributedStore : IDistributedStore
{
    /// <summary>
    /// Agent 状态存储组件。
    /// </summary>
    IAgentStateStore AgentStateStore { get; }

    /// <summary>
    /// 基础 KV 存储组件。
    /// </summary>
    IBaseStore BaseStore { get; }

    /// <summary>
    /// 快照规范组件（可选）。
    /// </summary>
    ISnapshotSpec? SnapshotSpec { get; }

    /// <summary>
    /// 执行守卫组件（可选）。
    /// </summary>
    ISandboxExecutionGuard? ExecutionGuard { get; }
}
