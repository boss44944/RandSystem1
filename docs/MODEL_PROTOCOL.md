# Model protocol / 模型协议

Implemented Iteration 001:
- IDecisionModel.ModelId: string
- PredictAsync(DecisionRequest, CancellationToken): Task<DecisionPrediction>
- DecisionRequest: requestId, taskType, nonempty unique options, optional context JSON
- DecisionPrediction: requestId, taskType, modelId, modelVersion, probabilities, predictedOption, latencyMs, predictedAt, isSimulation.

The validator accepts only finite probabilities in [0,1], one per option, summing to 1 within 1e-9. Model errors must not be disguised as valid probabilities. Cancellation is propagated. The mock adapter predicts the first option as a deterministic tie-break and emits uniform probabilities for protocol testing, **not** a real measured confidence estimate.

Example request JSON:

    {"requestId":"demo-1","taskType":"choice","options":["UP","DOWN"],"context":{"symbol":"DEMO"}}

Real Jev/Laya adapters are deferred until their public interfaces and semantics can be verified.
