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

using System.Collections.Generic;

namespace AgentScope.Core.Tool;

/// <summary>
/// ExtendedModel 的简单内存实现。
/// 使用预构建的字典和列表直接存储扩展属性。
///
/// 对标: Java io.agentscope.core.tool.SimpleExtendedModel
/// </summary>
public class SimpleExtendedModel : ExtendedModel
{
    public IReadOnlyDictionary<string, object> AdditionalProperties { get; }
    public IReadOnlyList<string> AdditionalRequired { get; }

    public SimpleExtendedModel(
        IReadOnlyDictionary<string, object> additionalProperties,
        IReadOnlyList<string> additionalRequired)
    {
        AdditionalProperties = additionalProperties
            ?? new Dictionary<string, object>();
        AdditionalRequired = additionalRequired
            ?? new List<string>();
    }

    public SimpleExtendedModel()
        : this(new Dictionary<string, object>(), new List<string>())
    {
    }
}
