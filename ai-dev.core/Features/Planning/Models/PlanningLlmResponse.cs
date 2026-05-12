namespace AiDev.Features.Planning.Models;

/// <summary>
/// Represents the content and token usage returned from a planning model call.
/// </summary>
/// <param name="Content">The response content returned by the model.</param>
/// <param name="InputTokens">The number of input tokens consumed by the request.</param>
/// <param name="OutputTokens">The number of output tokens generated in the response.</param>
public sealed record PlanningLlmResponse(string Content, int InputTokens, int OutputTokens);
