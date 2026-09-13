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

namespace AgentScope.Core.Tool;

/// <summary>
/// 由持有 Toolkit 引用的组件 (如中间件) 实现。
///
/// 当 Toolkit 被深拷贝时 (如 HarnessAgentBuilder.Build() 中隔离 Agent 状态),
/// 实现此接口的组件会收到新的 Toolkit 引用，避免持有过期引用导致状态不一致。
///
/// 对标: Java io.agentscope.core.tool.ToolkitAware
/// </summary>
public interface IToolkitAware
{
    /// <summary>
    /// 当 Toolkit 被替换时调用，通知组件绑定新的 Toolkit 实例。
    /// </summary>
    /// <param name="toolkit">新的 Toolkit 实例。</param>
    void RebindToolkit(Toolkit toolkit);
}
