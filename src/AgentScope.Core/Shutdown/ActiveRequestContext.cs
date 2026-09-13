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
using System.Threading;

namespace AgentScope.Core.Shutdown;

/// <summary>
/// 活跃请求上下文：跟踪当前正在执行的请求信息，
/// 允许在超时或关闭时中断特定请求。
/// 对应 Java: io.agentscope.core.shutdown.ActiveRequestContext
/// </summary>
public class ActiveRequestContext
{
    /// <summary>请求唯一标识符。</summary>
    public string RequestId { get; init; } = "";

    /// <summary>处理的 Agent 名称。</summary>
    public string AgentName { get; init; } = "";

    /// <summary>会话标识符。</summary>
    public string SessionId { get; init; } = "";

    /// <summary>请求开始时间 (UTC)。</summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 用于中断此请求的 CancellationTokenSource。
    /// 超时或关闭时通过它取消仍在执行的请求。
    /// </summary>
    public CancellationTokenSource? CancellationTokenSource { get; init; }
}
