That's a thorough analysis. Here are the candidates for deepening, ordered by impact:

1. Tool schema duplication across executors
Cluster: AnthropicToolSchemas, OllamaToolSchemas, LmStudioSkills, WorkspaceTools.Execute()
Why coupled: All executors implement the same 9 workspace tools but each defines its own JSON schema format separately. Adding or renaming a tool requires edits in 5 places.
Dependency category: In-process
Test impact: Replace per-executor schema unit tests with a single boundary test that verifies all executors expose consistent capabilities

2. Executor divergence: LocalToolBroker vs WorkspaceTools
Cluster: ai-dev.core.local/Implementation/LocalToolBroker, ai-dev.executor.ollama/WorkspaceTools
Why coupled: Two independent tool dispatch implementations — LocalOrchestrator gets 4 tools, all HTTP executors get 9. The split is invisible at the IAgentExecutor boundary and produces silently different agent behaviour depending on how a model is configured.
Dependency category: Local-substitutable (filesystem)
Test impact: Replace LocalToolBroker unit tests with boundary tests asserting on what small-model agents can and cannot do

3. AgentRunnerService implicit state machine
Cluster: AgentRunnerService, IAgentRunnerService, SessionInfo, rate-limit tracking, background fire-and-forget loop
Why coupled: The 7-method public interface hides a concurrent state machine — sessions, rate limits, cancellation, inbox archival, re-launch logic — all in one 500-line class with no compile-time invariants
Dependency category: Local-substitutable (in-memory session store)
Test impact: Replace narrow method tests with session lifecycle boundary tests (launch → run → complete → re-launch)

4. Board → Decisions → Agent dependency chain
Cluster: BoardService, TaskAssignedHandler, OverwatchService, DecisionChatService, DispatcherService
Why coupled: 4-layer event chain where each layer has independent error handling — a failure in TaskAssignedHandler silently orphans a task for 30 minutes before OverwatchService catches it
Dependency category: Local-substitutable (in-memory event bus)
Test impact: Replace handler unit tests with end-to-end workflow tests from "task assigned" to "agent launched"

5. AgentPromptBuilder stale inbox snapshot
Cluster: AgentRunnerService (lines 260–346), AgentPromptBuilder
Why coupled: The inbox snapshot is captured at session start but consumed 80 lines later — messages written in between are silently dropped
Dependency category: Local-substitutable (temp filesystem)
Test impact: Replace Build() purity tests with a test that writes an inbox message mid-session and asserts the agent sees it

Which of these would you like to explore?