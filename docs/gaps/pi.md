# Gap Analysis: ai-dev-net vs pi-mono

Comparison performed against pi-mono (`C:\dev\pi-mono`) to identify features present in pi-mono but missing or incomplete in ai-dev-net.

---

## What ai-dev-net has that pi-mono doesn't

ai-dev-net is a **project orchestration platform** for teams of agents. Pi-mono is a **coding assistant** with no team/project primitives.

| ai-dev-net strength | Notes |
|---|---|
| Kanban board + stall detection | Overwatch service monitors task movement |
| Human-in-the-loop decision escalation | Formal agent→human escalation workflow |
| Inter-agent async messaging (inbox/outbox) | Agents coordinate via file-based messages |
| Knowledge base + playbooks | Structured context injection per agent |
| Domain events (TaskAssigned → dispatch) | IDomainEventDispatcher, handlers |
| Consistency checks + auto-repair | Workspace integrity validation with UI |
| Multi-project / multi-agent dispatcher | Dual-layer FSW + polling |
| OpenTelemetry traces + meters | Distributed tracing across all features |

---

## Gaps identified in ai-dev-net

### 🔴 High priority

#### 1. Interactive decision chat *(planned)*

**Problem**: The current decision resolution flow (agent writes file → human types a response in a textarea → one-shot resolve) is asymmetric and one-directional. The agent cannot ask follow-up questions, and the human cannot iterate with the agent before reaching a conclusion.

**Pi-mono approach**: Live back-and-forth chat session between human and agent.

**Planned approach**: When a human opens a pending decision, a chat session is started. Messages sent by the human are delivered to the agent's inbox (tagged with the decision ID). The agent is auto-launched; its outbox responses (also tagged with the decision ID) appear in the chat thread. The human can continue the conversation then resolve the decision with a summary when done. The decision file remains as the audit record.

---

#### 2. Thinking/reasoning level configuration *(planned)*

**Problem**: The `Reasoning` flag exists in `ModelCapabilities` but there is no way to configure the thinking budget per agent. Users get the model default, which varies by provider and may be off or a fixed budget.

**Pi-mono approach**: Unified `off / low / medium / high / xhigh` levels across all providers. Visual indicator in the UI.

**Planned approach**: Add `ThinkingLevel` enum to the domain (`Off, Low, Medium, High`). Add to `AgentInfo`, `AgentTemplate`, and `ExecutorContext`. Surface a selector in `AgentDetailPage` and `TemplateCard`. Map to provider-specific parameters per executor:
- **Claude CLI**: `--thinking-mode enabled --thinking-budget-tokens N`
- **Anthropic API**: `thinking: { type: "enabled", budget_tokens: N }`
- **GitHub Models**: `reasoning_effort: "low" | "medium" | "high"` (where supported)
- **Ollama**: not supported — hide control if model lacks `Reasoning` capability

---

#### 3. Token & cost tracking *(planned)*

**Problem**: There is no visibility into how many tokens agents consume per session, nor any estimate of API cost. In production with many agents running, this makes cost management impossible.

**Pi-mono approach**: Input/output/cache token counts + per-model pricing, displayed per session.

**Planned approach**: Add `TokenUsage` record (`InputTokens, OutputTokens, CacheReadTokens, CacheWriteTokens`) to `ExecutorResult`. Add per-model pricing fields to `ModelDescriptor` (`InputCostPer1MTokens`, `OutputCostPer1MTokens`). Parse usage from executor output:
- **Claude CLI**: JSON block emitted at session end
- **Anthropic API**: Usage fields in SSE stream events
- **GitHub Models**: Usage field in final response body
- **Ollama**: `prompt_eval_count` / `eval_count` in response

Store a `.usage.json` companion file alongside each transcript. Aggregate in `DigestService`. Surface in `AgentDetailPage` (last session) and `TranscriptPage`.

---

### 🟡 Medium priority

#### 4. Mid-session steering

**Problem**: There is no way to send a message to an agent that is currently running. You can only write to the inbox before launch or after the session ends.

**Pi-mono approach**: `steer()` injects a message between tool calls; `followUp()` queues work for after completion.

**Notes for ai-dev-net**: Would require a mechanism to write to the agent's inbox while it is running and have the agent read new messages mid-session. Claude CLI does not natively support this (it reads stdin once). An Anthropic/API executor could support injected steering via multi-turn continuation.

---

#### 5. Workspace-level context (AGENTS.md hierarchy)

**Problem**: Each agent has its own `CLAUDE.md` but there is no shared project-level or workspace-level context file that all agents inherit. Cross-cutting conventions (e.g. "this project uses TypeScript strict mode") must be duplicated in every agent's CLAUDE.md.

**Pi-mono approach**: Loads `AGENTS.md` from the current working directory upwards; all matching files are concatenated into the system prompt.

**Planned approach**: Support a `CONTEXT.md` (or `WORKSPACE.md`) at the project root that is prepended to every agent's effective system prompt. Optionally walk parent directories for a global user-level context file.

---

#### 6. Transcript HTML export / sharing

**Problem**: Transcripts are raw timestamped `.md` files. There is no way to export or share a formatted session summary.

**Pi-mono approach**: Export to styled HTML; share as a private GitHub gist.

**Notes**: Lower implementation cost — could be a simple Markdown→HTML export with a "Copy link" gist flow if a GitHub token is already configured.

---

### 🟢 Lower priority

#### 7. Image/vision input in messages

Inbox messages are Markdown files — no attachment mechanism. Pi-mono supports clipboard-pasted and drag-dropped images.

#### 8. Runtime prompt template library

Pi-mono has reusable `/templatename` snippets invokable during a session. ai-dev-net has agent *creation* templates but no runtime prompt library.

#### 9. Session branching

Pi-mono supports in-place JSONL session branching and tree navigation. ai-dev-net transcripts are append-only.

#### 10. OAuth provider flows

Pi-mono can log into GitHub Copilot, Google, and Anthropic OAuth without raw API keys. ai-dev-net requires API keys in settings.

---

## Out of scope (deliberate differences)

- **25+ additional LLM providers**: ai-dev-net covers the primary use cases (Claude, Anthropic API, Ollama, GitHub Models). New providers can be added as `IAgentExecutor` implementations without affecting existing code.
- **Automatic context compaction**: ai-dev-net agents use session-based prompts rather than long-running conversations, so context overflow is less acute. May be revisited if agents are extended to support multi-turn continuations.
- **TUI / terminal interface**: ai-dev-net is a web Blazor app; a terminal UI is not planned.
- **Docker/sandbox isolation**: Out of scope for the current architecture.
- **Package manager for extensions**: ai-dev-net uses a DI-based executor/handler extension model; a package installer is not needed at this stage.
