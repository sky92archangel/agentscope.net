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
using System.Text;
using System.Text.Json;

namespace AgentScope.Harness.Tools;

/// <summary>
/// MCP 服务器注册器。对标 Java McpServerRegistrar。
/// 根据配置创建 MCP 客户端并连接（stdio / SSE / HTTP）。
/// </summary>
public sealed class McpServerRegistrar
{
    private readonly List<McpServerConfig> _servers = new();
    private readonly ConcurrentDictionary<string, McpClientEntry> _connections = new();

    /// <summary>已注册的服务器配置。</summary>
    public IReadOnlyList<McpServerConfig> Registered => _servers;
    public void Clear() { _servers.Clear(); _connections.Clear(); }

    public void Register(McpServerConfig config) => _servers.Add(config);

    /// <summary>
    /// 连接所有已注册的 MCP 服务器。
    /// 返回 (successNames, failNames)。
    /// </summary>
    public async Task<(List<string> Success, List<string> Fail)> ConnectAllAsync(
        CancellationToken ct = default)
    {
        var success = new List<string>();
        var fail = new List<string>();

        foreach (var cfg in _servers)
        {
            try
            {
                var client = cfg.Transport switch
                {
                    "stdio" => await ConnectStdioAsync(cfg, ct),
                    "sse" => await ConnectSseAsync(cfg, ct),
                    "http" => await ConnectHttpAsync(cfg, ct),
                    _ => throw new NotSupportedException($"不支持的 transport: {cfg.Transport}")
                };
                _connections[cfg.Name] = client;
                success.Add(cfg.Name);
            }
            catch (Exception ex)
            {
                fail.Add($"{cfg.Name}: {ex.Message}");
            }
        }

        return (success, fail);
    }

    /// <summary>
    /// 断开所有连接并清理。
    /// </summary>
    public async Task DisconnectAllAsync()
    {
        foreach (var kv in _connections)
        {
            try { await kv.Value.Client.DisposeAsync(); }
            catch { }
        }
        _connections.Clear();
    }

    /// <summary>
    /// 通过 Name 获取连接的 MCP 客户端，未找到返回 null。
    /// </summary>
    public IMcpClient? GetClient(string name) =>
        _connections.TryGetValue(name, out var entry) ? entry.Client : null;

    // ── Stdio 连接：启动子进程并通过 stdin/stdout JSON-RPC 通信 ──
    private static async Task<McpClientEntry> ConnectStdioAsync(
        McpServerConfig cfg, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cfg.Command))
            throw new InvalidOperationException("stdio transport 必须提供 Command");

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = cfg.Command,
            Arguments = cfg.Args != null ? string.Join(" ", cfg.Args) : "",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (cfg.Env != null)
            foreach (var kv in cfg.Env)
                psi.Environment[kv.Key] = kv.Value;

        var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();

        // 发送 initialize 请求并等待响应
        var client = new StdioMcpClient(process);
        await client.InitializeAsync(ct);
        return new McpClientEntry(client, process);
    }

    // ── SSE 连接：通过 Server-Sent Events 流式通信 ──
    private static async Task<McpClientEntry> ConnectSseAsync(
        McpServerConfig cfg, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cfg.Url))
            throw new InvalidOperationException("sse transport 必须提供 Url");

        var httpClient = new HttpClient();
        var client = new SseMcpClient(httpClient, cfg.Url);
        await client.InitializeAsync(ct);
        return new McpClientEntry(client, null);
    }

    // ── HTTP 连接：直接 HTTP JSON-RPC ──
    private static async Task<McpClientEntry> ConnectHttpAsync(
        McpServerConfig cfg, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cfg.Url))
            throw new InvalidOperationException("http transport 必须提供 Url");

        var httpClient = new HttpClient();
        var client = new HttpMcpClient(httpClient, cfg.Url);
        await client.InitializeAsync(ct);
        return new McpClientEntry(client, null);
    }

    private sealed record McpClientEntry(IMcpClient Client, System.Diagnostics.Process? Process);
}

// ── MCP 客户端接口 ──

/// <summary>
/// 最小化 MCP 客户端接口。
/// </summary>
public interface IMcpClient
{
    /// <summary>发送 ListTools 请求，返回工具名列表。</summary>
    Task<List<string>> ListToolsAsync(CancellationToken ct = default);
    /// <summary>调用指定工具。</summary>
    Task<JsonElement> CallToolAsync(string toolName, Dictionary<string, object> args,
        CancellationToken ct = default);
    /// <summary>释放资源。</summary>
    ValueTask DisposeAsync();
}

// ── 内部 JSON-RPC 消息 ──

internal sealed record JsonRpcRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("jsonrpc")]
    string JsonRpc = "2.0",
    [property: System.Text.Json.Serialization.JsonPropertyName("id")]
    string? Id = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("method")]
    string? Method = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("params")]
    JsonElement? Params = null
);

internal sealed record JsonRpcResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("jsonrpc")]
    string JsonRpc = "2.0",
    [property: System.Text.Json.Serialization.JsonPropertyName("id")]
    string? Id = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("result")]
    JsonElement? Result = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("error")]
    JsonRpcError? Error = null
);

internal sealed record JsonRpcError(
    [property: System.Text.Json.Serialization.JsonPropertyName("code")]
    int Code,
    [property: System.Text.Json.Serialization.JsonPropertyName("message")]
    string Message
);

// ── Stdio MCP 客户端 ──

internal sealed class StdioMcpClient : IMcpClient, IAsyncDisposable
{
    private readonly System.Diagnostics.Process _process;
    private int _msgId;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public StdioMcpClient(System.Diagnostics.Process process) => _process = process;

    public async Task InitializeAsync(CancellationToken ct)
    {
        var req = new JsonRpcRequest(Id: "1", Method: "initialize",
            Params: JsonSerializer.SerializeToElement(
                new { protocolVersion = "2024-11-05", capabilities = new { } }, JsonOpts));
        await SendAsync(req, ct);
    }

    public async Task<List<string>> ListToolsAsync(CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _msgId).ToString();
        var req = new JsonRpcRequest(Id: id, Method: "tools/list");
        var resp = await SendAsync(req, ct);
        // 从 result.tools 数组中提取 name
        if (resp.Result is null) return new List<string>();
        var tools = new List<string>();
        if (resp.Result.Value.TryGetProperty("tools", out var toolsEl))
        {
            foreach (var t in toolsEl.EnumerateArray())
            {
                if (t.TryGetProperty("name", out var nameEl))
                    tools.Add(nameEl.GetString() ?? "");
            }
        }
        return tools;
    }

    public async Task<JsonElement> CallToolAsync(string toolName,
        Dictionary<string, object> args, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _msgId).ToString();
        var paramsEl = JsonSerializer.SerializeToElement(
            new { name = toolName, arguments = args }, JsonOpts);
        var req = new JsonRpcRequest(Id: id, Method: "tools/call", Params: paramsEl);
        var resp = await SendAsync(req, ct);
        if (resp.Error is not null)
            throw new InvalidOperationException(
                $"MCP 工具调用错误 [{resp.Error.Code}]: {resp.Error.Message}");
        return resp.Result ?? JsonSerializer.SerializeToElement("ok");
    }

    public ValueTask DisposeAsync()
    {
        try { if (!_process.HasExited) _process.Kill(); } catch { }
        _process.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<JsonRpcResponse> SendAsync(JsonRpcRequest req, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(req, JsonOpts);
        await _process.StandardInput.WriteLineAsync(json.AsMemory(), ct);
        await _process.StandardInput.FlushAsync(ct);
        var line = await _process.StandardOutput.ReadLineAsync(ct) ?? "{}";
        return JsonSerializer.Deserialize<JsonRpcResponse>(line, JsonOpts)
               ?? new JsonRpcResponse();
    }
}

// ── SSE MCP 客户端 ──

internal sealed class SseMcpClient : IMcpClient
{
    private readonly HttpClient _http;
    private readonly string _url;
    private int _msgId;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public SseMcpClient(HttpClient http, string url) { _http = http; _url = url; }

    public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task<List<string>> ListToolsAsync(CancellationToken ct = default)
    {
        var resp = await PostAsync("tools/list", null, ct);
        var tools = new List<string>();
        if (resp.TryGetProperty("tools", out var toolsEl))
            foreach (var t in toolsEl.EnumerateArray())
                if (t.TryGetProperty("name", out var n))
                    tools.Add(n.GetString() ?? "");
        return tools;
    }

    public async Task<JsonElement> CallToolAsync(string toolName,
        Dictionary<string, object> args, CancellationToken ct = default)
    {
        var resp = await PostAsync("tools/call",
            JsonSerializer.SerializeToElement(new { name = toolName, arguments = args },
                JsonOpts), ct);
        if (resp.TryGetProperty("error", out var errEl))
            throw new InvalidOperationException(
                $"MCP 工具调用错误: {errEl.GetProperty("message").GetString()}");
        return resp.TryGetProperty("result", out var r) ? r : resp;
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<JsonElement> PostAsync(string method, JsonElement? paramsEl,
        CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _msgId).ToString();
        var req = new JsonRpcRequest(Id: id, Method: method, Params: paramsEl);
        var json = JsonSerializer.Serialize(req, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var httpResp = await _http.PostAsync(_url, content, ct);
        httpResp.EnsureSuccessStatusCode();
        var body = await httpResp.Content.ReadAsStringAsync(ct);
        var rpcResp = JsonSerializer.Deserialize<JsonRpcResponse>(body, JsonOpts);
        if (rpcResp?.Error is not null)
            throw new InvalidOperationException(
                $"MCP 错误 [{rpcResp.Error.Code}]: {rpcResp.Error.Message}");
        return rpcResp?.Result ?? JsonSerializer.SerializeToElement("ok");
    }
}

// ── HTTP MCP 客户端 ──

internal sealed class HttpMcpClient : IMcpClient
{
    private readonly HttpClient _http;
    private readonly string _url;
    private int _msgId;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public HttpMcpClient(HttpClient http, string url) { _http = http; _url = url; }

    public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task<List<string>> ListToolsAsync(CancellationToken ct = default)
    {
        var resp = await PostAsync("tools/list", null, ct);
        var tools = new List<string>();
        if (resp.TryGetProperty("tools", out var toolsEl))
            foreach (var t in toolsEl.EnumerateArray())
                if (t.TryGetProperty("name", out var n))
                    tools.Add(n.GetString() ?? "");
        return tools;
    }

    public async Task<JsonElement> CallToolAsync(string toolName,
        Dictionary<string, object> args, CancellationToken ct = default)
    {
        var resp = await PostAsync("tools/call",
            JsonSerializer.SerializeToElement(new { name = toolName, arguments = args },
                JsonOpts), ct);
        if (resp.TryGetProperty("error", out var errEl))
            throw new InvalidOperationException(
                $"MCP 工具调用错误: {errEl.GetProperty("message").GetString()}");
        return resp.TryGetProperty("result", out var r) ? r : resp;
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<JsonElement> PostAsync(string method, JsonElement? paramsEl,
        CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _msgId).ToString();
        var req = new JsonRpcRequest(Id: id, Method: method, Params: paramsEl);
        var json = JsonSerializer.Serialize(req, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var httpResp = await _http.PostAsync(_url, content, ct);
        httpResp.EnsureSuccessStatusCode();
        var body = await httpResp.Content.ReadAsStringAsync(ct);
        var rpcResp = JsonSerializer.Deserialize<JsonRpcResponse>(body, JsonOpts);
        if (rpcResp?.Error is not null)
            throw new InvalidOperationException(
                $"MCP 错误 [{rpcResp.Error.Code}]: {rpcResp.Error.Message}");
        return rpcResp?.Result ?? JsonSerializer.SerializeToElement("ok");
    }
}
