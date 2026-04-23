using AiDev.Features.Planning.Models;

namespace AiDev.Features.Planning;

/// <summary>
/// Sends a conversation turn to an LLM and returns the full assistant response.
/// Implementations live in executor projects (e.g. ai-dev.executor.anthropic).
/// </summary>
public interface IPlanningLlmClient
{
    /// <summary>
    /// The executor name this client handles. Must match the "executor" field in agent.json
    /// (e.g. "anthropic", "claude", "ollama").
    /// </summary>
    string ExecutorName { get; }

    /// <summary>
    /// Sends the accumulated conversation history plus a new user message to the LLM
    /// and returns the complete assistant response with token usage.
    /// </summary>
    /// <param name="modelId">Model identifier (e.g. "claude-sonnet-4-6").</param>
    /// <param name="systemPrompt">Phase-specific system prompt.</param>
    /// <param name="history">Prior conversation turns (excluding the current user message).</param>
    /// <param name="userMessage">The new user message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PlanningLlmResponse> ChatAsync(
        string modelId,
        string systemPrompt,
        IReadOnlyList<ConversationMessage> history,
        string userMessage,
        CancellationToken ct = default);
}
