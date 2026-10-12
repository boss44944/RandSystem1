using System.Text.Json;

namespace RandSystem1.Abstractions.Models;

/// <summary>A single N-choice decision. Context must contain only information available at prediction time.</summary>
public sealed record DecisionRequest(
    string RequestId,
    string TaskType,
    IReadOnlyList<string> Options,
    JsonElement? Context = null);
