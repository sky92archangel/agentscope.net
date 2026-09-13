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

using MongoDB.Bson;
using MongoDB.Driver;

namespace AgentScope.Extensions.Store.MongoDB;

/// <summary>
/// MongoDB 基础 KV 存储封装，围绕 <see cref="IMongoCollection{BsonDocument}"/> 提供简单的
/// 字符串键值读写、删除与前缀列举能力。
/// 对应 Java: io.agentscope.extensions.mongodb.store.MongoBaseStore（最小可用版本，仅含 KV 语义）。
/// </summary>
/// <remarks>
/// 每个 KV 条目在 MongoDB 中存储为一个文档：
/// <c>{ _id: "&lt;key&gt;", value: "&lt;string&gt;" }</c>
/// TTL 参数在本实现中被忽略——MongoDB 的单文档 TTL 需要通过 TTL 索引或应用层实现。
/// 如需 TTL 支持，请在集合上创建 MongoDB TTL 索引，或使用 <see cref="MongoDistributedStore"/>。
/// </remarks>
public sealed class MongoBaseStore
{
    /// <summary>
    /// MongoDB 集合引用。
    /// </summary>
    private readonly IMongoCollection<BsonDocument> _collection;

    /// <summary>
    /// 使用指定的 MongoDB 集合初始化 <see cref="MongoBaseStore"/>。
    /// </summary>
    /// <param name="collection">MongoDB 集合实例。</param>
    public MongoBaseStore(IMongoCollection<BsonDocument> collection)
    {
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
    }

    /// <summary>
    /// 获取指定键对应的字符串值。
    /// </summary>
    /// <param name="key">键。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>键存在时返回字符串值，否则返回 null。</returns>
    public async ValueTask<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", key);
        var doc = await _collection.Find(filter).Project(Builders<BsonDocument>.Projection.Include("value")).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (doc == null || !doc.TryGetValue("value", out var value))
            return null;
        return value.IsBsonNull ? null : value.AsString;
    }

    /// <summary>
    /// 设置指定键的字符串值。
    /// </summary>
    /// <param name="key">键。</param>
    /// <param name="value">要存储的值。</param>
    /// <param name="ttl">可选的生存时间（本实现中忽略）。</param>
    /// <param name="ct">取消令牌。</param>
    public async ValueTask SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", key);
        var update = Builders<BsonDocument>.Update.Set("value", value);
        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 删除指定键，返回是否成功删除。
    /// </summary>
    /// <param name="key">键。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>键存在且被删除时返回 true，否则返回 false。</returns>
    public async ValueTask<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", key);
        var result = await _collection.DeleteOneAsync(filter, ct).ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    /// <summary>
    /// 列举所有匹配指定前缀的键。
    /// </summary>
    /// <param name="prefix">键前缀。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>键的异步可枚举序列。</returns>
    public async IAsyncEnumerable<string> ListKeysAsync(string prefix, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var filter = Builders<BsonDocument>.Filter.Regex("_id", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(prefix)}"));
        using var cursor = await _collection.FindAsync(filter, cancellationToken: ct).ConfigureAwait(false);
        while (await cursor.MoveNextAsync(ct).ConfigureAwait(false))
        {
            foreach (var doc in cursor.Current)
            {
                ct.ThrowIfCancellationRequested();
                yield return doc["_id"].AsString;
            }
        }
    }
}
