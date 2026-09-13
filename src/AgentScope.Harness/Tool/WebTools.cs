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
using AgentScope.Core.Tool;

namespace AgentScope.Harness.Tool;

/// <summary>
/// Web 工具集。对标 Java WebTools。
/// 提供网页抓取和网络搜索功能。
/// </summary>
public sealed class WebTools : ITool
{
    private readonly HttpClient _http;

    /// <summary>是否禁用网络工具（默认 false）。</summary>
    public bool Disabled { get; init; }
    public bool IsExternal => false;

    /// <summary>
    /// 构造 WebTools。
    /// </summary>
    /// <param name="http">可选的 HttpClient 实例。</param>
    /// <param name="disabled">是否禁用网络工具。</param>
    public WebTools(HttpClient? http = null, bool disabled = false)
    {
        _http = http ?? new HttpClient();
        Disabled = disabled;
    }

    public string Name => "web_fetch";
    public string Description => "获取网页内容";

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (Disabled)
            return ToolResult.Fail("网络工具已被禁用");

        var url = parameters.GetValueOrDefault("url")?.ToString();
        if (string.IsNullOrWhiteSpace(url))
            return ToolResult.Fail("需要 url 参数");

        try
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return ToolResult.Ok(content.Length > 10000 ? content[..10000] + "\n... (已截断)" : content);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"获取网页失败: {ex.Message}");
        }
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
                ["url"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "要抓取的网页 URL" }
            },
            ["required"] = new[] { "url" }
        }
    };
}

/// <summary>
/// 网络搜索工具（Tavily API）。对标 Java WebSearchTool。
/// </summary>
public sealed class WebSearchTool : ITool
{
    private readonly HttpClient _http;
    private string? _apiKey;

    public string Name => "web_search";
    public string Description => "使用 Tavily API 进行网络搜索";

    public bool IsExternal => false;
    /// <summary>是否禁用（默认 false）。</summary>
    public bool Disabled { get; init; }

    /// <summary>Tavily API Key（优先取环境变量 TAVILY_API_KEY）。</summary>
    public string? ApiKey
    {
        get => _apiKey ?? Environment.GetEnvironmentVariable("TAVILY_API_KEY");
        set => _apiKey = value;
    }

    /// <summary>
    /// 构造 WebSearchTool。
    /// </summary>
    /// <param name="http">可选的 HttpClient 实例。</param>
    /// <param name="apiKey">可选的 Tavily API Key。</param>
    /// <param name="disabled">是否禁用。</param>
    public WebSearchTool(HttpClient? http = null, string? apiKey = null, bool disabled = false)
    {
        _http = http ?? new HttpClient();
        _apiKey = apiKey;
        Disabled = disabled;
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (Disabled)
            return ToolResult.Fail("网络搜索工具已被禁用");

        var query = parameters.GetValueOrDefault("query")?.ToString();
        if (string.IsNullOrWhiteSpace(query))
            return ToolResult.Fail("需要 query 参数");

        var apiKey = ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return ToolResult.Fail("未配置 Tavily API Key（设置 TAVILY_API_KEY 环境变量或在构造时传入）");

        try
        {
            var maxResults = 5;
            if (parameters.TryGetValue("max_results", out var mr) &&
                int.TryParse(mr?.ToString(), out var parsed) && parsed > 0)
                maxResults = Math.Min(parsed, 20);

            var body = new Dictionary<string, object>
            {
                ["api_key"] = apiKey,
                ["query"] = query,
                ["max_results"] = maxResults,
                ["include_answer"] = true,
                ["include_raw_content"] = false
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("https://api.tavily.com/search", content);
            response.EnsureSuccessStatusCode();
            var respJson = await response.Content.ReadAsStringAsync();

            // 美化输出
            using var doc = JsonDocument.Parse(respJson);
            var sb = new System.Text.StringBuilder();
            if (doc.RootElement.TryGetProperty("answer", out var answerEl) &&
                answerEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(answerEl.GetString()))
            {
                sb.AppendLine($"摘要: {answerEl.GetString()}");
                sb.AppendLine();
            }

            if (doc.RootElement.TryGetProperty("results", out var resultsEl))
            {
                int i = 1;
                foreach (var r in resultsEl.EnumerateArray())
                {
                    var title = r.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var link = r.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                    var snippet = r.TryGetProperty("content", out var s) ? s.GetString() ?? "" : "";
                    sb.AppendLine($"{i}. {title}");
                    sb.AppendLine($"   URL: {link}");
                    if (!string.IsNullOrWhiteSpace(snippet))
                        sb.AppendLine($"   {snippet}");
                    sb.AppendLine();
                    i++;
                }
            }

            return ToolResult.Ok(sb.ToString().TrimEnd());
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"网络搜索失败: {ex.Message}");
        }
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
                ["query"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "搜索查询" },
                ["max_results"] = new Dictionary<string, object>
                {
                    ["type"] = "integer",
                    ["description"] = "最大结果数（默认 5，最大 20）"
                }
            },
            ["required"] = new[] { "query" }
        }
    };
}
