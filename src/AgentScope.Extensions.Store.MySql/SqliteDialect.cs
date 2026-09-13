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
/// SQLite 方言。使用 INSERT OR REPLACE 实现 UPSERT，datetime('now') 获取当前时间。
/// 对应 Java: io.agentscope.extension.jdbc.dialect.SqliteDialect
/// </summary>
public sealed class SqliteDialect : SqlDialect
{
    /// <inheritdoc/>
    public override string GetCreateTableSql(string tableName, string keyColumn,
        string valueColumn, string? versionColumn)
    {
        return $@"CREATE TABLE IF NOT EXISTS ""{tableName}"" (
    ""{keyColumn}"" TEXT PRIMARY KEY,
    ""{valueColumn}"" TEXT NOT NULL,
    created_at TEXT DEFAULT (datetime('now')),
    expires_at TEXT NULL
)";
    }

    /// <inheritdoc/>
    public override string GetUpsertSql(string tableName, string keyColumn,
        string valueColumn, string? versionColumn)
    {
        return $"INSERT OR REPLACE INTO \"{tableName}\" (\"{keyColumn}\", \"{valueColumn}\") VALUES (@k, @v)";
    }

    /// <inheritdoc/>
    public override string GetUpsertWithTtlSql(string tableName, string keyColumn,
        string valueColumn, string? versionColumn)
    {
        return $"INSERT OR REPLACE INTO \"{tableName}\" (\"{keyColumn}\", \"{valueColumn}\", expires_at) VALUES (@k, @v, datetime('now', '+@t seconds'))";
    }

    /// <inheritdoc/>
    public override string GetSelectSql(string tableName, string keyColumn)
    {
        var v = QuoteColumn("value");
        var t = QuoteTable(tableName);
        var k = QuoteColumn(keyColumn);
        return $"SELECT {v} FROM {t} WHERE {k} = @k AND (expires_at IS NULL OR expires_at > datetime('now'))";
    }

    /// <inheritdoc/>
    public override string GetDeleteSql(string tableName, string keyColumn)
    {
        var t = QuoteTable(tableName);
        var k = QuoteColumn(keyColumn);
        return $"DELETE FROM {t} WHERE {k} = @k";
    }

    /// <inheritdoc/>
    public override string GetListKeysSql(string tableName, string keyColumn)
    {
        var t = QuoteTable(tableName);
        var k = QuoteColumn(keyColumn);
        return $"SELECT {k} FROM {t} WHERE {k} LIKE @p";
    }

    private static string QuoteTable(string name) => $@"""{name}""";
    private static string QuoteColumn(string name) => $@"""{name}""";
}
