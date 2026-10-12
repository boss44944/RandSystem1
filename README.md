# RandSystem1

**Know When Your AI Is Better Than Random.**

**先证明比乱猜强，再谈智能决策。**

[简体中文](#简体中文) | [English](#english) | [日本語](#日本語) | [Español](#español)

## 简体中文

RandSystem1 是一个开源、模型无关的 AI 决策评估框架，而不是新的 AI 模型。其长期目标是通过 **Random → Evaluate → Decide（随机基准 → 统计评估 → 决策融合）**，科学地比较模型、基线与决策策略。

**当前状态：Iteration 003（统计评估，待验收）。** 已有 .NET 10 基础工程、随机基线，以及分类评分、Brier/Log Loss、Skill、ECE、Monte Carlo 和受约束的统计推断 API。模型互补性、融合、回测、真实 Jev/Laya 接入尚未实现，请参阅 [开发计划](docs/DEVELOPMENT_PLAN.md)。

### 快速开始

需要 .NET SDK 10.0+。

    dotnet restore RandSystem1.slnx
    dotnet build RandSystem1.slnx
    dotnet test RandSystem1.slnx
    dotnet run --project src/RandSystem1.Api --urls http://127.0.0.1:5078

服务启动后：

    curl http://127.0.0.1:5078/api/health
    curl http://127.0.0.1:5078/api/models
    curl -X POST 'http://127.0.0.1:5078/api/models/predict?modelId=mock-uniform' -H 'Content-Type: application/json' -d '{"requestId":"example-1","taskType":"choice","options":["A","B","C"],"context":{"source":"demo"}}'

**重要：** mock-uniform 返回均匀概率，并将 isSimulation 设为 true。它不是 Jev/Laya 等真实模型的预测，不能作为“模型优于随机”的证据。第一版不提供真实资金交易能力。

完整原始需求：[项目开发任务书](docs/PROJECT_REQUIREMENTS.md)。阅读：[系统架构](docs/ARCHITECTURE.md) · [统计方法](docs/STATISTICAL_METHODS.md) · [模型协议](docs/MODEL_PROTOCOL.md) · [API](docs/API.md) · [迭代计划](docs/DEVELOPMENT_PLAN.md)。

## English

RandSystem1 is an open-source, model-agnostic evaluation and decision framework, **not another AI model**. Its roadmap is **Random → Evaluate → Decide**: establish fair random baselines, measure out-of-sample skill, and test decision fusion rather than assuming it works.

**Status: Iteration 003 statistical evaluation (pending review).** The .NET 10 baseline engine now includes labeled classification metrics, Brier/Log Loss, skill, ECE, seeded Monte Carlo null distributions, paired bootstrap, time-series block bootstrap and guarded binomial inference. Complementarity, fusion, backtesting and real Jev/Laya adapters remain **planned, not implemented**.

To run, install .NET SDK 10+ and use the commands in the Chinese quick-start section. API requests use JSON. The mock model explicitly reports isSimulation=true; never interpret its probabilities as measured real-world accuracy. No live trading is supported.

Documentation: [Architecture](docs/ARCHITECTURE.md) · [Statistical methods](docs/STATISTICAL_METHODS.md) · [Model protocol](docs/MODEL_PROTOCOL.md) · [API](docs/API.md) · [Roadmap](docs/DEVELOPMENT_PLAN.md).

## 日本語

RandSystem1 は特定のモデルに依存しない、オープンソースの AI 意思決定評価フレームワークです。新しい AI モデルではありません。目標は **Random → Evaluate → Decide**（ランダム基準 → 統計的評価 → 意思決定の統合）です。

**現在は Iteration 003（統計評価、レビュー待ち）です。** .NET 10 の乱数基準に加え、分類指標、Brier/Log Loss、Skill、ECE、Monte Carlo、対応のある比較と時系列ブロック Bootstrap の API を実装しました。モデル融合、バックテスト、実際の Jev/Laya 接続は今後の開発対象です。

実行には .NET SDK 10 以上が必要です。上記のクイックスタートのコマンドを使ってください。モックの出力は isSimulation=true と明示され、実モデルの予測精度を証明するものではありません。実資金による取引はサポートしていません。

## Español

RandSystem1 es un marco de código abierto e independiente de modelos para evaluar decisiones de IA; **no es un nuevo modelo de IA**. Su objetivo es **Random → Evaluate → Decide**: establecer referencias aleatorias, evaluar estadísticamente y comprobar la utilidad real de la combinación de modelos.

**Estado actual: Iteración 003 (evaluación estadística; pendiente de revisión).** Incluye .NET 10, líneas base reproducibles, métricas de clasificación y probabilidades, Skill, ECE, Monte Carlo y comparaciones emparejadas, con Bootstrap por bloques para series temporales. La fusión, los backtests y la conexión real con Jev/Laya siguen pendientes.

Se requiere .NET SDK 10 o superior. Utilice los comandos de inicio rápido anteriores. El modelo de prueba devuelve isSimulation=true; sus probabilidades no constituyen evidencia de precisión real. No se permiten operaciones con dinero real.

Consulte [Arquitectura](docs/ARCHITECTURE.md), [Métodos estadísticos](docs/STATISTICAL_METHODS.md) y [Plan de desarrollo](docs/DEVELOPMENT_PLAN.md).

## License

Released under the [MIT License](LICENSE). The project owner may revise this choice before wider adoption.
