namespace AiDev.Features.Planning.Models;

public sealed record PlanningLlmResponse(string Content, int InputTokens, int OutputTokens);
