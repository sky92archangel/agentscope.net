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

namespace AgentScope.Extensions.Sandbox.AgentRun;

/// <summary>
/// 阿里云 OSS 存储桶挂载配置。对标 Java OssMountConfig。
/// </summary>
public sealed class OssMountConfig
{
    /// <summary>OSS 端点地址（如 https://oss-cn-hangzhou.aliyuncs.com）。</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>存储桶名称。</summary>
    public string Bucket { get; set; } = "";

    /// <summary>访问密钥 ID。</summary>
    public string AccessKeyId { get; set; } = "";

    /// <summary>访问密钥 Secret。</summary>
    public string AccessKeySecret { get; set; } = "";

    /// <summary>挂载到容器内的目标路径（可选，默认 /mnt/oss）。</summary>
    public string MountPath { get; set; } = "/mnt/oss";

    /// <summary>是否只读挂载。</summary>
    public bool ReadOnly { get; set; }
}

/// <summary>
/// NAS 文件系统挂载配置。对标 Java NasMountConfig。
/// </summary>
public sealed class NasMountConfig
{
    /// <summary>NAS 服务端路径（如 123456abc.cn-hangzhou.nas.aliyuncs.com:/share）。</summary>
    public string ServerPath { get; set; } = "";

    /// <summary>挂载到容器内的目标路径。</summary>
    public string MountPath { get; set; } = "/mnt/nas";

    /// <summary>是否只读挂载。</summary>
    public bool ReadOnly { get; set; }
}

/// <summary>
/// AgentRun 挂载配置集合。对标 Java AgentRunMountConfig。
/// </summary>
public sealed class AgentRunMountConfig
{
    /// <summary>OSS 挂载配置列表。</summary>
    public List<OssMountConfig> OssMounts { get; set; } = [];

    /// <summary>NAS 挂载配置列表。</summary>
    public List<NasMountConfig> NasMounts { get; set; } = [];
}
