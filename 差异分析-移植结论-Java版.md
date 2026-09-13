# AgentScope Java v2.0.3 → .NET v2.0.1 差异分析与移植建议

> 分析日期: 2026-09-12
>
> Java 版本: v2.0.3-SNAPSHOT
>
> .NET 版本: develop/v2.0.1

---

## 一、总体评估

AgentScope.NET **v2.0.1** 与 AgentScope Java **v2.0.3-SNAPSHOT** 的架构同源，均源于 Python 原版设计。两者在核心模块 (Agent/Model/Tool/Memory/MCP/Event) 上高度一致，完成度均超过 Python 的 80%。但各有侧重：

| 维度 | Java v2.0.3 | .NET v2.0.1 | 差异方向 |
|------|------------|-------------|---------|
| 架构模式 | Spring Boot 微服务 + Maven 多模块 | 原生 .NET + NuGet 多项目 | 不同生态 |
| 核心框架 | 完全对标 Python | 完全对标 Python | ~95% 一致 |
| Service 层 | 四平面架构 (Gateway/Data/Control/Scheduler) | 有 IService + ControlPlane/DataPlane 但无完整应用框架 | ⚠️ .NET 缺应用层 |
| 扩展生态 | 更丰富的存储后端 (MongoDB/JDBC) | 更多渠道 (WeCom) 和 RAG 后端 (Haystack) | 各有千秋 |
| DI 集成 | 12 个 Spring Boot Starter | 仅 Tracing 有 DI 扩展 | ⚠️ .NET 缺 DI 引导 |
| Workflow | 无独立 Workflow 引擎 | 有 DAG WorkflowEngine | ✅ .NET 独有优势 |
| TUI | 无 | 有 Terminal.Gui 界面 | ✅ .NET 独有 |

---

## 二、Java 具备而 .NET 缺失的功能 (P0-P2)

### P0 — 必须移植

#### 1. Chat Completions Web API 协议层

**Java 路径**: `agentscope-extensions/agentscope-extensions-protocol/agentscope-extensions-chat-completions-web/`

**Java 思路**: 独立的协议模块，将 Agent 暴露为 `/v1/chat/completions` 兼容的 REST API，使任意 OpenAI 兼容客户端可直接调用 AgentScope Agent。

**.NET 现状**: ⚠️ `OpenAIClient` 仅有 `ChatCompletionsEndpoint` 常量，内部作为客户端使用。没有独立的协议 DTO 类和 REST 端点暴露。

**移植工作量**: 低 (~2-3 天)

**建议**: 创建 `AgentScope.Protocols.ChatCompletions` 项目，包含 `ChatCompletionRequest`/`ChatCompletionResponse` DTO + ASP.NET Core 中间件/Minimal API 端点。

---

### P1 — 建议移植

#### 2. MongoDB 存储实现

**Java 路径**: `agentscope-extensions-mongodb/`

**Java 实现**: 基于 MongoDB Java Driver 的分布式存储实现 (`MongoDBStore`)，同时作为 `IAgentStateStore` 和 `IDistributedStore` 后端。

**.NET 现状**: ❌ 完全不存在。全项目无任何 MongoDB 引用。

**移植工作量**: 低 (~3-5 天)

**建议**: 基于 `MongoDB.Driver` NuGet 包，实现 `IDistributedStore` 和 `DistributedAgentStateStore`。

---

#### 3. 通用 JDBC / ADO.NET 存储封装

**Java 路径**: `agentscope-extensions-jdbc/`

**Java 实现**: 基于 Spring JDBC (JdbcTemplate) 的通用关系型数据库存储，支持动态 SQL 生成。

**.NET 现状**: ⚠️ .NET 有 `IDistributedStore` 接口 + MySQL/PostgreSQL/COS/OSS/Redis 实现，但缺少**通用 ADO.NET 封装**（可直接对接任意 `DbConnection` 的泛化实现）。

**移植工作量**: 中 (~1 周)

**建议**: 创建 `AgentScope.Extensions.Store.Jdbc`，基于 `System.Data.Common.DbConnection` 抽象，实现泛用型关系库存储。

---

#### 4. DI 集成 / NuGet 引导包

**Java 路径**: `agentscope-spring-boot-starters/` (12 个 Starter)

**Java 实现**: 每个模型/功能都有对应的 `@Configuration` 自动装配 + `application.yml` 属性绑定。

**.NET 现状**: ❌ 仅有 `TracingBootstrap.AddAgentScopeTracing()` 一个扩展方法。核心框架不提供 `IServiceCollection` 扩展。

**移植工作量**: 中 (~1 周)

**建议**: 创建 `AgentScope.Bootstrap` NuGet 包，提供:
```csharp
services.AddAgentScopeCore()
       .AddModel<OpenAIModel>("gpt-4")
       .AddMemory<SqliteMemory>()
       .AddPermissionEngine()
       .WithHarness();
```

---

#### 5. 模型 E2E 测试体系

**Java 路径**: `agentscope-extensions-model-e2e-tests/`

**Java 实现**: 对 OpenAI/Anthropic/DashScope/Gemini/Ollama 的端到端测试，使用真实 API Key (CI 环境变量注入)。

**.NET 现状**: ❌ 无专用的 E2E 测试项目。仅有单元测试 (`AgentScope.Core.Tests` + `AgentScope.Integration.Tests`)。

**移植工作量**: 低 (每个 Provider 半天)

**建议**: 创建 `AgentScope.IntegrationTests.E2E` 项目，配置 `appsettings.Test.json` 存放 API Key 环境变量引用。

---

### P2 — 可选移植

#### 6. 工具子系统中间抽象补齐

**Java 独有的抽象**:

| Java 类 | 功能 | .NET 等效 |
|---------|------|-----------|
| `DefaultContextStore` | 工具执行上下文存取 | ⚠️ .NET 有 `ToolExecutionContext` 但更简化 |
| `ToolEmitter` | 工具调用事件发射 | ❌ 无独立抽象 |
| `ToolResultConverter` | 工具结果格式化 | ⚠️ `DefaultToolResultConverter` 已有 |
| `SchemaOnlyTool` | 仅 Schema 不执行的工具 | ❌ 无 |
| `MetaToolFactory` | 元工具工厂 | ❌ 无 |
| `ToolGroupManager` | 工具组生命周期管理 | ⚠️ 有 `ToolGroup` 但无 Manager |

**移植工作量**: 低 (~3-5 天)

#### 7. 文件工具分层

**Java 独有的抽象**:

| Java 类 | 功能 | .NET 等效 |
|---------|------|-----------|
| `FileToolUtils` | 文件操作共享工具类 | ❌ 无 |
| `CommandValidator` | Shell 命令验证器接口 | ⚠️ .NET 有 `CommandValidator` |
| `UnixCommandValidator` | Unix 安全策略 | ❌ 无 |
| `WindowsCommandValidator` | Windows 安全策略 | ❌ 无 |

**移植工作量**: 低 (~1-2 天)

#### 8. 遗留状态兼容层

**Java 路径**: `state/legacy/ToolkitState.java`

**Java 实现**: 从 v1.x 状态格式迁移到 v2.x 的兼容桥接。

**.NET 现状**: ⚠️ 已有 `LegacyStateLoader`，但功能范围可能小于 Java。

**移植工作量**: 低 (~1 天)

---

## 三、.NET 的独有优势 (Java 不具备)

| 功能 | 路径 | 说明 |
|------|------|------|
| **WorkflowEngine (DAG)** | `AgentScope.Core/Workflow/` | .NET 独有的 DAG 工作流引擎，9 种节点类型 + Builder 模式，Java 无等效 |
| **AgentScope.TUI** | `AgentScope.TUI/` | 基于 Terminal.Gui 的终端聊天界面 |
| **AgentScope.Uno** | `AgentScope.Uno/` | 跨平台桌面 UI (Uno Platform) |
| **Haystack RAG** | `AgentScope.Extensions.Rag.Haystack/` | Haystack RAG 集成 (Java 无) |
| **WeCom 渠道** | `AgentScope.Extensions.Channel.WeCom/` | 企业微信渠道 (Java 无) |
| **AgentScope.Extensions.Training** | `AgentScope.Extensions.Training/` | 模型训练管理器 |
| **SQLite Memory** | `AgentScope.Core/Memory/SqliteMemory.cs` | 基于 EF Core 的持久化记忆，含批量模式和信赖搜索 |
| **Reactive 流式** | 全库 `IAsyncEnumerable` + `IObservable` | .NET 原生异步流式支持比 Java Reactor 更简洁 |
| **MCP Streamable HTTP** | `AgentScope.Core/MCP/StreamableHttpMcpClient.cs` | MCP Streamable HTTP 客户端 |
| **JSONL Trace Exporter** | `AgentScope.Core/Tracing/JsonlTraceExporter.cs` | JSONL 格式追踪导出 |

---

## 四、总体对比矩阵

| 功能模块 | Java v2.0.3 | .NET v2.0.1 | 差距 |
|---------|------------|-------------|------|
| **Agent / ReAct** | ✅ 完整 | ✅ 完整 | = |
| **消息系统 (Msg/Event/Block)** | ✅ 完整 (含 Audio/Video Block) | ✅ 完整 | = |
| **模型提供商** | ✅ 6 家 (OpenAI/Anthropic/DashScope/Gemini/Ollama + E2E) | ✅ 7 家 (含 DeepSeek) | .NET 多 DeepSeek |
| **格式化器** | ✅ 5 家 | ✅ 4 家 | = |
| **MCP 协议** | ✅ Stdio + HTTP (同步/异步) | ✅ Stdio + SSE + Streamable HTTP | .NET 多 SSE/StreamableHTTP |
| **工具系统** | ✅ 更丰富的中间抽象 (Emitter/Converter/MetaToolFactory) | ✅ 但有简化 | ⚠️ 缺中间抽象 |
| **记忆系统** | ✅ 完整 | ✅ 完整 (含 SQLite) | = |
| **权限系统** | ✅ 完整 | ✅ 完整 | = |
| **多 Agent** | ✅ AgentGroup/MsgHub | ✅ AgentGroup/Coordinator/Router | = |
| **追踪系统** | ✅ Tracer + OtelTracingMiddleware | ✅ Tracer + OpenTelemetry 集成 | = |
| **Service 四平面** | ✅ **完整 (Gateway/Data/Scheduler/Common)** | ⚠️ 有接口但无完整应用框架 | ⚠️ 需补齐 |
| **DI / Starter** | ✅ **12 个 Spring Boot Starter** | ❌ **仅有 Tracing DI** | ❌ 需创建 |
| **Chat Completions Web API** | ✅ **独立协议模块** | ⚠️ **内部引用仅常量** | ⚠️ 需补齐 |
| **MongoDB 存储** | ✅ **已实现** | ❌ **完全不存在** | ❌ 需创建 |
| **JDBC / ADO.NET 存储** | ✅ **已实现** | ⚠️ 有 MySQL/PostgreSQL 但无通用封装 | ⚠️ 可选补齐 |
| **WebSocket Transport** | ✅ OkHttp + JDK 实现 | ✅ ClientWebSocketTransport | = |
| **Graceful Shutdown** | ✅ 完整体系 | ✅ 完整体系 | = |
| **Filesystem 体系** | ✅ CompositeFilesystem | ✅ Composite/Overlay/Baked/Sandbox | = |
| **Skill 子系统** | ✅ 完整 | ✅ 完整 (含 SkillBox/MarkdownParser) | = |
| **Workflow 引擎** | ❌ 无 | ✅ **DAG WorkflowEngine** | ✅ .NET 独有 |
| **TUI 终端界面** | ❌ 无 | ✅ **Terminal.Gui** | ✅ .NET 独有 |
| **RAG 集成** | ✅ 4 家 (Bailian/Dify/RagFlow/Simple) | ✅ 4 家 (Bailian/Dify/Haystack/RagFlow) | .NET 多 Haystack |
| **沙箱** | ✅ 4 种 (Docker/K8s/E2B/Daytona) | ✅ 5 种 (含 AgentRun) | .NET 多 AgentRun |
| **渠道** | ✅ 5 种 (钉钉/飞书/GitHub/GitLab/企微) | ✅ 5 种 | = |
| **调度器** | ✅ Quartz + XXL-Job | ✅ Quartz + XXL-Job | = |
| **A2A 协议** | ✅ 独立扩展模块 | ✅ 在 Core + Harness 中 | = |
| **AG-UI 协议** | ✅ 独立扩展模块 | ✅ 在 Core/AgUI 中 | = |
| **Agent 协议** | ✅ 独立扩展模块 | ✅ AgentProtocolTransport | = |
| **向量存储** | ❌ (依赖第三方) | ✅ 4 种 (Milvus/Qdrant/ES/PgVector) | ✅ .NET 独有 |
| **E2E 测试** | ✅ **专用模型 E2E 测试模块** | ❌ **无 E2E 测试** | ⚠️ 需补齐 |

---

## 五、建议的移植路线

```
第1阶段 (P0, 1周)
├── Chat Completions Web API 协议 (2-3天)
│   ├── 创建 AgentScope.Protocols.ChatCompletions 项目
│   ├── ChatCompletionRequest / ChatCompletionResponse DTO
│   └── ASP.NET Core Minimal API 端点暴露
│
└── DI 集成 / NuGet 引导包 (2-3天)
    └── 创建 AgentScope.Bootstrap 包
        ├── AddAgentScopeCore() 核心注册
        ├── AddModel<T>() 模型注册
        ├── AddMemory<T>() 记忆注册
        └── 支持 appsettings.json 配置绑定

第2阶段 (P1, 2周)
├── MongoDB 存储 (3-5天)
│   └── AgentScope.Extensions.Store.MongoDB
│       ├── MongoDBDistributedStore
│       └── MongoDBAgentStateStore
│
├── 通用 ADO.NET 存储 (3-5天)
│   └── AgentScope.Extensions.Store.AdoNet
│       └── 基于 DbConnection 抽象的泛化实现
│
└── 模型 E2E 测试 (3-5天)
    └── AgentScope.IntegrationTests.E2E
        ├── OpenAI / Anthropic / DashScope / Gemini / Ollama
        └── CI 环境变量注入 API Key

第3阶段 (P2, 1周)
├── 工具中间抽象补齐 (3-5天)
│   ├── SchemaOnlyTool
│   ├── MetaToolFactory
│   └── ToolGroupManager
│
├── 文件工具分层 (1-2天)
│   ├── FileToolUtils
│   └── UnixCommandValidator / WindowsCommandValidator
│
└── 遗留状态兼容增强 (1天)
```

---

## 六、结论

**总体差异较小**：Java 与 .NET 版本同源度极高，核心功能完成度均在 90% 以上。需要移植的实质性新功能不多，主要集中在**生态适配**和**补齐**方面：

| 领域 | 核心差距 |
|------|---------|
| 🔴 必须补齐 | 无 (核心功能均已对齐) |
| 🟡 建议补齐 | Chat Completions API 协议、DI/NuGet 引导包、MongoDB 存储 |
| 🟢 可选补齐 | 工具子系统的中间抽象、文件工具分层、E2E 测试 |

.NET 版本在 WorkflowEngine(DAG)、向量存储(4种 vs 0)、TUI、更多沙箱/渠道/RAG 后端等方面甚至略优于 Java 版本。

> **对比前次 Python v2.0.8 分析**：Java 与 .NET 的差异远小于 Python 与 .NET 的差异。Python 版多了 Realtime Agent、GoalPipeline、SOP、TTS Provider 等全新功能模块 → **这些才是真正的移植优先级**。Java 版的差异更多是生态适配层面的缺失，而非功能模块缺失。
