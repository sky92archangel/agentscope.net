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
/// PostgreSQL 方言。使用双引号引用标识符，ON CONFLICT 实现 UPSERT。
/// 对应 Java: io.agentscope.extension.jdbc.dialect.PostgreSqlDialect
/// </summary>
public sealed class PostgreSqlDialect : SqlDialect
{
    /// <inheritdoc/>
    public override string GetCreateTableSql(string tableName, string keyColumn,
        string valueColumn, string? versionColumn)
    {
        return $@"CREATE TABLE IF NOT EXISTS ""{tableName}"" (
    ""{keyColumn}"" VARCHAR(255) PRIMARY KEY,
    ""{valueColumn}"" TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP NULL
)";
    }

    /// <inheritdoc/>
    public override string GetUpsertSql(string tableName, string keyColumn,
        string valueColumn, string? versionColumn)
    {
        var t = QuoteTable(tableName);
        var k = QuoteColumn(keyColumn);
        var v = QuoteColumn(valueColumn);
        return $"INSERT INTO {t} ({k}, {v}) VALUES (@k, @v) ON CONFLICT ({k}) DO UPDATE SET {v} = EXCLUDED.{v}";
    }

    /// <inheritdoc/>
    public override string GetUpsertWithTtlSql(string tableName, string keyColumn,
        string valueColumn, string? versionColumn)
    {
        var t = QuoteTable(tableName);
        var k = QuoteColumn(keyColumn);
        var v = QuoteColumn(valueColumn);
        return $"INSERT INTO {t} ({k}, {v}, expires_at) VALUES (@k, @v, NOW() + INTERVAL '1 second' * @t) ON CONFLICT ({k}) DO UPDATE SET {v} = EXCLUDED.{v}, expires_at = EXCLUDED.expires_at";
    }

    /// <inheritdoc/>
    public override string GetSelectSql(string tableName, string keyColumn)
    {
        var t = QuoteTable(tableName);
        var k = QuoteColumn(keyColumn);
        var v = QuoteColumn("value");
        return $"SELECT {v} FROM {t} WHERE {k} = @k AND (expires_at IS NULL OR expires_at > NOW())";
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
