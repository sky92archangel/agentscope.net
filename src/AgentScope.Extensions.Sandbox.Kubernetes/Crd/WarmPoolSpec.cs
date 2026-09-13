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
/// WarmPool CRD：预创建沙箱实例池，实现秒级获取。
/// 对应 Java: io.agentscope.sandbox.kubernetes.crd.WarmPool
/// </summary>
public class WarmPoolSpec
{
    /// <summary>沙箱容器镜像。</summary>
    public string Image { get; set; } = "";

    /// <summary>最小就绪实例数。</summary>
    public int MinReady { get; set; } = 2;

    /// <summary>池最大容量。</summary>
    public int MaxSize { get; set; } = 10;
}
