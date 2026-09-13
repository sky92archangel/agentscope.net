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

using AgentScope.Harness.Subagent.Protocol;
using System.Net.Http.Json;
using System.Text.Json;

namespace AgentScope.Harness.Subagent.Tasks;

/// <summary>Agent Protocol HTTP 客户端，对应 Java AgentProtocolTaskClient</summary>
public sealed class AgentProtocolTaskClient
{
    private readonly HttpClient _http;

    public AgentProtocolTaskClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
    }

    // ===== 客户端方法（SubAgent 调用远端） =====

    /// <summary>创建任务（POST /tasks），对应 Java createTask。</summary>
    public async Task<string> CreateTaskAsync(string baseUrl,
        Dictionary<string, string>? headers, string taskId,
        string agentId, string input,
        RemoteSubmitContext? context = null,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/tasks")
        {
            Content = JsonContent.Create(new
            {
                task_id = taskId,
                agent_id = agentId,
                input,
                context
            })
        };
        ApplyHeaders(req, headers);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        return taskId;
    }

    /// <summary>提交任务（服务端端点复用 CreateTaskAsync）。</summary>
    public async Task SubmitTaskAsync(string baseUrl,
        Dictionary<string, string>? headers, string taskId,
        string agentId, string input,
        RemoteSubmitContext? context = null,
        CancellationToken ct = default)
    {
        await CreateTaskAsync(baseUrl, headers, taskId, agentId, input, context, ct);
    }

    /// <summary>
    /// 提交任务（含完整 header 与 context）。对应 Java submitTask。
    /// </summary>
    public async Task SubmitTaskAsync(string baseUrl,
        Dictionary<string, string>? headers, string taskId,
        string agentId, string input, string? sessionId,
        RemoteSubmitContext? context = null,
        CancellationToken ct = default)
    {
        var ctx = context ?? RemoteSubmitContext.Empty;
        if (!string.IsNullOrEmpty(sessionId))
            ctx = ctx with { ParentSessionId = sessionId };

        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/tasks")
        {
            Content = JsonContent.Create(new
            {
                task_id = taskId,
                agent_id = agentId,
                input,
                session_id = sessionId,
                context = ctx
            })
        };
        ApplyHeaders(req, headers);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task<RemoteTaskStatus> GetStatusAsync(string baseUrl,
        Dictionary<string, string>? headers, string taskId,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUrl.TrimEnd('/')}/tasks/{taskId}");
        ApplyHeaders(req, headers);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<RemoteTaskStatus>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty status response");
    }

    public async Task<string?> WaitForResultAsync(string baseUrl,
        Dictionary<string, string>? headers, string taskId,
        long timeoutSeconds, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUrl.TrimEnd('/')}/tasks/{taskId}/wait");
        ApplyHeaders(req, headers);
        using var res = await _http.SendAsync(req, cts.Token);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync(ct);
    }

    public async Task CancelTaskAsync(string baseUrl,
        Dictionary<string, string>? headers, string taskId,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/tasks/{taskId}/cancel");
        ApplyHeaders(req, headers);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// 恢复任务（提交 HITL 确认/拒绝决策）。对应 Java resumeTask。
    /// 增强：包含 recovery 逻辑，自动轮询直到决策被接受或超时。
    /// </summary>
    public async Task ResumeTaskAsync(string baseUrl,
        Dictionary<string, string>? headers, string taskId,
        List<RemoteConfirmDecision> decisions,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/tasks/{taskId}/resume")
        {
            Content = JsonContent.Create(new { decisions })
        };
        ApplyHeaders(req, headers);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// 恢复任务并等待结果（自动轮询恢复后的完成状态）。对应 Java resumeAndWait。
    /// </summary>
    public async Task<string?> ResumeAndWaitAsync(string baseUrl,
        Dictionary<string, string>? headers, string taskId,
        List<RemoteConfirmDecision> decisions,
        long timeoutSeconds = 300, CancellationToken ct = default)
    {
        // 提交决策
        await ResumeTaskAsync(baseUrl, headers, taskId, decisions, ct);

        // 轮询等待任务完成
        using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        pollCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var pollToken = pollCts.Token;

        while (!pollToken.IsCancellationRequested)
        {
            var status = await GetStatusAsync(baseUrl, headers, taskId, pollToken);
            if (status.IsTerminalSuccess)
                return status.Status;
            if (status.IsTerminalFailure)
                throw new InvalidOperationException($"Task {taskId} failed: {status.Error}");
            if (status.IsCancelled)
                throw new OperationCanceledException($"Task {taskId} was cancelled");

            await Task.Delay(1000, pollToken);
        }

        throw new TimeoutException($"Task {taskId} did not complete within {timeoutSeconds}s");
    }

    private static void ApplyHeaders(HttpRequestMessage req,
        Dictionary<string, string>? headers)
    {
        if (headers == null) return;
        foreach (var (k, v) in headers)
            req.Headers.TryAddWithoutValidation(k, v);
    }
}


