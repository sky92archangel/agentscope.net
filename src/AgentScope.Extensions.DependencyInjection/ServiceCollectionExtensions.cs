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

using AgentScope.Core;
using AgentScope.Core.Model;
using AgentScope.Core.Permission;
using AgentScope.Core.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentScope.Extensions.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> 的 AgentScope 扩展方法。
/// 对标 Java agentscope-spring-boot-starters 的 AgentScopeAutoConfiguration。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 AgentScope 核心服务到 DI 容器。
    /// </summary>
    public static IServiceCollection AddAgentScope(this IServiceCollection services)
    {
        return services.AddAgentScope(_ => { });
    }

    /// <summary>
    /// 注册 AgentScope 核心服务到 DI 容器，并允许通过 <paramref name="configure"/> 委托配置选项。
    /// </summary>
    public static IServiceCollection AddAgentScope(
        this IServiceCollection services,
        Action<AgentScopeOptions> configure)
    {
        // 配置选项
        var options = new AgentScopeOptions();
        configure(options);
        services.TryAddSingleton(options);

        // IModelFactory — 包装静态 ModelFactory 的单例服务
        services.TryAddSingleton<IModelFactory, ModelFactoryWrapper>();

        // IToolFactory — 包装静态 ToolFactory 的单例服务
        services.TryAddSingleton<IToolFactory, ToolFactoryWrapper>();

        // StatePersistence — 默认全部管理
        services.TryAddSingleton(StatePersistence.All);

        // IPermissionEngine — 根据配置可选注册
        if (options.EnablePermission)
        {
            services.TryAddSingleton<IPermissionEngine>(_ =>
                new PermissionEngine(PermissionMode.Default));
        }

        // 示例：将 EnhancedReActAgent 注册为可注入的服务
        services.TryAddTransient<EnhancedReActAgent>(sp =>
        {
            var modelFactory = sp.GetRequiredService<IModelFactory>();
            var engine = sp.GetService<IPermissionEngine>();

            // 使用配置中的默认模型
            AgentScope.Core.Model.IModel? model = null;
            if (options.DefaultModel != null && options.ModelConfigs.TryGetValue(options.DefaultModel, out var cfg))
            {
                var parts = cfg.Split('|');
                var provider = parts.Length > 0 ? parts[0] : "openai";
                var modelName = parts.Length > 1 ? parts[1] : "gpt-4o";
                var apiKey = parts.Length > 2 ? parts[2] : "";
                model = modelFactory.Create(provider, modelName, apiKey);
            }

            // 未配置默认模型时才回退到占位配置（避免覆盖用户配置且避免 401）
            model ??= modelFactory.Create("openai", "gpt-4o", "");

            // 使用 Builder 创建 Agent（EnhancedReActAgent 构造函数为 internal）
            var builder = new EnhancedReActAgentBuilder()
                .Name("agent")
                .Model(model)
                .SysPrompt("你是一个 AI 助手，请使用 ReAct 模式思考并完成任务。");

            if (engine != null)
            {
                builder.PermissionEngine(engine);
            }

            return builder.Build();
        });

        return services;
    }
}
