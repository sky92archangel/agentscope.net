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

namespace AgentScope.Core.MCP;

/// <summary>
/// MCP 工具调用的元数据（注入 CallToolRequest.meta + toolCallId）。
/// 对应 Java: io.agentscope.core.tool.mcp.McpMeta
/// </summary>
/// <param name="ToolCallId">工具调用 ID</param>
/// <param name="Extra">额外的自定义元数据字典</param>
public sealed record McpMeta(
    string? ToolCallId,
    Dictionary<string, object>? Extra = null);
