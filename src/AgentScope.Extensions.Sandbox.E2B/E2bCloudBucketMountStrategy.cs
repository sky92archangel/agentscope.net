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

namespace AgentScope.Extensions.Sandbox.E2B;

/// <summary>
/// E2B 云桶挂载策略：将 S3/MinIO 兼容存储桶挂载到沙箱内。
/// 对标 Java E2bCloudBucketMountStrategy。
/// </summary>
public sealed class E2bCloudBucketMountStrategy
{
    /// <summary>存储桶名称（必需）。</summary>
    public string BucketName { get; set; } = "";

    /// <summary>S3/MinIO 兼容端点地址。</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>访问密钥 ID。</summary>
    public string AccessKey { get; set; } = "";

    /// <summary>秘密访问密钥。</summary>
    public string SecretKey { get; set; } = "";

    /// <summary>挂载到容器内的目标路径（可选，默认 /mnt/bucket）。</summary>
    public string MountPath { get; set; } = "/mnt/bucket";

    /// <summary>是否只读挂载。</summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// 返回桶挂载的 CLI 参数，供沙箱启动时注入 rclone/s3fs 挂载命令。
    /// </summary>
    public string ToMountArgs()
    {
        return $"--bucket {BucketName} --endpoint {Endpoint} --mount-path {MountPath}";
    }
}

/// <summary>
/// 云桶挂载策略集合，供 E2B 沙箱启动时附加挂载配置。
/// 对标 Java E2bCloudBucketMountConfigs。
/// </summary>
public sealed class E2bCloudBucketMountConfigs
{
    /// <summary>挂载策略列表。</summary>
    public List<E2bCloudBucketMountStrategy> Mounts { get; set; } = [];
}
