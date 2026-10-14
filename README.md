# RandSystem1

**Know When Your AI Is Better Than Random.**

**先证明比乱猜强，再谈智能决策。**

[简体中文](#简体中文) | [English](#english) | [日本語](#日本語) | [Español](#español)

## 简体中文

RandSystem1 是一个**开源、模型无关的 AI 决策统计评估与融合框架**，不是大语言模型或模型训练平台。

**Random → Evaluate → Decide**：建立可复现的随机基准，用独立样本与统计方法验证预测是否优于随机，再研究有约束的模型融合与决策。未经验证的优势不会被当作事实。

### 已实现

截至 **Iteration 007**，仓库已包含以下可运行的 .NET 10 组件：

- **随机基线**：Uniform、Dirichlet、训练集 Majority/Prior、随机集成和固定种子重放。
- **统计评估**：Accuracy、Balanced Accuracy、Precision/Recall/F1、Brier、Log Loss、Skill Score、ECE、Monte Carlo，以及有适用性约束的配对检验和区块 Bootstrap。
- **模型互补性**：预测分歧、联合错误、错误相关性和事后 Oracle *理论上限*（不能作为实际可达到的融合准确率）。
- **决策融合**：等权、静态权重、历史验证表现权重、分段动态权重；缺失、异常、风险门限和证据不足时支持 **ABSTAIN**。
- **模拟回测**：按时间前进的 Walk-Forward 分割、标签窗口 Purge、Embargo、手续费/滑点、资金曲线、收益与回撤。
- **股票策略演示**：固定种子生成的**完全合成行情**，对比规则模型、规则融合、随机交易、买入持有与不交易；未来五交易日 UP/DOWN 标签与结果可复现。

**尚未实现**：可用的真实 Jev/Laya 模型适配器、生产级持久化和长时间异步任务。模拟预测不是模型真实准确率，历史或合成回测不保证未来收益。**不提供真实资金自动交易或下单功能。**

### 快速开始

需要 [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)。

    dotnet restore RandSystem1.slnx
    dotnet build RandSystem1.slnx -c Release --warnaserror
    dotnet test RandSystem1.slnx -c Release
    dotnet run --project src/RandSystem1.Api --urls http://127.0.0.1:5078

在另一个终端调用 API：

    curl http://127.0.0.1:5078/api/health
    curl http://127.0.0.1:5078/api/models
    curl -X POST http://127.0.0.1:5078/api/random/generate -H 'Content-Type: application/json' -d '{"baseline":"uniform","options":["UP","DOWN"],"seed":42,"count":2,"samplesPerModel":3}'
    curl 'http://127.0.0.1:5078/api/examples/stock/compare?seed=42'

最后一个接口只生成模拟数据并比较模拟策略，**不会访问行情服务或执行交易**。输出含 isSimulation、种子、数据版本、模拟资金曲线和不确定性说明。完整固定样本见 [股票模拟报告](examples/stock-demo-007-seed42.json)。

### 文档

[架构](docs/ARCHITECTURE.md) · [统计方法](docs/STATISTICAL_METHODS.md) · [模型协议](docs/MODEL_PROTOCOL.md) · [API](docs/API.md) · [开发路线图](docs/DEVELOPMENT_PLAN.md) · [需求与边界](docs/PROJECT_REQUIREMENTS.md)

## English

RandSystem1 is an **open-source, model-agnostic framework for evaluating and combining AI decisions**, not a language model or a training platform.

**Random → Evaluate → Decide:** establish reproducible random baselines, test predictive skill on properly separated observations, and only then evaluate decision fusion under explicit risk constraints.

**Implemented through Iteration 007:** fixed-seed random/Dirichlet/training-only majority and prior baselines; classification, probability, calibration and Monte Carlo metrics; paired and time-aware statistical tests with stated assumptions; model disagreement and hindsight Oracle bounds; equal/static/validation-weighted/dynamic fusion with **ABSTAIN**; chronological walk-forward paper backtesting with purge/embargo and transaction costs; and a reproducible **fully synthetic** five-trading-session UP/DOWN strategy comparison.

**Not yet implemented:** real Jev/Laya adapters, production persistence and background job infrastructure. Synthetic results are *not* real model accuracy, Oracle is an upper bound rather than a deployable rule, and no historical backtest guarantees future returns. **No live-money trading or order execution is provided.**

Install .NET SDK 10+, then use the build/test/run commands in [快速开始](#快速开始). The stock comparison is available at GET /api/examples/stock/compare?seed=42 and is explicitly simulation only.

See [Architecture](docs/ARCHITECTURE.md), [Statistical methods](docs/STATISTICAL_METHODS.md), [Model protocol](docs/MODEL_PROTOCOL.md), [API](docs/API.md) and [Roadmap](docs/DEVELOPMENT_PLAN.md).

## 日本語

RandSystem1 は、**オープンソースでモデル非依存の AI 意思決定評価・統合フレームワーク**です。新しい言語モデルでも学習プラットフォームでもありません。

**Random → Evaluate → Decide**：再現可能なランダム基準を作り、独立した評価データで予測能力を検証し、その結果に基づいて制約付きの意思決定統合を評価します。

**Iteration 007 まで実装済み**：固定シードのランダム基準（Uniform/Dirichlet/学習データのみを使った事前分布）、分類・確率・校正の統計評価、Monte Carlo と条件付きの統計検定、モデルの不一致と事後的 Oracle 上限、ABSTAIN 対応の融合、時系列 Walk-Forward・Purge・Embargo を使った模擬バックテスト、完全合成データによる５取引日先の UP/DOWN 比較です。

実際の Jev/Laya 接続や本番用の永続化は未実装です。モック出力は実際の AI 精度を証明せず、**実資金での取引・発注機能もありません**。実行には .NET SDK 10 以上が必要です。上記の [クイックスタート](#快速开始) を参照してください。

## Español

RandSystem1 es un **marco abierto e independiente de modelos para evaluar y combinar decisiones de IA**; no es un modelo nuevo ni una plataforma de entrenamiento.

**Random → Evaluate → Decide:** establecer referencias aleatorias reproducibles, medir la capacidad predictiva con datos separados y, después, comprobar estrategias de combinación sujetas a límites de riesgo.

**Implementado hasta la iteración 007:** referencias Uniform/Dirichlet y prior/majority ajustadas solo con entrenamiento; métricas de clasificación, probabilidades y calibración; Monte Carlo y pruebas estadísticas con supuestos explícitos; desacuerdo entre modelos y límite Oracle retrospectivo; combinación con ABSTAIN; backtesting cronológico Walk-Forward con purga, embargo y costes; y un ejemplo **totalmente sintético** de predicción UP/DOWN a cinco sesiones.

La conexión real con Jev/Laya, la persistencia de producción y los trabajos en segundo plano aún están pendientes. Una simulación no demuestra la precisión de una IA real ni garantiza beneficios futuros. **No se ejecutan operaciones con dinero real.** Se requiere .NET SDK 10 o superior; consulte el inicio rápido anterior.

Documentación: [Arquitectura](docs/ARCHITECTURE.md) · [Métodos estadísticos](docs/STATISTICAL_METHODS.md) · [API](docs/API.md) · [Plan](docs/DEVELOPMENT_PLAN.md).

## License

Released under the [MIT License](LICENSE).
