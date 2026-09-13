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

namespace AgentScope.Core.AgUI;

/// <summary>
/// 管理 AG-UI 恢复流程：校验 resume 契约，跟踪每 thread 未决 interrupt，
/// 恢复时直接发 TOOL_CALL_RESULT 不重放 START/ARGS/END。
/// 对应 Java: io.agentscope.agui.processor.AguiResumeCoordinator
/// </summary>
public class AguiResumeCoordinator
{
    private readonly ConcurrentDictionary<string, PendingInterrupt> _pendingInterrupts = new();

    /// <summary>
    /// 注册中断：记录指定 thread 上因工具调用产生的挂起中断。
    /// </summary>
    /// <param name="threadId">线程标识。</param>
    /// <param name="toolCallId">工具调用标识。</param>
    /// <param name="confirmResult">用户确认结果（可为 null）。</param>
    public void RegisterInterrupt(string threadId, string toolCallId, object? confirmResult)
    {
        var pending = new PendingInterrupt(toolCallId, confirmResult, DateTime.UtcNow);
        _pendingInterrupts[threadId] = pending;
    }

    /// <summary>
    /// 校验并消费中断：尝试取出指定 thread 的未决中断。
    /// </summary>
    /// <param name="threadId">线程标识。</param>
    /// <param name="confirmResult">输出用户确认结果。</param>
    /// <returns>存在未决中断则 true，否则 false。</returns>
    public bool TryConsumeInterrupt(string threadId, out object? confirmResult)
    {
        if (_pendingInterrupts.TryRemove(threadId, out var pending))
        {
            confirmResult = pending.ConfirmResult;
            return true;
        }

        confirmResult = null;
        return false;
    }

    /// <summary>
    /// 清理过期中断：删除指定 thread 的未决中断记录。
    /// </summary>
    /// <param name="threadId">线程标识。</param>
    public void Cleanup(string threadId)
    {
        _pendingInterrupts.TryRemove(threadId, out _);
    }
}

internal record PendingInterrupt(string ToolCallId, object? ConfirmResult, DateTime CreatedAt);
