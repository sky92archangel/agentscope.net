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
using AgentScope.Core.Formatter;
using AgentScope.Core.Message;
using AgentScope.Core.Model;
using AgentScope.Core.Model.Anthropic;

namespace AgentScope.Example.PromptCaching;

/// <summary>
/// Anthropic prompt caching 演示
/// 创建 AnthropicModel 启用 PromptCaching，
/// 发送多轮对话，观察 CachedTokens。
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Anthropic Prompt Caching 示例 ===\n");

        // 步骤 1：创建启用 PromptCaching 的 AnthropicModel
        var defaultOptions = new GenerateOptions
        {
            PromptCaching = new AnthropicPromptCacheConfig
            {
                Enabled = true,
                CacheTtl = "5m"
            }
        };

        var model = new AnthropicModel(
            modelName: "claude-3-5-sonnet-20241022",
            apiKey: Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
            baseUrl: Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL"),
            defaultOptions: defaultOptions);

        Console.WriteLine("模型: claude-3-5-sonnet-20241022");
        Console.WriteLine("PromptCaching: 已启用 (CacheTtl=5m)");
        Console.WriteLine();

        // 步骤 2：构建多轮对话消息
        var messages = new List<Msg>
        {
            Msg.Builder().Role("user").TextContent("请用中文介绍一下 AgentScope 框架。").Build()
        };

        try
        {
            // 第一轮
            Console.WriteLine("--- 第一轮对话 ---");
            var response1 = await model.GenerateAsync(new ModelRequest { Messages = messages });
            Console.WriteLine($"响应: {response1.Text?[..Math.Min(100, response1.Text?.Length ?? 0)]}...");
            Console.WriteLine($"成功: {response1.Success}");

            // 追加第二轮消息
            messages.Add(Msg.Builder().Role("assistant").TextContent(response1.Text ?? "").Build());
            messages.Add(Msg.Builder().Role("user").TextContent("它的主要特点是什么？").Build());

            Console.WriteLine($"\n--- 第二轮对话（预期命中缓存） ---");
            var response2 = await model.GenerateAsync(new ModelRequest { Messages = messages });
            Console.WriteLine($"响应: {response2.Text?[..Math.Min(100, response2.Text?.Length ?? 0)]}...");
            Console.WriteLine($"成功: {response2.Success}");

            Console.WriteLine("\n（注: PromptCaching 效果需在 Anthropic API 响应中查看 x-amz-cache 相关标头）");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n调用 Anthropic API 出错（需要真实 API key）: {ex.Message}");
            Console.WriteLine("提示: 设置 ANTHROPIC_API_KEY 环境变量可运行此示例。");
            Console.WriteLine("\n本地演示：模拟 PromptCaching 流程。");

            // 无真实 API key 时模拟流程
            SimulateCaching();
        }

        Console.WriteLine("\n=== 示例结束 ===");
    }

    /// <summary>
    /// 无真实 API key 时模拟缓存效果演示
    /// </summary>
    static void SimulateCaching()
    {
        Console.WriteLine("--- 第一轮对话（模拟） ---");
        Console.WriteLine("响应: AgentScope 是一个 LLM 应用开发框架...");
        Console.WriteLine("输入 Token: 150 (缓存未命中)");
        Console.WriteLine("输出 Token: 200");

        Console.WriteLine("\n--- 第二轮对话（模拟，缓存命中） ---");
        Console.WriteLine("响应: 主要特点包括多 Agent 协作、工具调用...");
        Console.WriteLine("输入 Token: 350 (缓存命中，节省 ~150 tokens)");
        Console.WriteLine("输出 Token: 180");
    }
}
