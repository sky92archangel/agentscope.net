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
/// 分布式存储接口，提供版本化 KV + CAS 操作。
/// 对应 Java: io.agentscope.harness.agent.DistributedStore (存储层)
/// </summary>
public interface IDistributedStore
{
    /// <summary>
    /// 获取指定键关联的值。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>找到则返回值，否则返回 null</returns>
    ValueTask<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 无条件设置键的值。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <param name="ct">取消令牌</param>
    ValueTask SetAsync(string key, string value, CancellationToken ct = default);

    /// <summary>
    /// 带版本乐观锁的条件设置（CAS）：仅当版本匹配时写入。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <param name="expectedVersion">期望版本号</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>写入成功返回 true，版本冲突返回 false</returns>
    ValueTask<bool> SetIfVersionAsync(string key, string value, long expectedVersion, CancellationToken ct = default);

    /// <summary>
    /// 删除指定键的条目。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>条目存在并已删除则返回 true，否则 false</returns>
    ValueTask<bool> DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 获取指定键的当前版本号。
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>当前版本号；键不存在则返回 0</returns>
    ValueTask<long> GetVersionAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// 快照规范：支持分布式快照的保存和恢复。
/// 对应 Java: io.agentscope.harness.agent.SnapshotSpec
/// </summary>
public interface ISnapshotSpec
{
    /// <summary>
    /// 保存会话快照。
    /// </summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="snapshotData">快照数据</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>保存成功返回 true</returns>
    Task<bool> SaveSnapshotAsync(string sessionId, string snapshotData, CancellationToken ct = default);

    /// <summary>
    /// 加载会话快照。
    /// </summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>快照数据，未找到则返回 null</returns>
    Task<string?> LoadSnapshotAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 删除会话快照。
    /// </summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>删除成功返回 true</returns>
    Task<bool> DeleteSnapshotAsync(string sessionId, CancellationToken ct = default);
}

/// <summary>
/// 沙箱执行守卫：防止并发执行冲突。
/// 对应 Java: io.agentscope.harness.agent.SandboxExecutionGuard
/// </summary>
public interface ISandboxExecutionGuard
{
    /// <summary>
    /// 尝试获取会话的执行许可。
    /// </summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="timeout">超时时间</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>获取成功返回 true，超时或冲突返回 false</returns>
    Task<bool> TryAcquireAsync(string sessionId, TimeSpan timeout, CancellationToken ct = default);

    /// <summary>
    /// 释放会话的执行许可。
    /// </summary>
    /// <param name="sessionId">会话标识</param>
    Task ReleaseAsync(string sessionId, CancellationToken ct = default);
}
