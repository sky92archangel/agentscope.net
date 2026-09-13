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

namespace AgentScope.Core.State;

/// <summary>
/// 组合式分布式存储的默认实现，委托各组件接口。
/// 对应 Java: io.agentscope.harness.agent.DistributedStore (聚合 Builder + 默认实现)
/// </summary>
public class CompositeDistributedStore : ICompositeDistributedStore
{
    private readonly ConcurrentDictionary<string, (string Value, long Version)> _versionStore = new();

    /// <inheritdoc />
    public IAgentStateStore AgentStateStore { get; }

    /// <inheritdoc />
    public IBaseStore BaseStore { get; }

    /// <inheritdoc />
    public ISnapshotSpec? SnapshotSpec { get; }

    /// <inheritdoc />
    public ISandboxExecutionGuard? ExecutionGuard { get; }

    /// <summary>
    /// 初始化 <see cref="CompositeDistributedStore"/> 的新实例。
    /// </summary>
    internal CompositeDistributedStore(
        IAgentStateStore agentStateStore,
        IBaseStore baseStore,
        ISnapshotSpec? snapshotSpec,
        ISandboxExecutionGuard? executionGuard)
    {
        AgentStateStore = agentStateStore ?? throw new ArgumentNullException(nameof(agentStateStore));
        BaseStore = baseStore ?? throw new ArgumentNullException(nameof(baseStore));
        SnapshotSpec = snapshotSpec;
        ExecutionGuard = executionGuard;
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetAsync(string key, CancellationToken ct = default)
    {
        return await BaseStore.GetAsync(key, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetAsync(string key, string value, CancellationToken ct = default)
    {
        await BaseStore.SetAsync(key, value, ct).ConfigureAwait(false);

        // 同步更新本地版本追踪
        _versionStore.AddOrUpdate(key,
            _ => (value, 1L),
            (_, e) => (value, e.Version + 1));
    }

    /// <inheritdoc />
    public ValueTask<bool> SetIfVersionAsync(string key, string value, long expectedVersion, CancellationToken ct = default)
    {
        var updated = _versionStore.AddOrUpdate(key,
            _ => throw new InvalidOperationException($"键 '{key}' 不存在，无法执行 CAS 写入"),
            (_, existing) =>
            {
                if (existing.Version != expectedVersion)
                    throw new InvalidOperationException($"版本冲突：键 '{key}', 期望 {expectedVersion}, 实际 {existing.Version}");
                return (value, existing.Version + 1);
            });

        // 若 CAS 成功，同步写入底层 BaseStore（fire-and-forget 风格）
        _ = BaseStore.SetAsync(key, value, ct);
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        _versionStore.TryRemove(key, out _);
        return await BaseStore.DeleteAsync(key, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<long> GetVersionAsync(string key, CancellationToken ct = default)
    {
        return ValueTask.FromResult(
            _versionStore.TryGetValue(key, out var e) ? e.Version : 0L);
    }

    /// <summary>
    /// CompositeDistributedStore 构建器（Builder 模式）。
    /// 对应 Java: DistributedStore.Builder
    /// </summary>
    public class Builder
    {
        private IAgentStateStore? _agentStateStore;
        private IBaseStore? _baseStore;
        private ISnapshotSpec? _snapshotSpec;
        private ISandboxExecutionGuard? _executionGuard;

        /// <summary>
        /// 设置 Agent 状态存储组件。
        /// </summary>
        public Builder WithAgentStateStore(IAgentStateStore store)
        {
            _agentStateStore = store ?? throw new ArgumentNullException(nameof(store));
            return this;
        }

        /// <summary>
        /// 设置基础 KV 存储组件。
        /// </summary>
        public Builder WithBaseStore(IBaseStore store)
        {
            _baseStore = store ?? throw new ArgumentNullException(nameof(store));
            return this;
        }

        /// <summary>
        /// 设置快照规范组件（可选）。
        /// </summary>
        public Builder WithSnapshotSpec(ISnapshotSpec spec)
        {
            _snapshotSpec = spec ?? throw new ArgumentNullException(nameof(spec));
            return this;
        }

        /// <summary>
        /// 设置执行守卫组件（可选）。
        /// </summary>
        public Builder WithExecutionGuard(ISandboxExecutionGuard guard)
        {
            _executionGuard = guard ?? throw new ArgumentNullException(nameof(guard));
            return this;
        }

        /// <summary>
        /// 构建 <see cref="CompositeDistributedStore"/> 实例。
        /// </summary>
        /// <returns>构建完成的 CompositeDistributedStore</returns>
        public CompositeDistributedStore Build()
        {
            return new CompositeDistributedStore(
                _agentStateStore ?? throw new InvalidOperationException("AgentStateStore 是必需的，请调用 WithAgentStateStore() 设置"),
                _baseStore ?? throw new InvalidOperationException("BaseStore 是必需的，请调用 WithBaseStore() 设置"),
                _snapshotSpec,
                _executionGuard);
        }
    }
}
