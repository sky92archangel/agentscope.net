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

using System.Threading.Tasks;
using AgentScope.Core.Agent;
using AgentScope.Core.Tool;

namespace AgentScope.Core.Skill;

/// <summary>
/// Dynamic skill middleware: injects currently available skill descriptions into the system prompt phase,
/// and optionally triggers skill loading.
/// 动态技能中间件：在系统提示词阶段注入当前可用技能说明，并（可选）触发技能加载。
/// 实现 IToolkitAware，当技能被激活时同步激活对应的 SkillToolGroup。
/// Corresponds to Java: io.agentscope.core.skill.DynamicSkillMiddleware
/// </summary>
public class DynamicSkillMiddleware : MiddlewareBase, IToolkitAware
{
    /// <summary>
    /// The prompt provider that generates skill description sections.
    /// 负责生成技能说明段落的提示词提供者。
    /// </summary>
    private readonly AgentSkillPromptProvider _promptProvider;

    /// <summary>
    /// Whether to include only actively-enabled skills.
    /// 是否仅包含已激活的技能。
    /// </summary>
    private readonly bool _onlyActive;

    /// <summary>
    /// 当前绑定的 Toolkit 引用，用于同步 SkillToolGroup 激活状态。
    /// </summary>
    private Toolkit? _toolkit;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicSkillMiddleware"/> class.
    /// 初始化 <see cref="DynamicSkillMiddleware"/> 类的新实例。
    /// </summary>
    /// <param name="promptProvider">The skill prompt provider. / 技能提示词提供者。</param>
    /// <param name="onlyActive">If true, only active skills are included. / 若为 true，仅包含已激活的技能。</param>
    /// <exception cref="System.ArgumentNullException">Thrown when promptProvider is null. / 当 promptProvider 为 null 时抛出。</exception>
    public DynamicSkillMiddleware(AgentSkillPromptProvider promptProvider, bool onlyActive = true)
    {
        _promptProvider = promptProvider ?? throw new System.ArgumentNullException(nameof(promptProvider));
        _onlyActive = onlyActive;
    }

    /// <summary>
    /// IToolkitAware：绑定 Toolkit 时保存引用，并立即同步 SkillToolGroup。
    /// 当 Toolkit 深拷贝（如 Agent 状态隔离）时会自动调用此方法。
    /// </summary>
    public void RebindToolkit(Toolkit toolkit)
    {
        _toolkit = toolkit;
        SyncActiveSkillGroups();
    }

    /// <summary>
    /// 同步已激活技能与对应 SkillToolGroup 的激活状态。
    /// 技能激活则激活同名 ToolGroup，技能停用则停用对应 ToolGroup。
    /// </summary>
    private void SyncActiveSkillGroups()
    {
        if (_toolkit == null) return;

        var skills = _promptProvider.ListRegisteredSkills();
        foreach (var skill in skills)
        {
            if (skill.IsActiveByDefault)
                _toolkit.ActivateGroup(skill.Name);
            else
                _toolkit.DeactivateGroup(skill.Name);
        }
    }

    /// <inheritdoc />
    public override Task<string> OnSystemPromptAsync(IAgent agent, RuntimeContext ctx, string prompt)
    {
        // 每次系统提示词构建前同步 SkillToolGroup
        SyncActiveSkillGroups();

        // 生成技能说明段落并追加到系统提示词
        // Generate the skill description section and append it to the system prompt
        var section = _promptProvider.BuildSkillPromptSection(_onlyActive);
        if (!string.IsNullOrEmpty(section))
        {
            prompt = string.IsNullOrEmpty(prompt) ? section : prompt + "\n" + section;
        }

        return Task.FromResult(prompt);
    }
}
