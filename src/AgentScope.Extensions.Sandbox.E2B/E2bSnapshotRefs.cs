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

namespace AgentScope.Extensions.Sandbox.E2B;

/// <summary>
/// E2B 沙箱快照引用记录：用于从已有快照快速恢复沙箱环境。
/// 对标 Java E2bSnapshotRefs。
/// </summary>
/// <param name="SnapshotId">E2B 快照 id。</param>
/// <param name="TemplateId">创建快照所用的模板 id。</param>
/// <param name="CreatedAt">快照创建时间（UTC ISO8601）。</param>
public sealed record E2bSnapshotRef(
    string SnapshotId,
    string TemplateId,
    DateTime CreatedAt)
{
    /// <summary>快照状态（pending/ready/failed）。</summary>
    public string Status { get; init; } = "ready";

    /// <summary>可选的自定义标签。</summary>
    public string? Label { get; init; }
}

/// <summary>
/// E2B 快照引用集合，用于管理多个快照版本。
/// 对标 Java E2bSnapshotRefs。
/// </summary>
public sealed class E2bSnapshotRefs
{
    /// <summary>所有快照引用列表。</summary>
    public List<E2bSnapshotRef> Snapshots { get; set; } = [];

    /// <summary>最近活跃的快照引用。</summary>
    public E2bSnapshotRef? Latest => Snapshots.Count > 0 ? Snapshots[^1] : null;

    /// <summary>添加快照引用。</summary>
    public void Add(E2bSnapshotRef snapshot) => Snapshots.Add(snapshot);

    /// <summary>按 snapshotId 移除快照引用。</summary>
    public bool Remove(string snapshotId) => Snapshots.RemoveAll(s => s.SnapshotId == snapshotId) > 0;
}
