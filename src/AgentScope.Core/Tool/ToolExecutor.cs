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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentScope.Core.Formatter;
using AgentScope.Core.Message;

namespace AgentScope.Core.Tool;

/// <summary>
/// 工具执行器：单工具/批量执行，支持并行/串行控制、外部工具短路、超时和重试。
///
/// 调度管线:
///   单工具: 重试 for 循环 → WaitAsync 超时 → ICancellableTool 令牌
///   批量: 串行 Flux.concat → 并行 concurrencySafe 分区
///   外部工具: IsExternal=true → 直接返回 suspended
///
/// 对标: Java io.agentscope.core.tool.ToolExecutor
/// </summary>
public class ToolExecutor
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _retryDelay;
    private readonly Func<System.Exception, int, bool>? _shouldRetry;

    /// <summary>
    /// 创建工具执行器。
    /// </summary>
    /// <param name="maxAttempts">最大尝试次数（含首次），默认 1（不重试）。</param>
    /// <param name="timeout">单次执行超时；null 表示不强制超时。</param>
    /// <param name="retryDelay">重试间隔，默认 0。</param>
    /// <param name="shouldRetry">判定某异常是否应重试的回调；为 null 则对所有异常重试。</param>
    public ToolExecutor(
        int maxAttempts = 1,
        TimeSpan? timeout = null,
        TimeSpan? retryDelay = null,
        Func<System.Exception, int, bool>? shouldRetry = null)
    {
        if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        _maxAttempts = maxAttempts;
        _timeout = timeout ?? Timeout.InfiniteTimeSpan;
        _retryDelay = retryDelay ?? TimeSpan.Zero;
        _shouldRetry = shouldRetry;
    }

    // ==================== 单工具执行 ====================

    /// <summary>
    /// 执行单个工具，按配置应用重试/超时。
    /// </summary>
    public async Task<ToolResult> ExecuteAsync(
        ITool tool,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        if (tool == null) throw new ArgumentNullException(nameof(tool));

        // 外部工具短路
        if (tool.IsExternal)
            return ToolResult.Suspended(tool.Name);

        System.Exception? lastError = null;

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (_timeout != Timeout.InfiniteTimeSpan)
                    linkedCts.CancelAfter(_timeout);

                if (tool is ICancellableTool cancellable)
                {
                    return await cancellable.ExecuteAsync(
                        parameters ?? new Dictionary<string, object>(), linkedCts.Token)
                        .ConfigureAwait(false);
                }

                return await tool.ExecuteAsync(parameters ?? new Dictionary<string, object>())
                    .WaitAsync(linkedCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return ToolResult.Fail($"工具 {tool.Name} 执行超时（{_timeout}）。");
            }
            catch (ToolSuspendException)
            {
                return ToolResult.Suspended(tool.Name);
            }
            catch (System.Exception ex)
            {
                lastError = ex;
                var retry = _shouldRetry == null || _shouldRetry(ex, attempt);
                if (!retry || attempt >= _maxAttempts)
                    break;

                if (_retryDelay > TimeSpan.Zero)
                    await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return ToolResult.Fail(lastError?.Message ?? "工具执行失败。");
    }

    // ==================== 批量执行 ====================

    /// <summary>
    /// 批量执行工具调用。
    ///
    /// 串行模式: 按声明顺序执行。
    /// 并行模式: 根据 concurrencySafe 分区:
    ///   - 安全工具批量并发执行 (保持输出顺序)
    ///   - 不安全工具各自独立串行
    /// 外部工具: 直接返回 suspended 状态。
    /// </summary>
    public async Task<List<ToolResultBlock>> ExecuteAllAsync(
        Toolkit toolkit,
        IReadOnlyList<ToolUseBlock> toolCalls,
        bool parallel,
        ExecutionConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        if (toolkit == null) throw new ArgumentNullException(nameof(toolkit));
        if (toolCalls == null || toolCalls.Count == 0)
            return new List<ToolResultBlock>();

        // 串行模式
        if (!parallel)
        {
            var results = new List<ToolResultBlock>();
            foreach (var call in toolCalls)
            {
                var result = await ExecuteSingleWithInfrastructure(
                    toolkit, call, config, cancellationToken);
                if (result != null) results.Add(result);
            }
            return results;
        }

        // 并行模式 + concurrencySafe 分区
        var batches = new List<List<Func<Task<ToolResultBlock?>>>>();
        var currentSafe = new List<Func<Task<ToolResultBlock?>>>();

        foreach (var call in toolCalls)
        {
            var callCopy = call; // 捕获闭包
            Func<Task<ToolResultBlock?>> exec = () =>
                ExecuteSingleWithInfrastructure(toolkit, callCopy, config, cancellationToken);

            if (IsConcurrencySafe(toolkit, call.Name))
            {
                currentSafe.Add(exec);
            }
            else
            {
                if (currentSafe.Count > 0)
                {
                    batches.Add(new List<Func<Task<ToolResultBlock?>>>(currentSafe));
                    currentSafe.Clear();
                }
                batches.Add(new List<Func<Task<ToolResultBlock?>>> { exec });
            }
        }
        if (currentSafe.Count > 0)
            batches.Add(currentSafe);

        // 批次内并发，批次间串行（保持顺序）
        var allResults = new List<ToolResultBlock>();
        foreach (var batch in batches)
        {
            var batchTasks = batch.Select(f => f()).ToArray();
            var batchResults = await Task.WhenAll(batchTasks);
            foreach (var r in batchResults)
                if (r != null) allResults.Add(r);
        }
        return allResults;
    }

    // ==================== 基础设施 ====================

    private async Task<ToolResultBlock?> ExecuteSingleWithInfrastructure(
        Toolkit toolkit,
        ToolUseBlock call,
        ExecutionConfig? config,
        CancellationToken cancellationToken)
    {
        // 1. 查工具
        var tool = toolkit.Resolve(call.Name);
        if (tool == null)
        {
            return new ToolResultBlock
            {
                Id = call.Id, Name = call.Name,
                IsError = true, Output = $"Unknown tool: {call.Name}"
            };
        }

        // 2. 外部工具短路
        if (tool.IsExternal)
        {
            return new ToolResultBlock
            {
                Id = call.Id, Name = call.Name,
                IsError = false, IsSuspended = true,
                Output = "suspended"
            };
        }

        // 3. Schema 验证
        var validationErrors = ToolValidator.Validate(tool.GetSchema(), call.Input);
        if (validationErrors.Count > 0)
        {
            return new ToolResultBlock
            {
                Id = call.Id, Name = call.Name,
                IsError = true,
                Output = $"Parameter validation failed: {string.Join("; ", validationErrors)}"
            };
        }

        // 4. 合并预设参数
        var parameters = MergePresetParameters(toolkit, call.Name, call.Input);

        // 5. 执行 (使用当前执行器的重试/超时配置)
        try
        {
            var effectiveTimeout = config?.Timeout;
            // MaxRetries 是排除首次的重试次数，+1 = 总尝试次数
            var effectiveMaxAttempts = (config?.MaxRetries > 0 ? config.MaxRetries + 1 : 0) > 0
                ? config!.MaxRetries + 1 : _maxAttempts;
            var effectiveRetryDelay = config?.RetryDelay ?? _retryDelay;
            Func<System.Exception, int, bool>? shouldRetryFn = null;
            if (config?.RetryOn != null)
            {
                shouldRetryFn = (ex, _) => config.RetryOn(ex);
            }

            var executor = effectiveMaxAttempts > 1 || effectiveTimeout != Timeout.InfiniteTimeSpan
                ? new ToolExecutor(
                    maxAttempts: effectiveMaxAttempts,
                    timeout: effectiveTimeout,
                    retryDelay: effectiveRetryDelay,
                    shouldRetry: shouldRetryFn)
                : this;

            var result = await executor.ExecuteAsync(tool, parameters, cancellationToken);

            return new ToolResultBlock
            {
                Id = call.Id, Name = call.Name,
                IsError = !result.Success,
                IsSuspended = result.IsSuspended,
                Output = result.Success ? result.Result : result.Error
            };
        }
        catch (OperationCanceledException)
        {
            return new ToolResultBlock
            {
                Id = call.Id, Name = call.Name,
                IsError = true, Output = "Tool execution cancelled"
            };
        }
        catch (System.Exception ex)
        {
            return new ToolResultBlock
            {
                Id = call.Id, Name = call.Name,
                IsError = true,
                Output = $"Tool execution failed: {ex.Message}"
            };
        }
    }

    private static bool IsConcurrencySafe(Toolkit toolkit, string toolName)
    {
        var tool = toolkit.Resolve(toolName);
        if (tool == null) return true;
        if (tool is IToolConcurrencySafe safe)
            return safe.IsConcurrencySafe;
        return true; // 默认安全
    }

    private static Dictionary<string, object> MergePresetParameters(
        Toolkit toolkit, string toolName, Dictionary<string, object>? callInput)
    {
        var registered = toolkit.GetRegisteredTool(toolName);
        if (registered == null || registered.PresetParameters.Count == 0)
            return callInput ?? new Dictionary<string, object>();

        var merged = new Dictionary<string, object>(callInput ?? new());
        foreach (var kv in registered.PresetParameters)
            merged[kv.Key] = kv.Value;
        return merged;
    }
}
