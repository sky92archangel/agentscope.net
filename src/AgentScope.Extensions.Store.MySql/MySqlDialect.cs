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
/// MySQL 方言。使用反引号引用标识符，ENGINE=InnoDB，REPLACE INTO 实现 UPSERT。
/// 对应 Java: io.agentscope.extension.jdbc.dialect.MySqlDialect
/// </summary>
public sealed class MySqlDialect : SqlDialect
{
    /// <inheritdoc/>
    public override string GetCreateTableSql(string tableName, string keyColumn,
        string valueColumn, string? versionColumn)
    {
        return $@"CREATE TABLE IF NOT EXISTS `{tableName}` (
    `{keyColumn}` VARCHAR(255) PRIMARY KEY,
    `{valueColumn}` LONGTEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
    }

    /// <inheritdoc/>
    public override string GetUpsertSql(string tableName, string keyColumn,
        string valueColumn, string? versionColumn)
    {
        return $"REPLACE INTO `{tableName}` (`{keyColumn}`, `{valueColumn}`) VALUES (@k, @v)";
    }

    /// <inheritdoc/>
    public override string GetUpsertWithTtlSql(string tableName, string keyColumn,
        string valueColumn, string? versionColumn)
    {
        return $"REPLACE INTO `{tableName}` (`{keyColumn}`, `{valueColumn}`, `expires_at`) VALUES (@k, @v, DATE_ADD(NOW(), INTERVAL @t SECOND))";
    }

    /// <inheritdoc/>
    public override string GetSelectSql(string tableName, string keyColumn)
    {
        return $"SELECT `value` FROM `{tableName}` WHERE `{keyColumn}` = @k AND (expires_at IS NULL OR expires_at > NOW())";
    }

    /// <inheritdoc/>
    public override string GetDeleteSql(string tableName, string keyColumn)
    {
        return $"DELETE FROM `{tableName}` WHERE `{keyColumn}` = @k";
    }

    /// <inheritdoc/>
    public override string GetListKeysSql(string tableName, string keyColumn)
    {
        return $"SELECT `{keyColumn}` FROM `{tableName}` WHERE `{keyColumn}` LIKE @p";
    }
}
