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
using AgentScope.Core.Tool;

namespace AgentScope.Harness.Tool;

/// <summary>
/// 异步结果等待工具，让 Agent 可以等待一个或多个异步操作完成。
/// 对标 Java WaitAsyncResultsTool。
/// </summary>
public sealed class WaitAsyncResultsTool : ITool
{
    /// <summary>最大超时秒数（硬上限 120s）。</summary>
    private const int MaxTimeoutSeconds = 120;

    /// <summary>连续空结果熔断阈值。</summary>
    private const int MaxEmptyPolls = 3;

    private static readonly ConcurrentDictionary<string, TaskCompletionSource<string>> PendingResults = new();

    /// <summary>连续空等待计数器（按 Agent/会话隔离）。</summary>
    private static readonly ConcurrentDictionary<string, int> EmptyPollCounters = new();

    public string Name => "wait_async_results";
    public string Description => "等待一个或多个异步操作完成并获取结果";
    public bool IsExternal => false;

    /// <summary>
    /// 注册一个待等待的异步操作标识。
    /// </summary>
    public static string RegisterPending()
    {
        var token = Guid.NewGuid().ToString("N");
        PendingResults[token] = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        return token;
    }

    /// <summary>
    /// 完成一个异步操作并将结果写入指定 token。
    /// </summary>
    public static bool Complete(string token, string result)
    {
        if (PendingResults.TryRemove(token, out var tcs))
        {
            return tcs.TrySetResult(result);
        }
        return false;
    }

    /// <summary>
    /// 取消一个待等待的异步操作。
    /// </summary>
    public static bool Cancel(string token)
    {
        if (PendingResults.TryRemove(token, out var tcs))
        {
            return tcs.TrySetCanceled();
        }
        return false;
    }

    /// <summary>
    /// 重置连续空等待计数器（通常在新回合开始时调用）。
    /// </summary>
    public static void ResetEmptyPollCounter(string agentId) =>
        EmptyPollCounters.TryRemove(agentId, out _);

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        // ── 解析 task_ids ──
        var taskIds = new List<string>();
        if (parameters.TryGetValue("task_ids", out var idsObj) && idsObj != null)
        {
            if (idsObj is System.Collections.IList list)
            {
                foreach (var item in list)
                    if (item?.ToString() is { } s && !string.IsNullOrWhiteSpace(s))
                        taskIds.Add(s);
            }
            else
            {
                // 单个字符串用逗号分割
                var str = idsObj.ToString();
                if (!string.IsNullOrWhiteSpace(str))
                    taskIds.AddRange(str.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        // 兼容旧版单 token 参数
        if (taskIds.Count == 0)
        {
            var token = parameters.GetValueOrDefault("token")?.ToString();
            if (!string.IsNullOrWhiteSpace(token))
                taskIds.Add(token);
        }

        if (taskIds.Count == 0)
            return ToolResult.Fail("需要 task_ids 参数（逗号分隔或列表）");

        // ── 解析 wait_all ──
        var waitAll = true;
        if (parameters.TryGetValue("wait_all", out var waObj))
        {
            if (waObj is bool b) waitAll = b;
            else bool.TryParse(waObj?.ToString(), out waitAll);
        }

        // ── 解析 timeout（120s 硬上限） ──
        var timeoutSeconds = 30;
        if (parameters.TryGetValue("timeout", out var toObj) &&
            int.TryParse(toObj?.ToString(), out var parsed))
        {
            timeoutSeconds = Math.Min(parsed, MaxTimeoutSeconds);
        }
        else
        {
            timeoutSeconds = MaxTimeoutSeconds;
        }

        // ── 解析 agent_id（用于熔断计数器） ──
        var agentId = parameters.GetValueOrDefault("agent_id")?.ToString() ?? "default";

        try
        {
            // ── barrier 模式：等待多个 task_ids ──
            if (waitAll)
            {
                var tasks = new List<Task<string>>();
                var remainingTokens = new List<string>();

                foreach (var id in taskIds)
                {
                    if (PendingResults.TryGetValue(id, out var tcs))
                    {
                        remainingTokens.Add(id);
                        tasks.Add(tcs.Task);
                    }
                    else
                    {
                        // token 已被消费或不存在，视为已完成（返回空结果）
                        tasks.Add(Task.FromResult(""));
                    }
                }

                if (tasks.Count == 0)
                {
                    var counter = EmptyPollCounters.AddOrUpdate(agentId, 1, (_, c) => c + 1);
                    if (counter >= MaxEmptyPolls)
                    {
                        EmptyPollCounters.TryRemove(agentId, out _);
                        return ToolResult.Fail($"连续 {MaxEmptyPolls} 次空结果，自动中止等待");
                    }
                    return ToolResult.Fail("没有待等待的 task_ids（可能均已处理完毕）");
                }

                // 重置空计数器——有实际任务
                EmptyPollCounters.TryRemove(agentId, out _);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var timeoutTask = Task.Delay(-1, cts.Token);

                // 等待所有完成或超时
                var allTask = Task.WhenAll(tasks);
                var completed = await Task.WhenAny(allTask, timeoutTask).ConfigureAwait(false);

                if (completed == timeoutTask)
                {
                    // 超时：取消仍在等待的 token，返回已完成的结果
                    foreach (var id in remainingTokens)
                        Cancel(id);
                    return ToolResult.Fail($"等待超时（{timeoutSeconds}s），部分 task_ids 未完成");
                }

                var results = await allTask.ConfigureAwait(false);
                var resultDict = new Dictionary<string, object>();
                for (int i = 0; i < taskIds.Count; i++)
                    resultDict[taskIds[i]] = i < results.Length ? results[i] : "";

                return ToolResult.Ok(resultDict);
            }
            else
            {
                // ── wait_all=false：任一完成即返回 ──
                var pendingTasks = new Dictionary<string, Task<string>>();
                foreach (var id in taskIds)
                {
                    if (PendingResults.TryGetValue(id, out var tcs))
                        pendingTasks[id] = tcs.Task;
                }

                if (pendingTasks.Count == 0)
                {
                    var counter = EmptyPollCounters.AddOrUpdate(agentId, 1, (_, c) => c + 1);
                    if (counter >= MaxEmptyPolls)
                    {
                        EmptyPollCounters.TryRemove(agentId, out _);
                        return ToolResult.Fail($"连续 {MaxEmptyPolls} 次空结果，自动中止等待");
                    }
                    return ToolResult.Fail("没有待等待的 task_ids");
                }

                EmptyPollCounters.TryRemove(agentId, out _);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var taskArray = pendingTasks.Values.ToArray();
                var completedTask = await Task.WhenAny(taskArray).WaitAsync(cts.Token).ConfigureAwait(false);

                var completedId = pendingTasks.First(kv => kv.Value == completedTask).Key;
                var result = await completedTask.ConfigureAwait(false);
                PendingResults.TryRemove(completedId, out _);

                return ToolResult.Ok(new Dictionary<string, object>
                {
                    [completedId] = result
                });
            }
        }
        catch (TimeoutException)
        {
            return ToolResult.Fail($"等待超时（{timeoutSeconds}s）");
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Fail("等待被取消");
        }
    }

    public Dictionary<string, object> GetSchema() => new()
    {
        ["name"] = Name,
        ["description"] = Description,
        ["parameters"] = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["task_ids"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["description"] = "要等待的 task_id 列表"
                },
                ["wait_all"] = new Dictionary<string, object>
                {
                    ["type"] = "boolean",
                    ["description"] = "是否等待所有 task_ids 完成（默认 true；false=任一个完成即返回）"
                },
                ["timeout"] = new Dictionary<string, object>
                {
                    ["type"] = "integer",
                    ["description"] = "超时秒数（默认 120，硬上限 120）"
                }
            },
            ["required"] = new[] { "task_ids" }
        }
    };
}
