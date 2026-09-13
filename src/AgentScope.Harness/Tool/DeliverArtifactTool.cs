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

using System.Text;
using AgentScope.Core.Tool;

namespace AgentScope.Harness.Tool;

/// <summary>
/// 交付工件工具。对标 Java ArtifactDeliveryTool。
/// 接受文件路径和内容，写入沙箱 output 目录或返回 Base64 编码。
/// </summary>
public sealed class DeliverArtifactTool : ITool
{
    /// <summary>
    /// 沙箱 output 目录路径（可选）。若不设置则返回 Base64 编码。
    /// </summary>
    public string? OutputDirectory { get; init; }

    public string Name => "deliver_artifact";
    public string Description => "交付文件工件（写入沙箱输出目录或返回 Base64 编码）";
    public bool IsExternal => false;

    public DeliverArtifactTool(string? outputDirectory = null)
    {
        OutputDirectory = outputDirectory;
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var filePath = parameters.GetValueOrDefault("file_path")?.ToString();
        if (string.IsNullOrWhiteSpace(filePath))
            return ToolResult.Fail("需要 file_path 参数");

        var content = parameters.GetValueOrDefault("content")?.ToString();
        if (content == null)
            return ToolResult.Fail("需要 content 参数");

        var overwrite = false;
        if (parameters.TryGetValue("overwrite", out var owObj))
        {
            if (owObj is bool b) overwrite = b;
            else bool.TryParse(owObj?.ToString(), out overwrite);
        }

        var outputDir = OutputDirectory;
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            // 写入文件系统
            try
            {
                var fullPath = Path.GetFullPath(Path.Combine(outputDir, filePath));

                // 安全检查：防止路径穿越（必须校验目录分隔符边界，
                // 否则 "..\out2\evil.txt" 这类同前缀兄弟目录可绕过）
                var sep = Path.DirectorySeparatorChar;
                var normalizedOutput = Path.GetFullPath(outputDir).TrimEnd(sep);
                var normalizedFull = Path.GetFullPath(fullPath).TrimEnd(sep);
                var inBounds = normalizedFull.StartsWith(
                    normalizedOutput + sep, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedFull, normalizedOutput, StringComparison.OrdinalIgnoreCase);
                if (!inBounds)
                    return ToolResult.Fail("路径穿越安全检查失败");

                if (File.Exists(fullPath) && !overwrite)
                    return ToolResult.Fail($"文件已存在且 overwrite=false: {filePath}");

                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(fullPath, content);
                return ToolResult.Ok(new Dictionary<string, object>
                {
                    ["status"] = "delivered",
                    ["path"] = fullPath,
                    ["size"] = content.Length
                });
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"写入工件失败: {ex.Message}");
            }
        }

        // 无 output 目录时返回 Base64 编码
        var bytes = Encoding.UTF8.GetBytes(content);
        var base64 = Convert.ToBase64String(bytes);
        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["status"] = "encoded",
            ["file_path"] = filePath,
            ["content_base64"] = base64,
            ["encoding"] = "base64",
            ["size"] = bytes.Length
        });
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
                ["file_path"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "目标文件路径（相对于 output 目录）"
                },
                ["content"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "文件内容"
                },
                ["overwrite"] = new Dictionary<string, object>
                {
                    ["type"] = "boolean",
                    ["description"] = "是否覆盖已存在的文件（默认 false）"
                }
            },
            ["required"] = new[] { "file_path", "content" }
        }
    };
}
