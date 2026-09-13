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

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AgentScope.Extensions.Sandbox.AgentRun;

/// <summary>
/// AgentRun MCP 通信通道：通过 MCP 协议（JSON-RPC over WebSocket）与沙箱通信。
/// 对标 Java AgentRunMcpChannel。
/// </summary>
public sealed class AgentRunMcpChannel : IAsyncDisposable
{
    private ClientWebSocket? _ws;
    private readonly string _url;
    private readonly string? _authToken;

    /// <summary>
    /// 初始化 MCP 通道。
    /// </summary>
    /// <param name="url">WebSocket 端点地址。</param>
    /// <param name="authToken">可选的认证令牌。</param>
    public AgentRunMcpChannel(string url, string? authToken = null)
    {
        _url = url;
        _authToken = authToken;
    }

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public bool IsConnected => _ws?.State == WebSocketState.Open;

    /// <summary>
    /// 建立与 MCP 服务器的 WebSocket 连接。
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _ws?.Dispose();
        _ws = new ClientWebSocket();
        _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrEmpty(_authToken))
            _ws.Options.SetRequestHeader("Authorization", $"Bearer {_authToken}");

        await _ws.ConnectAsync(new Uri(_url), ct);
    }

    /// <summary>
    /// 通过 MCP JSON-RPC 协议发送方法调用并等待响应。
    /// </summary>
    /// <param name="method">方法名。</param>
    /// <param name="params">方法参数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>响应 JSON 元素。</returns>
    public async Task<JsonElement> CallAsync(string method, object? @params = null, CancellationToken ct = default)
    {
        if (_ws?.State != WebSocketState.Open)
            throw new InvalidOperationException("MCP channel is not connected.");

        var requestId = Guid.NewGuid().ToString();
        var request = new
        {
            jsonrpc = "2.0",
            id = requestId,
            method,
            @params,
        };

        var json = JsonSerializer.Serialize(request);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);

        var buffer = new byte[1024 * 64];
        var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
        var response = Encoding.UTF8.GetString(buffer, 0, result.Count);

        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var err))
            throw new InvalidOperationException($"MCP error: {err}");

        return root.TryGetProperty("result", out var res) ? res.Clone() : default;
    }

    /// <summary>
    /// 断开 WebSocket 连接。
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch
            {
                // ignore close errors
            }
        }

        _ws?.Dispose();
        _ws = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
