# RandSystem1

**Know When Your AI Is Better Than Random.**

**先证明比乱猜强，再谈智能决策。**

[简体中文](#简体中文) | [English](#english) | [日本語](#日本語) | [Español](#español)

## 简体中文

RandSystem1 是一个开源、模型无关的 AI 决策评估框架，而不是新的 AI 模型。其长期目标是通过 **Random → Evaluate → Decide（随机基准 → 统计评估 → 决策融合）**，科学地比较模型、基线与决策策略。

**当前状态：Iteration 004（模型互补性，待验收）。** 已实现 .NET 10 随机基线和统计评估，新增多模型联合错误、预测分歧、错误相关性、Oracle 事后理论上限。融合、回测、真实 Jev/Laya 接入尚未实现，请参阅 [开发计划](docs/DEVELOPMENT_PLAN.md)。

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

**Status: Iteration 004 model complementarity (pending review).** The .NET 10 statistics engine now supports aligned multi-model joint errors, disagreement, error association and hindsight Oracle upper bounds. Deployable fusion, backtesting and real Jev/Laya adapters remain **planned, not implemented**.

To run, install .NET SDK 10+ and use the commands in the Chinese quick-start section. API requests use JSON. The mock model explicitly reports isSimulation=true; never interpret its probabilities as measured real-world accuracy. No live trading is supported.

Documentation: [Architecture](docs/ARCHITECTURE.md) · [Statistical methods](docs/STATISTICAL_METHODS.md) · [Model protocol](docs/MODEL_PROTOCOL.md) · [API](docs/API.md) · [Roadmap](docs/DEVELOPMENT_PLAN.md).

## 日本語

RandSystem1 は特定のモデルに依存しない、オープンソースの AI 意思決定評価フレームワークです。新しい AI モデルではありません。目標は **Random → Evaluate → Decide**（ランダム基準 → 統計的評価 → 意思決定の統合）です。

**現在は Iteration 004（モデル相補性、レビュー待ち）です。** .NET 10 の統計評価に加え、複数モデルの共同正解・誤答、予測の不一致、誤りの相関、Oracle 理論上限を実装しました。融合、バックテスト、Jev/Laya 接続は未実装です。

実行には .NET SDK 10 以上が必要です。上記のクイックスタートのコマンドを使ってください。モックの出力は isSimulation=true と明示され、実モデルの予測精度を証明するものではありません。実資金による取引はサポートしていません。

## Español

RandSystem1 es un marco de código abierto e independiente de modelos para evaluar decisiones de IA; **no es un nuevo modelo de IA**. Su objetivo es **Random → Evaluate → Decide**: establecer referencias aleatorias, evaluar estadísticamente y comprobar la utilidad real de la combinación de modelos.

**Estado actual: Iteración 004 (complementariedad de modelos; pendiente de revisión).** Incluye evaluación estadística, errores conjuntos, desacuerdo, correlación de fallos y límites teóricos Oracle de varios modelos. La fusión, los backtests y la conexión Jev/Laya siguen pendientes.

Se requiere .NET SDK 10 o superior. Utilice los comandos de inicio rápido anteriores. El modelo de prueba devuelve isSimulation=true; sus probabilidades no constituyen evidencia de precisión real. No se permiten operaciones con dinero real.

Consulte [Arquitectura](docs/ARCHITECTURE.md), [Métodos estadísticos](docs/STATISTICAL_METHODS.md) y [Plan de desarrollo](docs/DEVELOPMENT_PLAN.md).

## License

Released under the [MIT License](LICENSE). The project owner may revise this choice before wider adoption.
