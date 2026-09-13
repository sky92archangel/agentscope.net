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

using AgentScope.Extensions.Store;

namespace AgentScope.Extensions.Store.MongoDB;

/// <summary>
/// 基于 MongoDB 的 Agent 状态存储。
/// 底层通过 <see cref="MongoDistributedStore"/> 实现持久化，在 <see cref="DistributedAgentStateStore"/>
/// 之上提供 Agent 运行时状态的存取语义。
/// 对应 Java: io.agentscope.extensions.mongodb.state.MongoAgentStateStore（最小可用版本）。
/// </summary>
public sealed class MongoAgentStateStore : DistributedAgentStateStore
{
    /// <summary>
    /// 使用已有的 <see cref="MongoDistributedStore"/> 初始化状态存储。
    /// </summary>
    /// <param name="store">底层的 MongoDB 分布式存储实例。</param>
    /// <param name="keyPrefix">所有键的前缀，默认为 "agentstate"。</param>
    public MongoAgentStateStore(MongoDistributedStore store, string keyPrefix = "agentstate")
        : base(store, keyPrefix)
    {
    }

    /// <summary>
    /// 便捷构造：直接传入 MongoDB 连接字符串，自动创建底层存储。
    /// </summary>
    /// <param name="connectionString">MongoDB 连接字符串（如 "mongodb://localhost:27017"）。</param>
    /// <param name="keyPrefix">所有键的前缀，默认为 "agentstate"。</param>
    public MongoAgentStateStore(string connectionString, string keyPrefix = "agentstate")
        : this(new MongoDistributedStore(connectionString), keyPrefix)
    {
    }
}
