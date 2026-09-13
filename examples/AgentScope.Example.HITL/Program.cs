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

using AgentScope.Core;
using AgentScope.Core.Agent;
using AgentScope.Core.Events;
using AgentScope.Core.Message;
using AgentScope.Core.Model;
using AgentScope.Core.Permission;
using AgentScope.Core.Tool;

namespace AgentScope.Example.HITL;

/// <summary>
/// HITL（Human-in-the-Loop）暂停恢复演示
/// 创建一个 PermissionEngine 配置为 Ask 模式的 Agent，
/// 当工具调用触发权限 Ask 时，观察 ConfirmCallback 被调用。
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== HITL 暂停恢复示例 ===\n");

        // 步骤 1：创建 PermissionEngine，默认 Default 模式 → 无规则匹配时返回 Ask
        var permission = new PermissionEngine(PermissionMode.Default);

        // 步骤 2：创建 MockModel（无需真实 API key）
        var model = MockModel.Builder()
            .ModelName("mock-model")
            .Build();

        // 步骤 3：注册一个简单工具
        var calculator = new CalculatorTool();

        // 步骤 4：创建 EnhancedReActAgent 并注入 ConfirmCallback
        var agent = EnhancedReActAgent.Builder()
            .Name("HITL_Agent")
            .SysPrompt("你是一个有帮助的助手，使用工具回答问题。")
            .Model(model)
            .AddTool(calculator)
            .PermissionEngine(permission)
            .ConfirmCallback(async evt =>
            {
                // 当权限引擎判定为 Ask 时，此回调被调用
                Console.WriteLine($"\n[ConfirmCallback 被调用]");
                Console.WriteLine($"  工具: {evt.ToolName}");
                Console.WriteLine($"  参数: {string.Join(", ", evt.Arguments?.Select(kv => $"{kv.Key}={kv.Value}") ?? [])}");
                Console.WriteLine($"  决定: 批准\n");
                return ConfirmResult.Approve();
            })
            .Build();

        // 步骤 5：发送一条需要工具调用的消息
        var msg = Msg.Builder()
            .Role("user")
            .TextContent("请计算 1+1 等于多少？")
            .Build();

        Console.WriteLine("发送消息: 请计算 1+1 等于多少？");
        var response = await agent.CallAsync(msg);
        Console.WriteLine($"\nAgent 响应: {response.GetTextContent()}");
        Console.WriteLine("\n=== 示例结束 ===");
    }
}

/// <summary>
/// 简单的计算器工具，用于演示 HITL 流程
/// </summary>
public class CalculatorTool : ITool
{
    public string Name => "calculator";
    public string Description => "执行数学计算";
    public bool IsExternal => false;

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("expression", out var expr))
        {
            return Task.FromResult(ToolResult.Ok($"计算结果: {expr} = ?"));
        }
        return Task.FromResult(ToolResult.Fail("缺少 expression 参数"));
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
                ["expression"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "数学表达式"
                }
            },
            ["required"] = new[] { "expression" }
        }
    };
}
