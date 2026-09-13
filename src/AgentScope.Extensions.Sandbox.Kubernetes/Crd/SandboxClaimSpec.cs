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

namespace AgentScope.Extensions.Sandbox.Kubernetes.Crd;

/// <summary>
/// SandboxClaim 自定义资源规范：请求一个沙箱环境。
/// 对应 Java: io.agentscope.sandbox.kubernetes.crd.SandboxClaim
/// </summary>
public class SandboxClaimSpec
{
    /// <summary>沙箱容器镜像。</summary>
    public string Image { get; set; } = "ubuntu:22.04";

    /// <summary>资源需求（CPU/内存）。</summary>
    public ResourceRequirements? Resources { get; set; }

    /// <summary>预热池目标大小。</summary>
    public int WarmPoolSize { get; set; } = 3;

    /// <summary>环境变量。</summary>
    public Dictionary<string, string>? Env { get; set; }
}

/// <summary>
/// Kubernetes 资源需求（CPU/内存）。对应 Java ResourceRequirements。
/// </summary>
public class ResourceRequirements
{
    /// <summary>CPU 请求量。</summary>
    public string Cpu { get; set; } = "1";

    /// <summary>内存请求量。</summary>
    public string Memory { get; set; } = "2Gi";
}
