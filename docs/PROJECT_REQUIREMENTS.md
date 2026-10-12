# RandSystem1 项目开发任务书

## 0. AI Agent 角色与执行要求

你是一名具有以下能力的高级 AI 软件工程师：

- .NET 10 / C# 软件架构师
- 概率统计与数学算法工程师
- AI Agent 系统架构师
- 机器学习模型评估工程师
- 量化投资回测工程师
- 自动化测试工程师

现在需要开发一个名为 **RandSystem1** 的开源项目。

项目口号：

**Know When Your AI Is Better Than Random.**

中文：

**先证明比乱猜强，再谈智能决策。**

请从需求分析、技术设计、代码实现、自动化测试到项目文档，完整推进开发工作。

### 执行原则

1. 不要只给出架构设计，必须实际编写代码。
2. 不要一次性实现全部功能，按照迭代计划逐步完成。
3. 每次迭代必须产生可运行、可测试的成果。
4. 核心数学算法必须有单元测试。
5. 不允许伪造真实模型的预测结果。
6. 不允许把随机概率当成真实预测概率。
7. 不允许使用未来数据优化历史决策。
8. 不允许假设模型融合必然提高准确率。
9. 优先采用成熟、简单、可维护的技术方案。
10. 每次迭代完成后更新文档并提交 Git。
11. 如果遇到环境限制，应记录问题并继续完成不受影响的任务。
12. 所有实验必须可复现，并明确区分模拟结果与真实测试结果。

---

# 一、项目定位

## 1.1 项目名称

**RandSystem1**

项目英文全称：

Random Baselines, Statistical Evaluation and Decision Fusion for AI Decision Systems.

项目中文名称：

**AI 决策系统随机基准、统计评估与智能融合框架。**

GitHub 仓库建议名称：

`rand-system1`

.NET 命名空间：

`RandSystem1`

CLI 名称：

`rands1`

## 1.2 核心理念

RandSystem1 不是一个新的 AI 模型。

它是一个独立于具体模型的决策评估和融合框架。

其核心流程：

```text
Random → Evaluate → Decide
```

即：

```text
随机基准 → 统计评估 → 决策融合
```

系统需要解决三个核心问题：

**Random：模型是否真的比随机猜测更好？**

**Evaluate：模型的预测能力是否真实、稳定、可靠？**

**Decide：如何利用多个模型的历史表现，做出更合理的最终决策？**

## 1.3 支持的模型

框架必须采用模型无关设计。

支持：

- Jev
- Laya
- 其他 System 1 决策模型
- 大语言模型
- 传统机器学习分类器
- 规则引擎
- 用户自定义决策器
- RandSystem1 内置随机模型

只要求模型能够提供标准化的决策结果。

注意：System 1 是项目关注的应用方向，不是严格限定的模型技术类型。

---

# 二、项目目标

RandSystem1 应具备以下能力。

### 目标 A：建立随机决策基准

能够针对任意 N 选一决策任务，生成随机预测结果。

通过蒙特卡洛模拟，建立随机决策的成绩分布。

### 目标 B：评估模型真实能力

判断模型是否显著优于随机选择、多数类别预测和历史先验等简单基线。

### 目标 C：评估概率可靠性

不仅比较预测是否正确，还要判断输出概率是否与真实结果匹配。

### 目标 D：分析模型互补性

分析多个模型是否经常犯相同错误。

判断低准确率模型是否仍然具有组合价值。

### 目标 E：智能决策融合

根据模型的历史表现、预测概率、错误相关性和业务风险，组合多个模型的预测。

### 目标 F：允许拒绝决策

当模型缺乏足够优势时，支持输出：

`ABSTAIN`

而不是强制选择某个答案。

### 目标 G：支持历史回测

通过历史数据验证：

- 单模型策略
- 多模型策略
- 随机策略
- 简单规则策略
- 拒绝决策策略

之间的实际差异。

---

# 三、技术栈

优先采用以下技术：

| 类型 | 技术 |
|---|---|
| 开发语言 | C# |
| 运行时 | .NET 10 |
| Web API | ASP.NET Core |
| 数据库 | PostgreSQL |
| 数据访问 | Dapper 或 EF Core |
| 单元测试 | xUnit |
| 日志 | Serilog |
| API 文档 | OpenAPI |
| 部署 | Docker Compose |
| 命令行 | System.CommandLine 或成熟替代方案 |
| 数据交换 | JSON |
| 项目文档 | Markdown |

要求：

- 核心算法不依赖数据库。
- 核心算法不依赖 ASP.NET Core。
- 核心算法可以作为独立 NuGet 包使用。
- 支持依赖注入。
- 支持异步模型调用。
- 支持取消任务。
- 支持固定随机种子。
- 支持 CPU 环境运行。
- 第一版不需要前端。
- 第一版不需要 Kubernetes。
- 第一版不需要消息队列。
- 第一版不需要微服务架构。

优先采用模块化单体架构。

---

# 四、项目结构

建议使用：

```text
RandSystem1/
│
├── src/
│   │
│   ├── RandSystem1.Abstractions/
│   │   ├── Models/
│   │   ├── Requests/
│   │   ├── Responses/
│   │   └── Interfaces/
│   │
│   ├── RandSystem1.Core/
│   │   ├── Decisions/
│   │   ├── Policies/
│   │   └── Validation/
│   │
│   ├── RandSystem1.Random/
│   │   ├── Uniform/
│   │   ├── Dirichlet/
│   │   ├── Majority/
│   │   ├── Prior/
│   │   └── Sampling/
│   │
│   ├── RandSystem1.Statistics/
│   │   ├── Metrics/
│   │   ├── MonteCarlo/
│   │   ├── Bootstrap/
│   │   ├── HypothesisTests/
│   │   └── Calibration/
│   │
│   ├── RandSystem1.Evaluation/
│   │   ├── Baselines/
│   │   ├── ModelComparison/
│   │   ├── Complementarity/
│   │   └── Reports/
│   │
│   ├── RandSystem1.Fusion/
│   │   ├── EqualWeight/
│   │   ├── StaticWeight/
│   │   ├── DynamicWeight/
│   │   ├── ModelSelection/
│   │   └── Abstention/
│   │
│   ├── RandSystem1.Backtesting/
│   │   ├── Engine/
│   │   ├── Strategies/
│   │   ├── Metrics/
│   │   └── Reports/
│   │
│   ├── RandSystem1.Infrastructure/
│   │   ├── Persistence/
│   │   ├── ModelAdapters/
│   │   └── Logging/
│   │
│   ├── RandSystem1.Api/
│   │
│   └── RandSystem1.Cli/
│
├── tests/
│   ├── RandSystem1.UnitTests/
│   ├── RandSystem1.IntegrationTests/
│   └── RandSystem1.StatisticalTests/
│
├── examples/
│   ├── RandomBaseline/
│   ├── ModelComparison/
│   ├── ModelFusion/
│   ├── StockTrading/
│   └── AgentRouting/
│
├── docs/
│   ├── ARCHITECTURE.md
│   ├── STATISTICAL_METHODS.md
│   ├── MODEL_PROTOCOL.md
│   ├── API.md
│   ├── BACKTESTING.md
│   ├── DEVELOPMENT_PLAN.md
│   └── iterations/
│
├── README.md
├── docker-compose.yml
└── RandSystem1.sln
```

允许根据实际需要调整目录，但必须保持职责清晰。

---

# 五、统一决策模型协议

所有外部模型通过统一接口接入。

建议：

```csharp
public interface IDecisionModel
{
    string ModelId { get; }

    Task<DecisionPrediction> PredictAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default);
}
```

### 输入示例

```json
{
  "requestId": "001",
  "taskType": "stock_direction",
  "options": [
    "UP",
    "DOWN"
  ],
  "context": {
    "symbol": "DEMO",
    "horizonDays": 5
  }
}
```

### 输出示例

```json
{
  "requestId": "001",
  "modelId": "jev",
  "modelVersion": "1.0",
  "probabilities": {
    "UP": 0.72,
    "DOWN": 0.28
  },
  "predictedOption": "UP",
  "latencyMs": 15.2
}
```

要求：

1. 概率必须位于 `[0,1]`。
2. 概率总和必须为 1，允许合理浮点误差。
3. 必须覆盖全部候选项。
4. 不允许出现 NaN 或 Infinity。
5. 必须记录模型版本。
6. 必须记录预测时间。
7. 必须记录任务类型。
8. 支持模型调用失败。
9. 支持模型超时。
10. 支持缺失预测结果。

外部模型适配器必须与核心算法解耦。

如果无法确认 Jev、Laya 的真实接口，先实现 Mock Adapter。

不得编造不存在的模型 API。

---

# 六、Random：随机基准模块

实现以下基线。

## 6.1 Uniform Random

对于 N 个候选项：

\[
P_i=\frac{1}{N}
\]

支持：

- 固定均匀概率分布。
- 根据均匀分布随机选择答案。

必须区分概率预测和随机抽样。

## 6.2 Dirichlet Random

使用 Dirichlet 分布随机生成概率向量。

例如：

```json
{
  "type": "Dirichlet",
  "alpha": 1.0,
  "seed": 42
}
```

支持配置浓度参数。

必须保证概率分布合法。

## 6.3 Majority Baseline

始终预测历史训练数据中出现频率最高的类别。

用于识别类别不平衡问题。

## 6.4 Prior Baseline

根据历史类别比例生成概率分布。

例如：

```json
{
  "UP": 0.65,
  "DOWN": 0.35
}
```

历史先验只能使用训练区间的数据计算。

## 6.5 Random Ensemble

支持一次生成多个独立随机预测器。

例如：

```json
{
  "baseline": "Dirichlet",
  "count": 10,
  "seed": 42
}
```

用于实现最初提出的实验：

**先生成 10 个随机决策模型，再将 Jev、Laya 等真实模型加入同一评估体系。**

要求：

- 所有模型使用相同测试题。
- 所有模型采用相同评分规则。
- 支持匿名模型排名。
- 支持比较随机平均成绩。
- 支持比较随机最佳成绩。
- 明确区分随机平均值和多次随机实验最大值。
- 防止因挑选最佳随机结果产生统计偏差。

---

# 七、Evaluate：模型评估模块

## 7.1 分类指标

实现：

- Accuracy
- Balanced Accuracy
- Precision
- Recall
- F1 Score
- Confusion Matrix

## 7.2 概率指标

实现：

- Brier Score
- Log Loss
- Expected Calibration Error
- Reliability Diagram

## 7.3 Skill Score

使用：

\[
Skill=1-\frac{Loss_{model}}{Loss_{baseline}}
\]

其中：

- Skill > 0：模型优于基线。
- Skill = 0：模型与基线相同。
- Skill < 0：模型劣于基线。

支持不同基线。

## 7.4 Monte Carlo Simulation

支持重复随机实验。

例如：

```json
{
  "iterations": 10000,
  "baseline": "UniformRandom",
  "seed": 42
}
```

输出：

- 平均准确率
- 标准差
- 置信区间
- 分位数
- 模型排名
- 模型成绩在随机分布中的位置

对于分类任务，能够计算模型相对于随机基线的经验尾部概率。

## 7.5 Hypothesis Testing

实现适用的统计检验：

- Binomial Test
- Permutation Test
- Bootstrap Confidence Interval
- Paired Bootstrap

要求：

- 正确区分独立样本与配对样本。
- 不得错误使用独立同分布假设。
- 对时间序列支持 Block Bootstrap。
- 对多模型比较考虑多重检验。
- 报告统计显著性和实际效果大小。

## 7.6 无标签评估

支持没有真实答案的场景。

可以计算：

- 模型输出熵
- 与均匀分布的距离
- Jensen-Shannon Divergence
- 输入扰动稳定性
- 选项顺序敏感性
- 多模型分歧率

必须明确：

**没有真实标签时，不能仅凭模型输出与随机分布的距离，证明模型准确率高于随机猜测。**

---

# 八、模型互补性分析

这是项目的重要特色。

假设：

Jev 准确率 70%。

Laya 准确率 60%。

系统不能简单按照 70:60 加权。

必须分析两者是否存在互补性。

计算：

1. 两个模型同时正确的比例。
2. 两个模型同时错误的比例。
3. Jev 正确而 Laya 错误的比例。
4. Jev 错误而 Laya 正确的比例。
5. 错误相关性。
6. 预测分歧率。
7. Oracle 理论上限。

示例：

```text
Jev正确 + Laya正确 = 50%
Jev正确 + Laya错误 = 20%
Jev错误 + Laya正确 = 10%
Jev错误 + Laya错误 = 20%
```

则：

```text
Jev准确率 = 70%

Laya准确率 = 60%

至少一个模型正确 = 80%
```

80% 是理论 Oracle 上限，不是可直接达到的融合准确率。

需要通过独立验证数据学习实际融合策略。

---

# 九、Decide：决策融合模块

实现以下策略。

## 9.1 Equal Weight

简单平均模型概率。

## 9.2 Static Weight

用户手动设置权重。

## 9.3 Performance Weight

根据历史表现计算权重。

优先考虑：

- Brier Score
- Log Loss
- 样本量
- 置信区间
- 近期表现

不能直接将历史准确率归一化后视为最佳权重。

## 9.4 Dynamic Weight

支持根据任务类型和历史表现动态选择模型。

例如：

```text
股票上涨趋势：
    Jev权重较高

股票下跌趋势：
    Laya权重较高

震荡行情：
    降低交易频率
```

以上只是策略示例。

真实权重必须通过样本外验证确定。

## 9.5 Decision Gate

根据综合预测和业务风险决定是否执行。

输出：

```json
{
  "decision": "ABSTAIN",
  "confidence": 0.61,
  "reason": "Insufficient validated advantage"
}
```

支持：

- EXECUTE
- ABSTAIN
- REJECT
- REVIEW

注意：

模型置信度、预测准确率和业务决策可靠性不是同一个概念。

## 9.6 Risk Policy

支持业务层定义：

- 最低预期收益
- 最大允许损失
- 最低模型可靠性
- 最大风险暴露
- 是否允许随机探索

高风险操作默认禁止随机探索。

---

# 十、股票投资示例

股票投资作为首个完整的业务演示。

目标：

验证 Jev、Laya 和其他模型的组合策略，是否真正优于随机决策和简单投资策略。

## 10.1 预测任务

预测未来五个交易日的涨跌方向。

```text
UP
DOWN
```

## 10.2 历史表现数据库

记录：

- 股票代码
- 预测日期
- 模型 ID
- 模型版本
- 预测概率
- 实际结果
- 市场状态
- 交易成本
- 策略收益

## 10.3 交易策略

实现：

- JevOnly
- LayaOnly
- EqualWeight
- DynamicWeight
- RandomTrading
- BuyAndHold
- NoTrade

## 10.4 收益指标

实现：

- 预测准确率
- 交易胜率
- 平均盈利
- 平均亏损
- 盈亏比
- 预期净收益
- 累计收益
- 年化收益
- 最大回撤
- Sharpe Ratio
- 交易次数
- 换手率

## 10.5 随机交易对照

随机交易必须保持与真实策略可比的条件：

- 相同股票池。
- 相同测试期间。
- 相同持仓期限。
- 相同交易成本。
- 相同风险约束。
- 尽可能匹配交易频率和仓位暴露。

避免因为随机策略的风险暴露不同而产生误导性比较。

## 10.6 最终决策

综合考虑：

```text
Jev预测
    +
Laya预测
    +
历史预测质量
    +
模型互补性
    +
随机基线检验
    +
预期收益
    +
交易成本
    +
风险约束
    ↓
最终决策
```

输出：

```json
{
  "symbol": "DEMO",
  "decision": "ABSTAIN",
  "modelPredictions": {
    "jev": 0.72,
    "laya": 0.65
  },
  "combinedProbability": 0.69,
  "baselineSkill": 0.08,
  "expectedNetReturn": 0.002,
  "reason": "Predictive advantage is not sufficiently established"
}
```

此数据仅用于演示接口。

第一版禁止真实资金交易。

---

# 十一、历史回测

必须实现 Walk-Forward Validation。

流程：

```text
训练数据
    ↓
训练或校准模型
    ↓
验证数据
    ↓
选择融合策略
    ↓
未来测试数据
    ↓
统计真实表现
    ↓
窗口向前滚动
```

要求：

1. 严格按照时间顺序。
2. 禁止未来信息泄漏。
3. 禁止使用测试数据调整当前预测。
4. 处理预测窗口重叠。
5. 支持 Purged / Embargo 时间切分。
6. 记录每次实验的随机种子。
7. 记录模型版本。
8. 记录策略参数。
9. 考虑手续费和滑点。
10. 考虑幸存者偏差。
11. 支持样本外评估。
12. 不允许把历史回测收益当成未来收益保证。

---

# 十二、API 设计

第一版提供：

```http
POST /api/random/generate

POST /api/models/predict

POST /api/evaluation/run

POST /api/evaluation/monte-carlo

POST /api/evaluation/complementarity

POST /api/fusion/predict

POST /api/backtest/run

GET /api/backtest/{id}

GET /api/models

GET /api/health
```

长时间任务采用异步执行。

支持：

- 任务 ID
- 运行状态
- 错误信息
- 执行耗时
- 结果查询

---

# 十三、CLI

提供命令行工具。

例如：

```bash
rands1 random --options 3 --count 10000

rands1 evaluate --dataset test.json

rands1 compare --models jev,laya

rands1 fusion --strategy weighted

rands1 backtest --config backtest.json
```

命令必须与实际实现一致。

不要在 README 中提供尚未实现的命令。

---

# 十四、测试标准

必须实现以下测试。

### 随机测试

- 均匀随机长期频率符合理论预期。
- Dirichlet 生成合法概率。
- 固定随机种子可复现。
- MajorityBaseline 正确处理类别不平衡。

### 统计测试

- 完美预测 Brier Score 为 0。
- 错误且过度自信的预测得到更高损失。
- Monte Carlo 结果符合理论分布。
- 统计检验能够识别明显优于随机的模型。
- 无优势模型不会被系统稳定误判为具有优势。

### 互补性测试

- 两个完全相同的模型没有额外互补性。
- 两个错误模式不同的模型能够被识别。
- Oracle 上限计算正确。

### 融合测试

- 等权融合计算正确。
- 动态权重不使用未来数据。
- 模型调用失败能够降级。
- 无优势时能够输出 ABSTAIN。

### 回测测试

- 不读取未来价格。
- 正确计算手续费。
- 正确处理空仓。
- 相同配置能够复现结果。
- 回测收益计算正确。

---

# 十五、迭代开发计划

采用小步迭代。

每次迭代结束必须保证项目能够构建和测试。

## Iteration 001：项目初始化

完成：

- .NET 解决方案
- 基础目录结构
- 统一模型接口
- Mock 模型
- API 基础工程
- 测试工程
- README

## Iteration 002：随机基线引擎

完成：

- UniformRandom
- DirichletRandom
- MajorityBaseline
- PriorBaseline
- Random Ensemble
- 随机种子

## Iteration 003：统计评估

完成：

- Accuracy
- Brier Score
- Log Loss
- Skill Score
- Monte Carlo
- 基础统计检验

## Iteration 004：模型互补性

完成：

- 错误相关性
- 分歧率
- 联合正确率
- Oracle 上限
- 分析报告
## Iteration 005：模型融合

完成：

- EqualWeight
- StaticWeight
- PerformanceWeight
- DynamicWeight
- DecisionGate
- Abstention

## Iteration 006：回测引擎

完成：

- 时间序列回测
- Walk-Forward
- 交易成本
- 收益指标
- 风险指标

## Iteration 007：股票投资示例

完成：

- 示例行情数据
- Mock 模型
- 多策略比较
- 随机交易对照
- 回测报告

## Iteration 008：真实模型接入

完成：

- 通用 HTTP Adapter
- Jev Adapter
- Laya Adapter
- 模型版本管理
- 模型调用日志

真实适配器必须根据实际项目文档实现。

如果无法获取真实接口，记录阻塞原因，不允许伪造接入成功。

## Iteration 009：生产化完善

完成：

- Docker Compose
- PostgreSQL 持久化
- 异步任务执行
- 错误处理
- 性能测试
- 使用文档
- 示例配置

---

# 十六、文档管理

在 `docs/iterations/` 下维护每次迭代文档。

例如：

```text
docs/iterations/
├── 001-initialization.md
├── 002-random-baselines.md
├── 003-statistical-evaluation.md
├── 004-model-complementarity.md
├── 005-decision-fusion.md
├── 006-backtesting.md
├── 007-stock-example.md
├── 008-model-adapters.md
└── 009-production.md
```

每份迭代文档必须包含：

1. 目标。
2. 实现范围。
3. 技术方案。
4. 关键代码位置。
5. 测试方法。
6. 验收结果。
7. 已知问题。
8. 下一阶段计划。

另外维护：

`docs/DEVELOPMENT_PLAN.md`

用于跟踪整体开发进度。

---

# 十七、最终验收

项目完成后，至少能够运行以下实验。

### 实验一：随机猜测基准

1000 道三选一问题。

执行 10000 次随机实验。

输出随机准确率分布。

### 实验二：AI 是否优于随机

构造：

- Model-A：约 70% 准确率。
- Model-B：约 35% 准确率。
- Random：理论准确率 33.33%。

验证统计评估结果。

### 实验三：模型互补性

构造两个错误相关性不同的模型。

分析融合是否可能提升准确率。

### 实验四：模型组合

比较：

- 单模型
- 等权组合
- 动态组合
- 随机基线

验证组合不一定优于单模型。

### 实验五：股票回测

比较：

- JevOnly
- LayaOnly
- EqualWeight
- DynamicWeight
- RandomTrading
- BuyAndHold
- NoTrade

输出统一报告。

### 实验六：拒绝决策

当模型没有足够统计优势时，系统能够输出 ABSTAIN。

---

# 十八、项目成功标准

RandSystem1 的成功，不以模型融合后准确率必然提高为标准。

真正的成功标准是：

1. 能准确建立随机基线。
2. 能正确评估模型表现。
3. 能识别模型是否具有统计优势。
4. 能分析模型之间的互补性。
5. 能验证融合策略是否有效。
6. 能识别无优势的决策场景。
7. 能输出可解释的决策依据。
8. 能通过独立回测验证结果。
9. 能方便地接入其他决策模型。
10. 能在普通 CPU 服务器上稳定运行。

---

# 十九、立即开始开发

现在开始执行。

第一步：

检查当前工作目录、Git 状态和 .NET SDK 环境。

第二步：

创建项目基础文档：

- README.md
- docs/ARCHITECTURE.md
- docs/STATISTICAL_METHODS.md
- docs/DEVELOPMENT_PLAN.md

第三步：

创建 .NET 10 解决方案和基础工程。

第四步：

完成 Iteration 001。

第五步：

运行构建与单元测试。

第六步：

提交 Git，并输出：

- 已完成内容。
- 关键代码文件。
- 测试结果。
- 已知问题。
- 下一阶段计划。

**不要在 Iteration 001 完成后自动跳过测试直接开发后续功能。**

必须确保当前迭代达到验收标准。

如果环境允许且测试全部通过，可以继续执行下一迭代。

最终目标是交付一个真正可以运行、可以验证、可以扩展的 RandSystem1 开源框架，而不是一份停留在设计阶段的技术方案。