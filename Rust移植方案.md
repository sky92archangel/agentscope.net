# AgentScope.NET → Rust 移植方案

> 分析日期: 2026-09-13
>
> 源版本: AgentScope.NET v2.0.1 (develop/v2.0.1, 959 个 .cs / ~66,000 行, net10.0)
>
> 目标: 用 Rust 实现 AgentScope 框架核心能力，形成可被 Rust 生态使用的 Agent 开发框架 (`agentscope-rs`)

---

## 目录

- [第一部分：当前代码 Review 结论](#第一部分当前代码-review-结论)
- [第二部分：移植总体策略](#第二部分移植总体策略)
- [第三部分：C# → Rust 核心概念映射](#第三部分c--rust-核心概念映射)
- [第四部分：关键技术难点与对策](#第四部分关键技术难点与对策)
- [第五部分：核心类型 Rust 设计草案](#第五部分核心类型-rust-设计草案)
- [第六部分：Cargo Workspace 工程结构](#第六部分cargo-workspace-工程结构)
- [第七部分：依赖选型（NuGet → crates.io）](#第七部分依赖选型nuget--cratesio)
- [第八部分：分阶段实施路线图](#第八部分分阶段实施路线图)
- [第九部分：测试策略](#第九部分测试策略)
- [第十部分：裁剪建议与风险清单](#第十部分裁剪建议与风险清单)

---

# 第一部分：当前代码 Review 结论

## 1.1 总体架构

```
src/ (48 个项目)
├── AgentScope.Core                    # 核心：~290 个 .cs，约 3 万行，35 个功能目录
├── AgentScope.Harness                 # 运行时外壳：~208 个 .cs，Builder+组合器模式
├── AgentScope.Extensions              # 扩展点接口层（零第三方依赖）
├── AgentScope.Extensions.*            # 42 个具体扩展（Model/Channel/RAG/Mem/Sandbox/Store/Vector/...）
├── AgentScope.Extensions.DependencyInjection  # DI 装配（对标 Spring Boot Starter）
├── AgentScope.Tracing.OpenTelemetry   # OTel 导出
├── AgentScope.TUI (Terminal.Gui)      # 终端 UI
└── AgentScope.Uno (Uno.WinUI)         # 桌面 GUI（XAML 待修）
tests/ (~620 个用例, xUnit, 无 Mock 库)
```

架构链路：**Agent → Model/Formatter/Transport、Memory、Toolkit、Hook、Middleware、Permission、State 七大件经 Builder 组合注入**；`EnhancedReActAgent`（1898 行）为执行引擎。

## 1.2 优点（移植时可保留的设计资产）

| # | 优点 | 证据 |
|---|------|------|
| 1 | **Core 零框架依赖**：仅 DotNetEnv / EF Core.Sqlite / System.Reactive 3 个 NuGet，不依赖 DI 容器，可整体映射为 Rust crate | `AgentScope.Core.csproj` |
| 2 | **接口边界清晰**：IAgent / IModel / IMemory / ITool / IHook / IFormatter / IVectorStore / ISandbox 等全部以接口+基类解耦，天然对应 Rust trait | `Agent\IAgent.cs`、`Model\IModel.cs`、`Tool\ITool.cs` |
| 3 | **扩展点前置于扩展**：`AgentScope.Extensions` 项目只含接口，42 个扩展各自独立、多数零第三方依赖（纯 HttpClient+System.Text.Json） | `Extensions\*` |
| 4 | **事件/中间件双通道扩展模型**：Hook（10 个阶段回调，支持 ShouldStop 短路）+ Middleware（洋葱链包裹事件流）职责分明，可直接平移 | `Hook\IHook.cs`、`Agent\MiddlewareBase.cs` |
| 5 | **消息系统结构化**：ContentBlock 抽象 record 体系（Text/Image/Audio/Video/ToolUse/ToolResult/Thinking），是 Rust enum 的完美映射对象 | `Message\ContentBlock.cs` |
| 6 | **状态与并发设计成熟**：版本化 CAS 的 IAgentStateStore、Toolkit 工具并发安全标记（IToolConcurrencySafe）、外部工具挂起(HITL)语义完整 | `State\IAgentStateStore.cs`、`Tool\ITool.cs` |
| 7 | **测试资产可平移**：~620 个用例自建测试替身（MockModel 等），无 Mock 框架魔法，逐条翻译为 Rust 测试成本低 | `tests\AgentScope.Core.Tests\` |

## 1.3 问题与风险（review 发现）

| # | 问题 | 位置 | 对移植的影响 |
|---|------|------|-------------|
| 1 | **主循环仍是文本协议 ReAct**：`EnhancedReActAgent` 用 `Thought:/Action:/Action Input:` 文本解析驱动循环，而原生 function calling 协议能力（tool_use 解析）已沉淀在 Formatter/Parser + ContentBlock 层，**未接入 Agent 主循环** | `EnhancedReActAgent.cs` (1898 行) | ⭐ Rust 版**建议直接实现原生 function calling 循环**（这是移植的最大改进机会，也是 Java/Python 新版方向）；文本协议可作为兼容模式保留 |
| 2 | 新旧两代 Agent 并存、大量复制代码 | `ReActAgent.cs`(544 行, 已 Obsolete) vs `EnhancedReActAgent.cs` | Rust 版只保留一个实现，不做 1:1 复制 |
| 3 | **两套同名 IChannel 抽象**：Harness `Gateway\Channel\IChannel.cs`（网关侧）与 Extensions `Channel\IChannel.cs`（IM 平台侧）概念冲突 | Harness / Extensions | Rust 命名需区分（如 `GatewayChannel` vs `PlatformChannel`） |
| 4 | Hook 接口 10 个方法偏宽（接口隔离不足），多数实现只用其中 2-3 个 | `Hook\IHook.cs` | Rust 版可拆分为小 trait 或改用单方法 + 事件枚举（见 §4.6） |
| 5 | `Dictionary<string, object>` 弱类型贯穿工具参数/ToolResult/Metadata，类型安全靠约定 | `Tool\ITool.cs`、`Msg.cs` | Rust 统一收敛为 `serde_json::Value`，反而提升类型安全 |
| 6 | 完整解决方案 118 错误 / 230 警告（测试 Mock + Uno XAML 未清零，Core 为 0 错误） | README 自述 | 移植基线以 Core 0 错误为准，Uno/TUI 不在首批范围 |
| 7 | `System.Reactive`（IObservable）与 `IAsyncEnumerable` 两套流式范式并存 | `Model\IModel.cs`、`MsgHub.cs` | Rust 版统一为 `futures::Stream`，不移植 Rx 范式（见 §4.2） |
| 8 | 工程卫生良好（.env/*.db 已 gitignore），但仓库根目录散落 10+ 份过程性 md 文档 | 根目录 | 与移植无关，建议归档到 docs/ |

**Review 总评**：架构同源于 Java 版且模块边界清晰，Core 可移植性**高**；主要移植成本不在业务逻辑，而在 **C# 运行时特性的等价替换**（AsyncLocal / IAsyncEnumerable / 反射 / event / 弱类型字典），这正是第四部分要解决的。

---

# 第二部分：移植总体策略

## 2.1 三条移植原则

1. **接口 1:1、实现地道**：trait 名称与语义对标 C# 接口（保持文档/注释双语风格，标注"对应 C#: xxx"），但实现使用 Rust 惯用法（enum、builder、`impl Stream`），不做逐行直译。
2. **借移植升级**：主循环升级为原生 function calling；砍掉 Rx 双范式与旧版 ReActAgent；弱类型字典收敛为 `serde_json::Value`。
3. **Core 先行、扩展按需**：42 个扩展不承诺全量移植，先交付 Core + Harness 核心 + 3~5 个高频扩展（OpenAI/Anthropic 模型、SQLite Memory、MCP、OTel），其余按社区需求排期。

## 2.2 能力范围分级

| 级别 | 内容 | 对标 |
|------|------|------|
| **P0 必须** | Msg/ContentBlock、IModel+OpenAI/Anthropic/DeepSeek、Formatter、Memory(InMemory/Sqlite)、Toolkit+内置工具、EnhancedReActAgent（原生 function calling）、Hook、Middleware、Permission、State/CAS、Session、中断恢复 | `AgentScope.Core` 主干 |
| **P1 应该** | 流式全链路（Stream 事件 + Accumulator）、MCP(stdio/SSE/HTTP)、Plan、Pipeline、Workflow、MultiAgent(MsgHub)、RAG(内存向量)、Skill(Markdown)、Tracing(OTel) | Core 其余 + Tracing.OpenTelemetry |
| **P2 可选** | Harness 运行时、A2A、AgUI、Service/Discovery、GracefulShutdown、DI 等价物（builder facade） | Harness + 对应 Core 模块 |
| **P3 按需** | 42 个扩展逐个决策（Redis/Postgres/Qdrant/Milvus/Docker 沙箱/Quartz 等都有成熟 Rust crate 对应）；TUI(ratatui)/GUI 不移植 | Extensions.* |

---

# 第三部分：C# → Rust 核心概念映射

## 3.1 语言机制映射总表

| C# 机制 | C# 中的位置 | Rust 等价物 | 难度 |
|---------|------------|------------|------|
| `interface` + 抽象基类 | IAgent/AgentBase 等 | `trait` + 默认方法；基类模板方法 → trait 默认方法 + 宏或组合 | 中 |
| `async Task<T>` | 全链路 | `async fn` + `async_trait`（dyn 分发）或原生 AFIT | 中 |
| `IAsyncEnumerable<T>` + `yield return` | StreamEventsAsync / GenerateStreamAsync / 中间件洋葱链 | `futures::Stream` + `async-stream` crate 的 `stream!` 宏 | 中 |
| `IObservable<T>` (System.Reactive) | IModel.Generate、MsgHub | **废弃 Rx 范式**，统一为 `Stream` / `tokio::sync::broadcast` | 低 |
| `AsyncLocal<T>` | `RuntimeContext.Current` | `tokio::task_local!` 兼容层 + 显式传参为主（见 §4.3） | 中 |
| `record` + `with` | RuntimeContext/ContentBlock/AgentEvent | `#[derive(Clone)] struct` + `..Default::default()` 或 builder；不可变语义靠 Clone | 低 |
| abstract record 继承体系 | ContentBlock、AgentEvent | **`enum`**（serde `tag` 序列化） | 低（最顺滑的映射） |
| C# `event` | PlanNotebook、ServiceManager、ToolEmitter | `tokio::sync::broadcast` / `tokio::sync::watch` / 回调 `Arc<dyn Fn>` | 中 |
| 反射注册（`[Tool]` 特性扫描） | `Toolkit.RegisterTool(object)`、`ToolSchemaGenerator` | **proc-macro** `#[agentscope::tool]` 编译期生成（对标 source generator，见 §4.4） | 高 |
| `Dictionary<string, object?>` | 工具参数/ToolResult/Metadata | `serde_json::Value` / `serde_json::Map<String, Value>` | 低 |
| `CancellationToken` | 全链路 | `tokio_util::sync::CancellationToken`（语义最接近，支持克隆共享） | 低 |
| `lock` 同步接口 (MemoryBase) | IMemory 实现 | `std::sync::RwLock`（同步）或 `parking_lot::RwLock` | 低 |
| `ConcurrentDictionary` | ModelRegistry 等 | `dashmap` | 低 |
| 接口默认实现 (DIM) | IFormatter.ApplyTools、IAgentStateStore | trait 默认方法 | 低 |
| `internal` 构造器 + Builder | EnhancedReActAgentBuilder 等 | `pub(crate) fn new` + `Builder` struct | 低 |
| LINQ | 各处 | 迭代器链（`iter().map().filter().collect()`） | 低 |
| 事件参数类 + 可改写字段 | HookEvents（SummaryText 等） | `&mut` 事件 struct（天然可变借用） | 低 |
| `IDisposable` / using | McpClient、Transport | `Drop` + 显式 `shutdown().await`（异步释放不能进 Drop，需显式 close） | 中 |
| `Nullable` 引用类型 | 全局 | `Option<T>`（编译器强制，类型安全反而更好） | 低 |
| 异常 (try/catch 全链路) | 各模块 | `Result<T, AgentScopeError>`（thiserror 错误枚举，`?` 传播）；框架内不留 panic | 中 |
| 多线程 Task.WhenAll | ToolExecutor 并行执行 | `futures::future::join_all` / `JoinSet` | 低 |

## 3.2 架构模式映射

| C# 模式 | Rust 模式 |
|---------|----------|
| 组合注入（Builder 传 7 大件） | Builder struct 持有 `Arc<dyn Trait>` 字段，`build()` 产出 Agent |
| HookManager 顺序执行 + ShouldStop | `Vec<Arc<dyn Hook>>` + `for` 循环 + 早退 |
| Middleware 洋葱链（next 委托） | `type Next = Fn(Input) -> BoxFuture<Stream>`；或改为 `tower::Service`/`Layer` 风格 |
| SPI 注册表 (ModelRegistry) | `Registry<RwLock<HashMap<Key, ProviderFactory>>>`，启动时显式注册（或 `inventory`/`linkme` crate 静态收集，不推荐首版用） |
| DependencyInjection 扩展 | 不引 DI 容器；提供 `AgentScope::builder()` facade；服务端场景对接 `tower` 生态 |

---

# 第四部分：关键技术难点与对策

## 4.1 async trait 与 dyn 动态分发（最大结构性决策）

**问题**：框架中 Model/Hook/Middleware/Tool/Store 全部以 trait object 注入（`Arc<dyn IModel>`），而 Rust 原生 AFIT（async fn in trait）产生的 trait 不是 dyn-compatible（async fn 默认返回 `impl Future`）。

**对策**：统一使用 `#[async_trait]` 宏（返回 `Pin<Box<dyn Future + Send>>`），代价是每个调用一次堆分配——对 LLM Agent 场景（单次调用含秒级网络 IO）完全可忽略：

```rust
#[async_trait]
pub trait Model: Send + Sync {
    fn model_name(&self) -> &str;
    fn supports_native_structured_output(&self) -> bool;
    async fn generate(&self, request: ModelRequest) -> Result<ModelResponse>;
    // 流式版本独立方法（对标 IStreamingChatModel）
    fn generate_stream(&self, messages: Vec<Msg>, ct: CancellationToken)
        -> BoxStream<'static, Result<ChatResponse>>;
}
```

- 高频纯计算路径（Accumulator、PermissionEngine 同步判定、Schema 生成）**不用 async**，保持同步 trait，对标 C# 中本来就是同步的 `Evaluate`/`GetSchema`。
- 若未来需要极致性能，可在热点 trait（如 Tool 执行）提供 enum-dispatch 快路径，但**不作为首版目标**。

## 4.2 流式：IAsyncEnumerable → Stream

C# 的 `yield return` 事件流是框架主干（`StreamEventsAsync`）。Rust 方案：

```rust
// 对标: EnhancedReActAgent.ProcessWithReActLoopStreamAsync
use async_stream::stream;
use futures::Stream;

pub fn stream_events(&self, input: Vec<Msg>, ctx: RuntimeContext)
    -> impl Stream<Item = AgentEvent> + Send + '_
{
    stream! {
        let mut iteration = 0;
        while iteration < self.max_iterations {
            // yield return 事件 —— 与 C# 一一对应
            for await chunk in self.reasoning_stream(&input).await {
                yield AgentEvent::reasoning_chunk(chunk);
            }
            // ...
        }
    }
}
```

要点：
- `futures::StreamExt::next()` / `for await` 消费，对标 `await foreach`；
- 中间件的 `OnAgentAsync`（包住整个事件流）用 `stream.map()` / `stream.filter_map()` 组合子实现洋葱包裹，语义等价；
- **System.Reactive 整体不移植**：`IModel.Generate()` 的 Rx 版本删除，只保留 async + Stream 两个方法；`MsgHub` 的 `IObserver<Msg>` 改为 `tokio::sync::broadcast::Sender<Msg>`。

## 4.3 RuntimeContext 与 AsyncLocal

C# 依赖 `AsyncLocal<RuntimeContext>.Current` 隐式跨异步流传播。Rust 没有 ExecutionContext 流动机制，两个方案：

| 方案 | 做法 | 取舍 |
|------|------|------|
| **A（推荐，主路径）** | **显式传参**：`ctx: &RuntimeContext` 作为所有 CallAsync/Stream 链路的参数（C# 版本来就有该参数，只是常用默认 null） | 类型显式、无魔法、符合 Rust 习惯；改动集中在签名层 |
| B（兼容层） | `tokio::task_local! { static CTX: RuntimeContext }`，提供 `RuntimeContext::current()` | 语义最接近 C#，但作用域限于 `task_local!` 包裹的 scope 内，跨 spawn 需手动 scope，易踩坑 |

方案：签名显式传递为主；同时提供 `with_context()` 辅助函数在 task 边界自动克隆传递，降低用户负担。

## 4.4 反射注册 → proc-macro（工作量最大单项）

C# `Toolkit.RegisterTool(object)` 靠运行时反射扫描 `[Tool]`/`[ToolParam]` 方法生成 Schema 与调用桥。Rust 没有运行时反射，**用编译期宏等价替换**：

```rust
// 用户侧体验（对标 C# [Tool] 特性反射注册）
use agentscope::tool;

#[tool(
    name = "get_weather",
    description = "查询指定城市天气"
)]
async fn get_weather(
    #[param(description = "城市名")] city: String,
    #[param(description = "温度单位", default = "celsius")] unit: Option<String>,
) -> Result<WeatherInfo> { ... }

let mut toolkit = Toolkit::new();
toolkit.register_fn(get_weather_tool());   // 宏生成的注册函数
```

宏展开生成三样东西（对标 `ReflectiveTool` + `ToolSchemaGenerator`）：
1. `ToolSchema`（JSON Schema：参数名/类型/描述/默认值，从签名与 `#[param]` 提取）；
2. 参数反序列化桥（`serde_json::Map<String,Value>` → 强类型参数，失败返回 `ToolResult::fail`）；
3. `Arc<dyn Tool>` 工厂函数。

结构体式工具（对标 `ToolBase` 继承）直接 `impl Tool for MyTool`。Schema 生成另备 `schemars` 集成路径：`#[derive(JsonSchema)]` 自动出 Schema，减少宏复杂度。

## 4.5 弱类型收敛

C# 中 `Dictionary<string, object?>`、`object? Content`、`object? Output` 在 Rust 全部收敛为：

```rust
pub type JsonMap = serde_json::Map<String, Value>;

pub struct ToolUseBlock {
    pub id: String,
    pub name: String,
    pub input: Option<JsonMap>,
    pub content: Option<String>,
}
```

需要保留任意类型的地方（如 ToolResult 内部对象）用 `Value`；确需富类型透传的少数场景（如子 Agent 结果）用 `Arc<dyn Any + Send + Sync>` 并收敛到一个 `ToolPayload` 枚举（Text/Json/Blocks/Bytes），**不开放裸 Any 给用户**。

## 4.6 C# event → Rust 事件通道

| C# event | Rust 等价 |
|----------|----------|
| `PlanNotebook.NodeStatusChanged` | `broadcast::Sender<PlanEvent>` |
| `ServiceManager.StatusChanged/Heartbeat` | `broadcast::Sender<ServiceEvent>` |
| `ToolEmitter.OnToolEmit` | 回调 `Vec<Box<dyn Fn(&ToolEvent) + Send + Sync>>`（低频）或 broadcast |
| `SubagentEventBus` | `broadcast` 或 mpsc hub |
| `InterruptibleAgentBase.InterruptionRequested` | `CancellationToken` + `watch` 状态 |

订阅端统一返回 `broadcast::Receiver`，用户 `while let Ok(ev) = rx.recv().await`。

## 4.7 Hook 双形态设计（解决 10 方法宽接口问题）

保留 1:1 的 `Hook` trait（10 个带默认空实现的 async 方法，`async_trait` 支持默认方法），同时提供事件枚举糖：

```rust
#[async_trait]
pub trait Hook: Send + Sync {
    fn name(&self) -> &str;
    // 默认空实现 —— 实现者只覆盖关心的阶段（比 C# 更省事，C# 需要 HookBase）
    async fn on_pre_reasoning(&self, _ev: &mut PreReasoningEvent) {}
    async fn on_post_reasoning(&self, _ev: &mut PostReasoningEvent) {}
    // ... 共 10 个，语义与 C# IHook 完全一致，事件参数用 &mut 实现可改写
}

// 事件枚举形态（HookManager 内部统一分发，亦可供 enum-based hook 使用）
pub enum HookEvent<'a> {
    PreReasoning(&'a mut PreReasoningEvent),
    // ...
}
```

`HookManager` 移植逻辑：顺序执行 + 任一 hook `should_stop` 即短路 + 错误事件兜底，与 C# 一致。

## 4.8 Middleware 洋葱链

C# `MiddlewareBase` 有 5 个虚方法、`next` 委托链。Rust 用类型别名表达链条：

```rust
pub type AgentStream = BoxStream<'static, AgentEvent>;
pub type Next<'a> = std::sync::Arc<dyn Fn(AgentInput) -> AgentStream + Send + Sync + 'a>;

#[async_trait]
pub trait AgentMiddleware: Send + Sync {
    fn order(&self) -> i32 { 0 }
    async fn on_system_prompt(&self, prompt: &mut String) {}
    fn on_agent_stream(&self, input: AgentInput, next: Next<'_>) -> AgentStream { next(input) }
    async fn on_model_call(&self, messages: &mut Vec<Msg>) {}
    async fn on_acting(&self, call: &mut ToolCallRequest) {}
}
```

`MiddlewareChain::build_agent_chain` 递归组合 `BoxStream`，对标 C# 的链式 next 委托；`tower::Layer` 风格可作为服务端场景的适配器，不作为框架内部抽象。

---

# 第五部分：核心类型 Rust 设计草案

## 5.1 消息系统（`agentscope-core/src/message/`）

```rust
// 对标 C#: Message/ContentBlock.cs —— abstract record 继承体系 → enum
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum ContentBlock {
    Text(TextBlock),
    Image(ImageBlock),
    Audio(AudioBlock),
    Video(VideoBlock),
    ToolUse(ToolUseBlock),
    ToolResult(ToolResultBlock),
    Thinking(ThinkingBlock),
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ToolResultBlock {
    pub id: String,
    pub output: Option<Value>,
    #[serde(default)]
    pub is_error: bool,
    #[serde(default)]
    pub is_suspended: bool,     // HITL 外部工具挂起信号，语义与 C# 一致
    pub name: Option<String>,
    pub metadata: Option<JsonMap>,
}

// 对标 C#: Msg + MsgBuilder
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Msg {
    pub id: String,                     // uuid v7（时序友好）
    pub name: Option<String>,
    pub role: MsgRole,                  // enum { System, User, Assistant, Tool }
    pub content: MessageContent,        // enum { Text(String), Blocks(Vec<ContentBlock>) }
    pub metadata: JsonMap,
    pub timestamp: i64,                 // Unix 毫秒
}

impl Msg {
    pub fn builder() -> MsgBuilder;             // 对标 Msg.Builder()
    pub fn text_content(&self) -> Option<&str>; // 对标 GetTextContent()
}
```

## 5.2 Agent trait 与生命周期

```rust
// 对标 C#: Agent/IAgent.cs（ICallable + IStreamable + IObservable 组合）
#[async_trait]
pub trait Agent: CallableAgent + StreamableAgent + Send + Sync {
    fn agent_id(&self) -> &str;
    fn name(&self) -> &str;
    fn description(&self) -> &str;
    fn interrupt(&self, message: Option<Msg>);   // 对标 Interrupt()
}

#[async_trait]
pub trait CallableAgent: Send + Sync {
    // 对标 Task<Msg> CallAsync(...)（注释：对标 Java Mono<Msg>）
    async fn call(&self, messages: Vec<Msg>, ctx: &RuntimeContext) -> Result<Msg>;
}

#[async_trait]
pub trait StreamableAgent: Send + Sync {
    // 对标 IAsyncEnumerable<Event>（注释：对标 Java Flux<Event>）
    fn stream_events(&self, messages: Vec<Msg>, ctx: &RuntimeContext)
        -> BoxStream<'static, Result<AgentEvent>>;
}
```

`EnhancedReActAgent`（Rust 版直接实现原生 function calling 循环）：

```rust
pub struct EnhancedReActAgent {
    model: Arc<dyn Model>,
    memory: Arc<dyn Memory>,
    toolkit: Toolkit,
    hooks: HookManager,
    middlewares: Vec<Arc<dyn AgentMiddleware>>,
    permission: Option<Arc<dyn PermissionEngine>>,   // Evaluate 保持同步
    interrupt: CancellationToken,                    // 对标 CancellationTokenSource
    max_iterations: usize,
    system_prompt: RwLock<String>,                   // 开放 setter，支持中间件注入
    state: AgentStateHandle,                         // 对标 IStateModule 三类键
}

impl EnhancedReActAgent {
    // 核心循环（原生 function calling 版）：
    // reasoning → 解析 tool_use blocks → permission 过滤 → 并发执行(JoinSet, 按
    // is_concurrency_safe 分槽) → tool_result 回填 → 继续循环 / 挂起(IsSuspended)
    // → 无 tool_use 即最终答复
    async fn react_loop(&self, msgs: &mut Vec<Msg>, ctx: &RuntimeContext)
        -> Result<Vec<AgentEvent>> { /* ... */ }
}
```

## 5.3 Tool 系统与宏

```rust
// 对标 C#: Tool/ITool.cs
#[async_trait]
pub trait Tool: Send + Sync {
    fn name(&self) -> &str;
    fn description(&self) -> &str;
    fn schema(&self) -> JsonMap;                    // 对标 GetSchema()
    fn is_external(&self) -> bool { false }         // SchemaOnlyTool 语义
    fn is_concurrency_safe(&self) -> bool { false } // 对标 IToolConcurrencySafe
    async fn execute(&self, params: JsonMap, ct: CancellationToken) -> Result<ToolOutcome>;
}

pub enum ToolOutcome {                        // 对标 ToolResult，用 enum 表达成功/失败
    Ok { result: Value },
    Fail { error: String },
    Suspended { tool: String, reason: Option<String> },   // HITL 挂起
}
```

`Toolkit` 门面聚合 `ToolRegistry + ToolGroupManager + ToolSchemaProvider + MetaToolFactory + ToolExecutor`，模块内部拆 crate module、对外一个 `Toolkit` 类型，与 C# 一致。

## 5.4 Memory / State

```rust
// 对标 C#: Memory/IMemory.cs（同步接口 + 内部锁）
pub trait Memory: Send + Sync {
    fn add(&self, msg: Msg);
    fn get_all(&self) -> Vec<Msg>;              // 返回克隆快照，内部 RwLock
    fn get_recent(&self, n: usize) -> Vec<Msg>;
    fn clear(&self);
    fn delete(&self, message_id: &str) -> bool;
}
#[async_trait]
pub trait PersistentMemory: Memory {
    async fn search(&self, query: &str, limit: usize) -> Result<Vec<Msg>>;
    async fn save(&self) -> Result<()>;
    async fn load(&self) -> Result<()>;
}

// 对标 C#: State/IAgentStateStore.cs —— 版本化 CAS 原样保留（乐观并发核心）
#[async_trait]
pub trait AgentStateStore: Send + Sync {
    async fn get(&self, key: &str) -> Result<Option<StateEntry>>;
    async fn get_versioned(&self, key: &str) -> Result<Option<VersionedState>>;
    async fn save(&self, key: &str, value: Value) -> Result<()>;
    async fn save_if_version(&self, key: &str, value: Value, expected: i64)
        -> Result<bool>;                        // false = 版本冲突
}
```

## 5.5 Model 与 Formatter 分层

保持 C# 的三层结构 **Model → Formatter → Transport**：

- `Transport` trait：`post_stream_json` / `post` / `connect_ws`，Rust 用 `reqwest` + `eventsource-stream`(SSE) + `tokio-tungstenite` 实现，替代 C# 自研 1200 行 Transport；
- `Formatter<Req, Resp, Params>`：C# 泛型接口在 Rust 中改为**每个协议一个 module**（`formatter/openai/`、`formatter/anthropic/`），由 `ModelRequest/ModelResponse` 中间层隔离——因为 Rust 的关联类型泛型 trait 会让 `Arc<dyn>` 注入复杂化，没必要 1:1 保留三参数泛型；
- `ModelRegistry`：`RwLock<HashMap<ProviderKey, Arc<dyn ModelProvider>>>` + 实例缓存，语义与 C# `GetOrCreate` 复合键一致。

---

# 第六部分：Cargo Workspace 工程结构

```
agentscope-rs/
├── Cargo.toml                        # [workspace] members, resolver=2
├── crates/
│   ├── agentscope-core/              # ← AgentScope.Core（P0 主战场）
│   │   └── src/
│   │       ├── agent/                # IAgent/AgentBase/EnhancedReActAgent/middleware/runtime_context
│   │       ├── message/              # Msg/ContentBlock/Accumulator
│   │       ├── model/                # trait Model + registry + transport/
│   │       ├── formatter/            # openai/ anthropic/ dashscope/ gemini/
│   │       ├── memory/               # memory_base/ sqlite/ long_term/ state_backed
│   │       ├── tool/                 # toolkit/ registry/ groups/ executor/ builtin/
│   │       ├── hook/ permission/ state/ session/
│   │       ├── pipeline/ workflow/ multi_agent/ plan/
│   │       ├── rag/ skill/ mcp/ a2a/ ag_ui/
│   │       ├── service/ interruption/ shutdown/ tracing/
│   │       ├── error.rs              # AgentScopeError (thiserror)
│   │       └── lib.rs
│   ├── agentscope-macros/            # proc-macro: #[tool]/#[param]（无依赖负担）
│   ├── agentscope/                   # facade crate：re-export + AgentScope::builder()（对标 DI 装配）
│   ├── agentscope-harness/           # ← AgentScope.Harness（P2）
│   ├── agentscope-tracing-otel/      # ← Tracing.OpenTelemetry（tracing + opentelemetry crate）
│   └── extensions/                   # ← 42 个扩展，逐 crate 对应（P3 按需）
│       ├── agentscope-store-redis/   # redis-rs
│       ├── agentscope-store-postgres/# sqlx
│       ├── agentscope-vector-qdrant/ # qdrant-client
│       ├── agentscope-vector-milvus/ # milvus-sdk-rust
│       ├── agentscope-sandbox-docker/# bollard
│       ├── agentscope-channel-feishu/#
│       └── ...                       # 其余按需求排期
├── examples/                         # 对标 examples/（QuickStart/GLM/HITL/StructuredOutput）
└── tests/
    ├── integration/                  # 对标 AgentScope.Integration.Tests
    └── llm-system/                   # 对标 LlmSystemTests（env 开关真实 LLM）
```

原则：
- **Core 只依赖** `tokio / futures / async-trait / async-stream / serde / serde_json / reqwest / tokio-tungstenite / thiserror / uuid / parking_lot / dashmap / tokio-util / schemars`（对标 C# Core 仅 3 依赖的克制风格，且 rusqlite 用 feature 门控 `sqlite`）；
- 每个 extension crate feature 可选，**用户装什么编译什么**（比 NuGet 更细粒度）；
- `agentscope-macros` 独立 crate，避免 proc-macro 拖累编译。

---

# 第七部分：依赖选型（NuGet → crates.io）

| 领域 | C# / NuGet | Rust crate | 说明 |
|------|-----------|-----------|------|
| 异步运行时 | TPL / async-await | `tokio` (full) | 事实标准 |
| 异步 trait | 语言内置 | `async-trait` | dyn 分发必需 |
| 流式 | `IAsyncEnumerable` | `futures` + `async-stream` | `stream!` 宏对标 yield return |
| HTTP 客户端 | HttpClient（自研 Transport） | `reqwest` (json, stream, rustls) | 替代 1200 行自研传输层 |
| WebSocket | ClientWebSocket | `tokio-tungstenite` | Gemini/实时传输 |
| SSE | 自研解析 | `eventsource-stream` | OpenAI/Anthropic 流式 |
| JSON | System.Text.Json | `serde` + `serde_json` | 完全对标 |
| JSON Schema | 自研 ToolSchemaGenerator | `schemars` + 宏生成 | 双路：宏或 derive |
| SQLite (Memory) | EF Core.Sqlite | `rusqlite`（bundled）或 `sqlx` (sqlite) | 首选 rusqlite，轻量无 async 复杂度 |
| 错误 | Exception | `thiserror`（库内）+ `anyhow`（示例） | |
| 取消 | CancellationToken | `tokio-util` CancellationToken | 语义几乎 1:1 |
| 事件 | C# event / System.Reactive | `tokio::sync::{broadcast, watch, mpsc}` | Rx 不移植 |
| 并发集合 | ConcurrentDictionary | `dashmap` | |
| 锁 | lock / SemaphoreSlim | `parking_lot` + `tokio::sync::Semaphore` | ToolExecutor 分槽并行 |
| UUID/GUID | Guid | `uuid` (v4/v7) | |
| 时间 | DateTime | `chrono` 或 `time` | |
| OTel | OpenTelemetry.* 1.11 | `tracing` + `opentelemetry` + `opentelemetry-otlp` | tracing 是 Rust 事实标准 |
| Redis | StackExchange.Redis | `redis-rs` (tokio) | |
| PostgreSQL | Npgsql | `sqlx` (postgres) | pgvector 同库 |
| MongoDB | MongoDB.Driver | `mongodb` 官方 crate | |
| Docker 沙箱 | Docker.DotNet | `bollard` | |
| Kubernetes | KubernetesClient | `kube` | |
| Git 技能仓 | LibGit2Sharp | `git2` 或调 CLI | git2 构建 зависит C 工具链，可降级为 CLI |
| Quartz/XXL-Job | Quartz / HTTP | `tokio-cron-scheduler` / 纯 HTTP | XXL-Job 本来就是 HTTP API |
| Terminal UI | Terminal.Gui | `ratatui` | P3，不首批 |
| PDF/Word | PdfPig / OpenXML | `pdf-extract` / `docx-rs` | P3，能力弱于 .NET，标注差异 |
| 测试 | xUnit | 内置 `#[test]` + `#[tokio::test]` + `assert_cmd`/`wiremock` | 见第九部分 |

---

# 第八部分：分阶段实施路线图

> 估算基准：1~2 名熟练 Rust 工程师；每阶段以"测试通过率"为出口标准（用例从 C# 测试翻译）。

## M0 —— 骨架与消息层（1~1.5 周）
- Workspace 搭建、CI（fmt/clippy/test/MSRV）、错误体系、RuntimeContext、Msg/ContentBlock/MsgRole/序列化往返测试、Accumulator
- **出口**：ContentBlock serde 往返 100% 对齐 C# JSON 形状（跨语言互操作验证）

## M1 —— 模型层（2~3 周）
- Transport（reqwest/SSE/WS）、`Model` trait、OpenAI + Anthropic + DeepSeek 三个 Formatter/Parser、ModelRegistry、GenerateOptions/ToolSchema/ResponseFormat、MockModel
- **出口**：真实 API 冒烟（env 开关）+ 流式 chunk 解析测试

## M2 —— 工具与记忆 + ReAct 核心（2~3 周）
- `agentscope-macros`（#[tool]）、Toolkit 全家（Registry/Groups/Executor 并行分槽/Suspended）、内置工具（shell/file/search）、PermissionEngine 9 步状态机、Memory(InMemory/Sqlite)、**EnhancedReActAgent 原生 function calling 循环**（含 HITL 挂起/恢复）
- **出口**：C# Tool/Permission/Memory/Agent 非流式用例翻译通过

## M3 —— 流式与扩展通道（2~3 周）
- Stream 全链路（stream_events + 中间件洋葱包裹 + Hook chunk 事件）、HookManager、MiddlewareChain、中断/恢复（CancellationToken + 状态捕获）、IStateModule + 版本化 CAS StateStore（内存/JSON 文件）、结构化输出
- **出口**：流式事件序与 C# `EnhancedReActAgentStreamingTests` 对齐

## M4 —— 协作与编排（3~4 周）
- MCP（stdio/SSE/StreamableHTTP，复用 transport 层）、Session、Plan（PlanNotebook + broadcast 事件）、Pipeline 7 节点、Workflow DAG、MultiAgent（MsgHub broadcast）、RAG（内存向量 + KnowledgeRetrievalTools）、Skill（Markdown 解析）
- **出口**：端到端示例（对标 examples/）全部可跑

## M5 —— Harness 与生态（3~4 周，P2 可与 M4 并行）
- HarnessAgent/Builder、MessageBus（mpsc+broadcast）、IFilesystem 抽象与 Local 实现、中间件包（Compaction/Transcript/WorkspaceContext 等 20 个按需挑选）、Tracing OTel crate、`agentscope` facade
- **出口**：Harness 示例（对标 AgentScope.Lab 场景）

## M6 —— 扩展按需与发布（持续）
- Redis/Postgres/Qdrant/Docker 沙箱等逐个 2~5 天；docs.rs 文档、发布 crates.io、跨语言互操作测试（与 .NET/Java 版 JSON 协议对齐）

**总计**：Core + facade ≈ **3~4 人月**；Harness + 首批 5 个扩展 ≈ **+2 人月**；全量扩展生态为长期演进。

---

# 第九部分：测试策略

1. **用例平移**：C# 54 个测试类 ~620 用例按模块逐条翻译（xUnit `[Fact]` → `#[test]`/`#[tokio::test]`）；自建测试替身（MockModel/TestStdioMcpServer）保持"无 Mock 框架"风格，用 Rust 结构体直接实现 trait；
2. **跨语言 JSON 一致性测试**：对 Msg/ContentBlock/AgentEvent/状态快照，用 `.NET` 版序列化样本做 golden file，Rust serde 必须往返一致 —— 保证三语言（Java/.NET/Rust）会话记录可互迁；
3. **集成测试**：对标 `IntegrationTests`（SQLite 临时文件、跨 Agent 记忆共享）；
4. **LLM 系统测试**：对标 `LlmSystemTests`（`AGENTSCOPE_RUN_EXTERNAL_TESTS` 环境变量门控，DashScope→DeepSeek→OpenAI 兼容回退），Rust 侧用 `dotenvy`；
5. **CI 门禁**：`cargo fmt --check`、`cargo clippy -D warnings`、`cargo test`、`cargo doc` 无警告；MSRV 锁定（建议 1.85 / edition 2024）。

---

# 第十部分：裁剪建议与风险清单

## 10.1 明确不移植 / 降级项

| 项 | 决定 | 理由 |
|----|------|------|
| System.Reactive 范式 | 不移植，统一 Stream | 双范式是 review 发现的问题 #7，Rust 生态以 Stream 为准 |
| 旧版 ReActAgent（文本协议） | 不移植，仅保留兼容模式开关 | 问题 #2，重复代码 |
| Uno 桌面 GUI | 不移植 | .NET 版自身 XAML 待修；Rust GUI（egui/iced）生态不成熟，投入产出比低 |
| TUI | 降级为 P3（ratatui 精简版） | 有价值但非框架核心 |
| Spring-style DI Starter | 改为 builder facade + tower 适配 | Rust 无 DI 容器惯用法 |
| JPA 式持久化 | sqlx 手写 SQL | 无 ORM 等价物必要 |

## 10.2 风险与缓解

| # | 风险 | 等级 | 缓解 |
|---|------|------|------|
| 1 | proc-macro（#[tool]）开发复杂度高 | 高 | M2 预留专门时间；先支持函数工具，结构体工具走 `impl Tool` 手写；Schema 用 schemars 兜底 |
| 2 | `async_trait` 的 BoxStream 生命周期（'static 约束）在自引用场景受阻 | 中 | 事件一律 owned + `Arc`；流式方法返回 `BoxStream<'static>` 并在内部克隆所需状态 |
| 3 | 向量/存储扩展的 Rust 客户端成熟度不均（Milvus/Elastic 弱于 .NET） | 中 | P3 逐个验证，REST API 直连兜底（C# 版多数扩展本来就是纯 HTTP 实现，同法可抄） |
| 4 | 跨语言协议漂移（Rust 与 Java/.NET 会话 JSON 不兼容） | 中 | 第九部分 golden file 测试强制对齐 |
| 5 | 单人维护风险 / 团队 Rust 经验 | 中 | 接口层保持与 C# 文档双语对照（延续现有"对应 C#: xxx"注释传统），降低维护门槛 |
| 6 | 原生 function calling 主循环与上游 Java 版行为差异 | 低 | 循环语义以 Java 上游为基准 + 保留文本协议兼容模式；差异记录在 docs |

---

## 附：一页决策摘要

- **可行性**：Core 架构边界清晰、依赖克制，Rust 移植**高度可行**；成本集中在运行时特性替换（async 流 / AsyncLocal / 反射 / 事件），均有成熟 crate 对应。
- **最大改进机会**：借移植把主循环升级为原生 function calling（C# 版 review 发现的能力缺口）。
- **首版范围**：P0 Core 主干 + `#[tool]` 宏 + OpenAI/Anthropic/DeepSeek + SQLite Memory + MCP + OTel ≈ **3~4 人月**。
- **核心口号**：**接口 1:1、实现地道、借移植升级、扩展按需**。
