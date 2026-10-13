using RandSystem1.Abstractions.Interfaces;
using RandSystem1.Abstractions.Models;
using RandSystem1.Core.Validation;
using RandSystem1.Core.Random;
using RandSystem1.Core.Statistics;
using RandSystem1.Infrastructure.ModelAdapters;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IDecisionModel, MockUniformDecisionModel>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
    app.MapGet("/openapi/v1.json", () => Results.File(
        Path.Combine(app.Environment.ContentRootPath, "openapi.json"),
        "application/json"));

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", application = "RandSystem1" }))
    .WithName("Health");
app.MapGet("/api/models", (IEnumerable<IDecisionModel> models) =>
    Results.Ok(models.Select(x => new { modelId = x.ModelId, isSimulation = true })))
    .WithName("ListModels");

app.MapPost("/api/random/generate", (BaselineRunRequest request, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(BaselineEngine.Run(request, cancellationToken));
    }
    catch (ArgumentException error)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["random"] = [error.Message] });
    }
}).WithName("GenerateRandomBaselines");

app.MapPost("/api/evaluation/run", (EvaluationRequest request) =>
{
    try { return Results.Ok(ClassificationMetrics.Evaluate(request)); }
    catch (ArgumentException error)
    { return Results.ValidationProblem(new Dictionary<string, string[]> { ["evaluation"] = [error.Message] }); }
}).WithName("EvaluateClassification");

app.MapPost("/api/evaluation/monte-carlo", (MonteCarloRequest request, CancellationToken ct) =>
{
    try { return Results.Ok(MonteCarloEvaluation.Run(request, ct)); }
    catch (ArgumentException error)
    { return Results.ValidationProblem(new Dictionary<string, string[]> { ["monteCarlo"] = [error.Message] }); }
}).WithName("EvaluateMonteCarlo");

app.MapPost("/api/evaluation/paired", (PairedComparisonRequest request) =>
{
    try { return Results.Ok(StatisticalInference.Compare(request)); }
    catch (ArgumentException error)
    { return Results.ValidationProblem(new Dictionary<string, string[]> { ["comparison"] = [error.Message] }); }
}).WithName("EvaluatePairedDifference");

app.MapPost("/api/evaluation/complementarity", (ComplementarityRequest request, CancellationToken ct) =>
{
    try { return Results.Ok(ModelComplementarity.Evaluate(request, ct)); }
    catch (ArgumentException error)
    { return Results.ValidationProblem(new Dictionary<string, string[]> { ["complementarity"] = [error.Message] }); }
}).WithName("EvaluateModelComplementarity");

app.MapPost("/api/evaluation/binomial", (ExactBinomialRequest request) =>
{
    try { return Results.Ok(StatisticalInference.ExactBinomialUpperTail(request)); }
    catch (ArgumentException error)
    { return Results.ValidationProblem(new Dictionary<string, string[]> { ["binomial"] = [error.Message] }); }
}).WithName("EvaluateBinomialUpperTail");

app.MapPost("/api/models/predict", async (
    DecisionRequest request,
    string? modelId,
    IEnumerable<IDecisionModel> models,
    CancellationToken cancellationToken) =>
{
    var errors = DecisionValidator.ValidateRequest(request);
    if (errors.Count > 0)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = errors.ToArray() });

    var model = models.FirstOrDefault(x => x.ModelId == (modelId ?? "mock-uniform"));
    if (model is null) return Results.NotFound(new { error = "Unknown model." });

    var prediction = await model.PredictAsync(request, cancellationToken);
    var predictionErrors = DecisionValidator.ValidatePrediction(request, prediction);
    if (predictionErrors.Count > 0)
        return Results.Problem("Model produced invalid prediction.", statusCode: 502);
    return Results.Ok(prediction);
}).WithName("Predict");

app.Run();

public partial class Program;
