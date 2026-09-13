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
using AgentScope.Core.Message;
using AgentScope.Core.Model;
using AgentScope.Core.Model.GLM;

namespace AgentScope.Example.GLM;

/// <summary>
/// 智谱 GLM 模型演示
/// 使用 GlmModel 发送消息。
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== GLM 模型示例 ===\n");

        // 步骤 1：创建 GlmModel（自动从环境变量读取 ZAI_API_KEY / GLM_API_KEY / ZHIPUAI_API_KEY）
        var model = new GlmModel(
            modelName: "glm-5.2",
            apiKey: Environment.GetEnvironmentVariable("ZAI_API_KEY")
                    ?? Environment.GetEnvironmentVariable("GLM_API_KEY"));

        Console.WriteLine($"模型: {GlmModel.DefaultModel}");
        Console.WriteLine($"API Base: {GlmModel.DefaultBaseUrl}");

        // 步骤 2：构建消息
        var messages = new List<Msg>
        {
            Msg.Builder().Role("user").TextContent("你好，请介绍一下你自己。").Build()
        };

        try
        {
            // 步骤 3：发送请求
            var request = new ModelRequest { Messages = messages };
            var response = await model.GenerateAsync(request);
            Console.WriteLine($"\n响应: {response.Text?[..Math.Min(200, response.Text?.Length ?? 0)]}");
            Console.WriteLine($"\n成功: {response.Success}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n调用 GLM API 出错（需要真实 API key）: {ex.Message}");
            Console.WriteLine("提示: 设置 ZAI_API_KEY 或 GLM_API_KEY 环境变量可运行此示例。");
            Console.WriteLine("\n本地演示：模拟 GLM 调用流程。");

            // 无真实 API key 时模拟流程
            Console.WriteLine("模型: glm-5.2");
            Console.WriteLine("消息: 你好，请介绍一下你自己。");
            Console.WriteLine("响应: 你好！我是智谱 AI 的 GLM 模型，很高兴为你服务...");
        }

        Console.WriteLine("\n=== 示例结束 ===");
    }
}
