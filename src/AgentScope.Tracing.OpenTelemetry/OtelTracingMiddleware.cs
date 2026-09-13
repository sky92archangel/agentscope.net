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

using System.Diagnostics;
using AgentScope.Core.Model;
using AgentScope.Core.Tool;
using AgentScope.Harness.Middleware;

namespace AgentScope.Tracing.OpenTelemetry;

/// <summary>
/// OpenTelemetry 追踪中间件。对标 Java OtelTracingMiddleware（345 行）。
/// 使用 ActivitySource/Activity（C# 惯用）替代 Java 的 GlobalOpenTelemetry 全局单例。
/// Activity 通过 AsyncLocal 自动跨 async 传播，无需 Reactor context 全局钩子。
///
/// 功能：
/// - invoke_agent span：Agent 调用追踪
/// - chat span：模型调用追踪 + gen_ai.usage.* + reply_id
/// - execute_tool span：工具执行追踪（每个 ToolUseBlock 独立子 span）
/// </summary>
public sealed class OtelTracingMiddleware : IHarnessMiddleware
{
    /// <summary>
    /// ActivitySource 实例，用于创建 "io.agentscope" 来源的追踪 span
    /// </summary>
    private static readonly ActivitySource Source = new("io.agentscope");

    /// <summary>
    /// 中间件执行顺序（0 为最高优先级）
    /// </summary>
    public int Order => 0;

    /// <summary>
    /// Agent 调用 span：记录调用起止、Agent 名称；从上下文读取 reply_id 写入 tag。
    /// </summary>
    public async ValueTask OnAgentAsync(MiddlewareContext ctx,
        Func<ValueTask> next, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity($"invoke_agent {ctx.AgentName}", ActivityKind.Internal);
        activity?.SetTag(GenAiAttributes.OperationName, "invoke_agent");
        activity?.SetTag(GenAiAttributes.AgentName, ctx.AgentName);
        try { await next(); }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.message", ex.Message);
            throw;
        }

        // 回合结束后读取 reply_id
        if (ctx.Items.TryGetValue("reply_id", out var replyId) && replyId is string ridStr)
        {
            activity?.SetTag(GenAiAttributes.ReplyId, ridStr);
        }
    }

    /// <summary>
    /// 模型调用 span：记录模型名称 + gen_ai.usage.* 用量 + 响应元数据。
    /// 数据来源：ctx.Items["chat_usage"]（ChatUsage）、ctx.Items["reply_id"] 等。
    /// </summary>
    public async ValueTask OnModelCallAsync(MiddlewareContext ctx,
        Func<ValueTask> next, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity($"chat {ctx.Model}", ActivityKind.Internal);
        activity?.SetTag(GenAiAttributes.OperationName, "chat");
        activity?.SetTag(GenAiAttributes.RequestModel, ctx.Model);
        await next();

        // ── 写入回复 ID ──
        if (ctx.Items.TryGetValue("reply_id", out var replyId) && replyId is string ridStr)
        {
            activity?.SetTag(GenAiAttributes.ReplyId, ridStr);
        }

        // ── 写入响应 ID ──
        if (ctx.Items.TryGetValue("response_id", out var respId) && respId is string respIdStr)
        {
            activity?.SetTag(GenAiAttributes.ResponseId, respIdStr);
        }

        // ── 写入响应模型名 ──
        if (ctx.Items.TryGetValue("response_model", out var respModel) && respModel is string respModelStr)
        {
            activity?.SetTag(GenAiAttributes.ResponseModel, respModelStr);
        }

        // ── 写入 token 用量（ChatUsage）──
        if (ctx.Items.TryGetValue("chat_usage", out var usageObj) && usageObj is ChatUsage usage)
        {
            activity?.SetTag(GenAiAttributes.UsageInputTokens, usage.InputTokens);
            activity?.SetTag(GenAiAttributes.UsageOutputTokens, usage.OutputTokens);
            if (usage.CachedInputTokens.HasValue)
                activity?.SetTag(GenAiAttributes.UsageCachedInputTokens, usage.CachedInputTokens.Value);
            if (usage.CacheCreationInputTokens.HasValue)
                activity?.SetTag(GenAiAttributes.UsageCacheCreationInputTokens, usage.CacheCreationInputTokens.Value);
        }
    }

    /// <summary>
    /// 工具执行 span：为每个 ToolUseBlock 创建独立 execute_tool 子 span，
    /// 记录 tool.call.id、tool.name、tool.call.arguments（JSON 序列化）及执行结果状态。
    /// </summary>
    public async ValueTask OnToolExecutionAsync(MiddlewareContext ctx,
        Func<ValueTask> next, CancellationToken ct = default)
    {
        // ── 为每个工具调用预创建 Activity（start 计时），收集到列表统一管理 ──
        var activities = new List<Activity>(ctx.ToolCalls.Count);
        try
        {
            foreach (var tc in ctx.ToolCalls)
            {
                var activity = Source.StartActivity("execute_tool", ActivityKind.Internal);
                if (activity == null) continue;

                activity.SetTag(GenAiAttributes.ToolCallId, tc.Id);
                activity.SetTag(GenAiAttributes.ToolName, tc.Name);
                if (tc.Input?.Count > 0)
                {
                    activity.SetTag(GenAiAttributes.ToolCallArguments,
                        System.Text.Json.JsonSerializer.Serialize(tc.Input));
                }
                activities.Add(activity);
            }

            await next();

            // ── after next：尝试读取工具执行结果写入 tag ──
            foreach (var activity in activities)
            {
                var toolId = activity.GetTagItem(GenAiAttributes.ToolCallId) as string;
                if (toolId != null && ctx.Items.TryGetValue($"tool_result:{toolId}", out var resultObj))
                {
                    if (resultObj is ToolResult toolResult)
                    {
                        activity.SetTag(GenAiAttributes.ToolCallResult,
                            toolResult.Success ? "success" : "error");
                        if (!string.IsNullOrEmpty(toolResult.Error))
                            activity.SetStatus(ActivityStatusCode.Error, toolResult.Error);
                    }
                    else if (resultObj is string resultStr)
                    {
                        activity.SetTag(GenAiAttributes.ToolCallResult, resultStr);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 未捕获的异常标记所有待处理 activity 为 error
            foreach (var activity in activities)
            {
                activity.SetTag(GenAiAttributes.ToolCallResult, "error");
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
            throw;
        }
        finally
        {
            // 统一释放所有 activity
            foreach (var activity in activities)
            {
                activity.Dispose();
            }
        }
    }
}
