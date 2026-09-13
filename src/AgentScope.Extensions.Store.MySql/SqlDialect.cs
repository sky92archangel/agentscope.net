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

namespace AgentScope.Extensions.Store.MySql;

/// <summary>
/// SQL 方言抽象。每种数据库实现自己的 SQL 模板。
/// 对应 Java: io.agentscope.extension.jdbc.dialect.SqlDialect
/// </summary>
public abstract class SqlDialect
{
    /// <summary>生成 CREATE TABLE 语句。</summary>
    public abstract string GetCreateTableSql(string tableName, string keyColumn, string valueColumn, string? versionColumn);

    /// <summary>生成 UPSERT 语句（无 TTL）。</summary>
    public abstract string GetUpsertSql(string tableName, string keyColumn, string valueColumn, string? versionColumn);

    /// <summary>生成 UPSERT 语句（含 expires_at TTL 字段）。</summary>
    public abstract string GetUpsertWithTtlSql(string tableName, string keyColumn, string valueColumn, string? versionColumn);

    /// <summary>生成根据 key 查询单行 value 的 SELECT 语句（含过期判断）。</summary>
    public abstract string GetSelectSql(string tableName, string keyColumn);

    /// <summary>生成根据 key 删除单行的 DELETE 语句。</summary>
    public abstract string GetDeleteSql(string tableName, string keyColumn);

    /// <summary>生成按前缀扫描 key 的 SELECT 语句。</summary>
    public abstract string GetListKeysSql(string tableName, string keyColumn);

    /// <summary>默认 MySQL 方言。</summary>
    public static SqlDialect MySql => new MySqlDialect();

    /// <summary>PostgreSQL 方言。</summary>
    public static SqlDialect PostgreSql => new PostgreSqlDialect();

    /// <summary>SQLite 方言。</summary>
    public static SqlDialect Sqlite => new SqliteDialect();
}
