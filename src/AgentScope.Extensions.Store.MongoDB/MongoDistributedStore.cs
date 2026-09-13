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

using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgentScope.Extensions.Store.MongoDB;

/// <summary>
/// MongoDB 分布式存储实现，实现 <see cref="IDistributedStore"/> 接口。
/// 提供基于 MongoDB 的键值读写、删除、前缀列举及异步释放能力。
/// 对应 Java: io.agentscope.extensions.mongodb.MongoDistributedStore（最小可用版本）。
/// </summary>
/// <remarks>
/// 用户可通过连接字符串或已有的 <see cref="MongoClient"/> 创建实例。
/// 通过连接字符串创建时，<see cref="DisposeAsync"/> 会自动关闭内部 MongoClient；
/// 通过外部 MongoClient 创建时，调用方负责其生命周期。
/// </remarks>
public sealed class MongoDistributedStore : IDistributedStore, IDisposable, IAsyncDisposable
{
    private readonly MongoClient? _client;
    private bool _disposed;
    private readonly bool _ownsClient;
    private readonly MongoBaseStore _baseStore;

    /// <summary>
    /// 使用已有的 <see cref="MongoClient"/> 创建 MongoDB 分布式存储。
    /// </summary>
    /// <param name="client">MongoDB 客户端（调用方负责生命周期）。</param>
    /// <param name="databaseName">数据库名，默认为 "agentscope"。</param>
    /// <param name="collectionName">集合名，默认为 "agentscope_kv"。</param>
    public MongoDistributedStore(MongoClient client, string? databaseName = null, string? collectionName = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = false;
        var db = client.GetDatabase(databaseName ?? MongoConstants.DefaultDatabase);
        var collection = db.GetCollection<BsonDocument>(collectionName ?? MongoConstants.KvStoreCollection);
        _baseStore = new MongoBaseStore(collection);
    }

    /// <summary>
    /// 通过连接字符串创建 MongoDB 分布式存储。
    /// 内部会创建并拥有 <see cref="MongoClient"/>，<see cref="DisposeAsync"/> 时会自动关闭。
    /// </summary>
    /// <param name="connectionString">MongoDB 连接字符串（如 "mongodb://localhost:27017"）。</param>
    /// <param name="databaseName">数据库名，默认为 "agentscope"。</param>
    /// <param name="collectionName">集合名，默认为 "agentscope_kv"。</param>
    public MongoDistributedStore(string connectionString, string? databaseName = null, string? collectionName = null)
        : this(new MongoClient(connectionString), databaseName, collectionName)
    {
        _ownsClient = true;
    }

    /// <inheritdoc />
    public ValueTask<string?> GetAsync(string key, CancellationToken ct = default)
        => _baseStore.GetAsync(key, ct);

    /// <inheritdoc />
    public ValueTask SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default)
        => _baseStore.SetAsync(key, value, ttl, ct);

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(string key, CancellationToken ct = default)
        => _baseStore.DeleteAsync(key, ct);

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListKeysAsync(string prefix, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var key in _baseStore.ListKeysAsync(prefix, ct).ConfigureAwait(false))
            yield return key;
    }

    /// <summary>
    /// 释放资源。如果实例拥有 <see cref="MongoClient"/>，则关闭之（幂等）。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsClient && _client is IDisposable disposable)
            disposable.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsClient && _client == null) return;

        // MongoDB.Driver 2.x 的 MongoClient 只实现 IDisposable，不实现 IAsyncDisposable，
        // 因此需要回落到同步 Dispose，避免连接泄漏。
        if (_client is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (_client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
