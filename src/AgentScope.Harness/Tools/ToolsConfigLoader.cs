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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentScope.Harness.Tools;

public static class ToolsConfigLoader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// 从 JSON 文件加载工具配置（含 mcpServers）。
    /// </summary>
    public static async Task<ToolsConfig> LoadAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return new ToolsConfig();
        var json = await File.ReadAllTextAsync(path, ct);

        // 尝试直接反序列化
        var config = JsonSerializer.Deserialize<ToolsConfig>(json, JsonOpts) ?? new ToolsConfig();

        // 尝试扩展解析：提取顶级 mcpServers 对象（标准 MCP 配置格式）
        // mcpServers 是对象，每个 key 是一个 server 名
        if (config.McpServers.Count == 0)
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("mcpServers", out var serversEl) ||
                doc.RootElement.TryGetProperty("mcp_servers", out serversEl))
            {
                foreach (var serverProp in serversEl.EnumerateObject())
                {
                    var name = serverProp.Name;
                    var svr = serverProp.Value;
                    var transport = svr.TryGetProperty("transport", out var t) ? t.GetString() ?? "stdio" : "stdio";
                    var command = svr.TryGetProperty("command", out var c) ? c.GetString() : null;
                    var url = svr.TryGetProperty("url", out var u) ? u.GetString() : null;

                    string[]? args = null;
                    if (svr.TryGetProperty("args", out var argsEl))
                    {
                        args = argsEl.EnumerateArray().Select(a => a.GetString() ?? "").ToArray();
                    }

                    Dictionary<string, string>? env = null;
                    if (svr.TryGetProperty("env", out var envEl))
                    {
                        env = new Dictionary<string, string>();
                        foreach (var e in envEl.EnumerateObject())
                            env[e.Name] = e.Value.GetString() ?? "";
                    }

                    config.McpServers.Add(new McpServerConfig(name, transport, command, args, url, env));
                }
            }
        }

        return config;
    }
}
