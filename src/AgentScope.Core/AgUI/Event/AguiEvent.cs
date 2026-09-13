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

using AgentScope.Core.AgUI.Model;

namespace AgentScope.Core.AgUI.Event;

/// <summary>
/// AG-UI 事件类型枚举。对�?Java AguiEventType�?/// </summary>
public enum AguiEventType
{
    RunStarted, RunFinished, RunError,
    StepStarted, StepFinished,
    TextMessageStart, TextMessageContent, TextMessageEnd, TextMessageChunk,
    ToolCallStart, ToolCallArgs, ToolCallEnd, ToolCallChunk, ToolCallResult,
    StateSnapshot, StateDelta, MessagesSnapshot,
    ActivitySnapshot, ActivityDelta,
    Raw, Custom,
    ReasoningStart, ReasoningMessageStart, ReasoningMessageContent,
    ReasoningMessageEnd, ReasoningMessageChunk, ReasoningEnd, ReasoningEncryptedValue
}

/// <summary>
/// AG-UI 事件记录基类。对�?Java sealed interface AguiEvent�?/// </summary>
public abstract record AguiEvent(
    AguiEventType Type,
    string ThreadId,
    string RunId,
    long? Timestamp = null,
    object? RawEvent = null);

// ── Run 生命周期 ──
public sealed record RunStarted(string ThreadId, string RunId, string? ParentRunId, RunAgentInput Input,
    long? Timestamp = null) : AguiEvent(AguiEventType.RunStarted, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record RunFinished(string ThreadId, string RunId, RunFinishedOutcome Outcome,
    long? Timestamp = null) : AguiEvent(AguiEventType.RunFinished, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record RunError(string ThreadId, string RunId, string Message, int ErrorCode,
    long? Timestamp = null) : AguiEvent(AguiEventType.RunError, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// ── 步骤 ──
public sealed record StepStarted(string ThreadId, string RunId, string StepName,
    long? Timestamp = null) : AguiEvent(AguiEventType.StepStarted, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record StepFinished(string ThreadId, string RunId, string StepName,
    long? Timestamp = null) : AguiEvent(AguiEventType.StepFinished, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// ── 文本消杯�?──
public sealed record TextMessageStart(string ThreadId, string RunId, string MessageId, string Role,
    long? Timestamp = null) : AguiEvent(AguiEventType.TextMessageStart, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record TextMessageContent(string ThreadId, string RunId, string Delta,
    long? Timestamp = null) : AguiEvent(AguiEventType.TextMessageContent, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record TextMessageEnd(string ThreadId, string RunId,
    long? Timestamp = null) : AguiEvent(AguiEventType.TextMessageEnd, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// ── 工具调用�?──
public sealed record ToolCallStart(string ThreadId, string RunId, string ToolCallId, string ToolCallName,
    long? Timestamp = null) : AguiEvent(AguiEventType.ToolCallStart, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record ToolCallArgs(string ThreadId, string RunId, string Delta,
    long? Timestamp = null) : AguiEvent(AguiEventType.ToolCallArgs, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record ToolCallEnd(string ThreadId, string RunId,
    long? Timestamp = null) : AguiEvent(AguiEventType.ToolCallEnd, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record ToolCallResult(string ThreadId, string RunId, string ToolName, object? Result,
    bool IsError = false, long? Timestamp = null) : AguiEvent(AguiEventType.ToolCallResult, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// ── 推睆�?──
public sealed record ReasoningStart(string ThreadId, string RunId,
    long? Timestamp = null) : AguiEvent(AguiEventType.ReasoningStart, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record ReasoningMessageStart(string ThreadId, string RunId,
    long? Timestamp = null) : AguiEvent(AguiEventType.ReasoningMessageStart, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record ReasoningMessageContent(string ThreadId, string RunId, string Delta,
    long? Timestamp = null) : AguiEvent(AguiEventType.ReasoningMessageContent, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record ReasoningMessageEnd(string ThreadId, string RunId,
    long? Timestamp = null) : AguiEvent(AguiEventType.ReasoningMessageEnd, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record ReasoningEnd(string ThreadId, string RunId,
    long? Timestamp = null) : AguiEvent(AguiEventType.ReasoningEnd, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// ── 快照与增量 ──
public sealed record StateSnapshot(string ThreadId, string RunId, object State,
    long? Timestamp = null) : AguiEvent(AguiEventType.StateSnapshot, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record StateDelta(string ThreadId, string RunId, string AgentId, IReadOnlyDictionary<string, object?> Delta,
    long? Timestamp = null) : AguiEvent(AguiEventType.StateDelta, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record MessagesSnapshot(string ThreadId, string RunId, IReadOnlyList<AguiMessage> Messages, string SnapshotId,
    long? Timestamp = null) : AguiEvent(AguiEventType.MessagesSnapshot, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record ActivitySnapshot(string ThreadId, string RunId, string ActivityId, string Status, long ActivityTimestamp,
    long? Timestamp = null) : AguiEvent(AguiEventType.ActivitySnapshot, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record ActivityDelta(string ThreadId, string RunId, string ActivityId, IReadOnlyDictionary<string, object?> Delta,
    long? Timestamp = null) : AguiEvent(AguiEventType.ActivityDelta, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// ── 文本消息块（流式）──
public sealed record TextMessageChunk(string ThreadId, string RunId, string MessageId, string TextDelta, int Index,
    long? Timestamp = null) : AguiEvent(AguiEventType.TextMessageChunk, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// ── 工具调用块（流式）──
public sealed record ToolCallChunk(string ThreadId, string RunId, string ToolCallId, string Name, string ArgumentsPartial,
    long? Timestamp = null) : AguiEvent(AguiEventType.ToolCallChunk, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// ── 推理消息块（流式）──
public sealed record ReasoningMessageChunk(string ThreadId, string RunId, string MessageId, string Content, string? Signature = null,
    long? Timestamp = null) : AguiEvent(AguiEventType.ReasoningMessageChunk, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record ReasoningEncryptedValue(string ThreadId, string RunId, string Subtype, string EntityId, string EncryptedData,
    long? Timestamp = null) : AguiEvent(AguiEventType.ReasoningEncryptedValue, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// ── 原始事件 ──
public sealed record Raw(string ThreadId, string RunId, string EventType, System.Text.Json.JsonElement Payload,
    long? Timestamp = null) : AguiEvent(AguiEventType.Raw, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

public sealed record CustomEvent(string ThreadId, string RunId, string Name, object? Value = null,
    long? Timestamp = null) : AguiEvent(AguiEventType.Custom, ThreadId, RunId, Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// ── Outcome 类型 ──
public abstract record RunFinishedOutcome;
public sealed record RunFinishedSuccessOutcome(object? Result) : RunFinishedOutcome;
public sealed record RunFinishedInterruptOutcome(Interrupt Interrupt) : RunFinishedOutcome;

/// <summary>
/// 挂起的中断（等待用户处睆）。对�?Java Interrupt�?/// </summary>
public sealed record Interrupt(string Reason, string? ReplyId = null, IDictionary<string, object>? Metadata = null);
