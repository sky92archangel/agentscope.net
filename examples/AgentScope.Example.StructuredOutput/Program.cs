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

using System.Text.Json;
using AgentScope.Core;
using AgentScope.Core.Formatter;
using AgentScope.Core.Message;
using AgentScope.Core.Model;
using AgentScope.Core.Model.OpenAI;

namespace AgentScope.Example.StructuredOutput;

/// <summary>
/// 结构化输出演示
/// 创建支持结构化输出的模型，调用 GenerateStructuredOutputAsync，
/// 输出反序列化结果。
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== 结构化输出示例 ===\n");

        // 步骤 1：创建支持结构化输出的 OpenAI 模型
        var model = new OpenAIModel(
            modelName: "gpt-4o",
            apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "sk-placeholder",
            baseUrl: Environment.GetEnvironmentVariable("OPENAI_BASE_URL"));

        // 步骤 2：创建 Agent
        var agent = EnhancedReActAgent.Builder()
            .Name("StructuredOutputAgent")
            .SysPrompt("你是一个提取结构化数据的助手。")
            .Model(model)
            .Build();

        // 步骤 3：准备输入消息
        var messages = new List<Msg>
        {
            Msg.Builder()
                .Role("user")
                .TextContent("提取以下信息：张三，28岁，北京，软件工程师")
                .Build()
        };

        Console.WriteLine("输入: 提取以下信息：张三，28岁，北京，软件工程师");

        // 步骤 4：调用结构化输出并反序列化
        try
        {
            var result = await agent.GenerateStructuredOutputAsync<PersonInfo>(messages);
            Console.WriteLine($"\n反序列化结果:");
            Console.WriteLine($"  姓名: {result.Name}");
            Console.WriteLine($"  年龄: {result.Age}");
            Console.WriteLine($"  城市: {result.City}");
            Console.WriteLine($"  职业: {result.Occupation}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n结构化输出调用（需要真实 API key）: {ex.Message}");
            Console.WriteLine("提示: 设置 OPENAI_API_KEY 和 OPENAI_BASE_URL 环境变量可运行此示例。");
            Console.WriteLine("\n本地测试：直接创建 PersonInfo 对象演示反序列化。");

            // 无真实 API key 时演示本地 JSON 反序列化
            var json = """{"Name":"张三","Age":28,"City":"北京","Occupation":"软件工程师"}""";
            var demo = JsonSerializer.Deserialize<PersonInfo>(json);
            Console.WriteLine($"\n模拟反序列化结果:");
            Console.WriteLine($"  姓名: {demo!.Name}");
            Console.WriteLine($"  年龄: {demo.Age}");
            Console.WriteLine($"  城市: {demo.City}");
            Console.WriteLine($"  职业: {demo.Occupation}");
        }

        Console.WriteLine("\n=== 示例结束 ===");
    }
}

/// <summary>
/// 人员信息实体，用于结构化输出反序列化
/// </summary>
public class PersonInfo
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string City { get; set; } = "";
    public string Occupation { get; set; } = "";
}
