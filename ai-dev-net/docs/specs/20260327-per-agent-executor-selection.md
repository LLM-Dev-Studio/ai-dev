# Per-Agent Executor Selection

**Date**: 2026-03-27
**Author**: analyst-jordan
**Status**: Final

---

## Problem Statement

Every agent currently runs via `ClaudeAgentExecutor` — the only registered implementation of `IAgentExecutor`. As the platform adds support for additional execution backends (Ollama, GitHub Copilot, etc.), operators must be able to configure which backend runs a given agent without changing shared infrastructure. The configuration must live alongside the agent itself (`agent.json`) and must be visible in the UI.

---

## Scope

**In scope**:
- An optional `executor` field in `agent.json` that names the backend to use.
- `AgentRunnerService` selecting the correct executor at launch time from a collection of registered `IAgentExecutor` implementations.
- Failure behaviour when the named executor is not registered.
- Surfacing the executor name in `AgentInfo` and the UI (read-only display).

**Out of scope**:
- Implementing any new executor (e.g. Ollama, Copilot) — this spec only defines selection mechanics.
- Editing the executor field via the UI meta-save form.
- Per-model configuration within an executor.

---

## User Stories

### Story 1 — Configure executor per agent

> As an operator, I want to set an optional `executor` field in an agent's `agent.json` so that I can choose which execution backend runs that agent without modifying shared application configuration.

#### Acceptance Criteria

1. `agent.json` accepts an optional top-level string field `"executor"` (e.g. `"executor": "ollama"`). Files that omit this field remain valid with no change in behaviour.
2. When `executor` is absent, null, or an empty/whitespace string, the system behaves as if `"executor": "claude"` were specified.
3. The `executor` value is treated as case-sensitive and must match the `Name` property of a registered `IAgentExecutor` exactly.

---

### Story 2 — AgentRunnerService selects executor at launch time

> As a developer, I want `AgentRunnerService` to pick the right executor for each agent at launch time so that different agents can run on different backends within the same process.

#### Acceptance Criteria

1. `AgentRunnerService` is constructed with `IEnumerable<IAgentExecutor>` rather than a single `IAgentExecutor`. Existing DI registrations of `IAgentExecutor` require no changes beyond this constructor signature update.
2. When `LaunchAgent` is called, the service reads the `executor` field from the agent's `agent.json` before spawning a process. This read is performed as part of the existing `LoadAgentJson` call.
3. The service selects the executor whose `Name` (case-sensitive) equals the resolved executor name (defaulting to `"claude"` per Story 1 AC2).
4. The selected executor's `BuildProcessStartInfo` is called to produce the `ProcessStartInfo` for the agent process. No other changes to the launch flow are required.
5. If multiple registered executors share the same `Name`, the first match in DI registration order is used.
6. The OTEL activity for the launch records the resolved executor name as `agent.executor` tag.

---

### Story 3 — Fail clearly when the named executor is not registered

> As an operator, I want to receive a clear error if I specify an executor that is not available so that misconfigured agents fail loudly rather than silently running on the wrong backend.

#### Acceptance Criteria

1. If the resolved executor name does not match any registered `IAgentExecutor.Name`, the service does **not** start a process and does not fall back silently to another executor.
2. The failure is logged at `Error` level. The log message must include: the agent key (`project/agent`), the requested executor name, and the comma-separated list of available executor names.
3. `LaunchAgent` returns `false` when the executor is not found (consistent with the existing return value for "already running"), so callers do not need to handle a new exception type.
4. The agent's `agent.json` is **not** updated to `status: running` when launch is refused due to a missing executor.

**Rationale for hard failure over silent fallback**: An agent configured for `"ollama"` has CLAUDE.md instructions and prompts written for that executor's behaviour. Silently running it on `"claude"` (or vice versa) would produce incorrect output and mask the configuration error entirely.

---

### Story 4 — Surface executor in AgentInfo and the UI

> As a user, I want to see which executor is running (or will run) each agent in the UI so that I can confirm the correct backend is configured.

#### Acceptance Criteria

1. `AgentInfo` gains a non-nullable `string Executor` property (default value: `"claude"`).
2. `AgentService.LoadAgent()` reads the `executor` field from `agent.json` and sets `AgentInfo.Executor`. When the field is absent, null, or whitespace, `AgentInfo.Executor` is set to `"claude"`.
3. The UI displays `Executor` alongside the agent's model and status. Placement is at the implementor's discretion, but it must be visible without expanding or navigating away from the agent list or agent detail view.
4. The executor field is **read-only** in the UI. It is not included in the existing save-meta form (name / description / model). Changing the executor requires editing `agent.json` directly.

---

## Edge Cases and Constraints

| Scenario | Expected Behaviour |
|---|---|
| `"executor": ""` (empty string) | Treated as absent; effective executor is `"claude"` |
| `"executor": "Claude"` (wrong case) | Treated as a missing executor → launch fails with error (AC3.1–3.3) |
| `agent.json` missing entirely | `LoadAgentJson` already returns null; executor defaults to `"claude"` |
| Only `ClaudeAgentExecutor` registered (current state) | No behaviour change; all agents without an `executor` field use it as before |
| Executor registered after app start | Not applicable — all executors are registered at startup via DI; no runtime registration is in scope |
| `LaunchAgent` called concurrently for same agent | Existing `ConcurrentDictionary` guard handles this; executor resolution is inside the guard |

---

## Open Questions

~~1. **Editable via UI?**~~ **Resolved 2026-03-27 by pm-morgan**: Executor is read-only in the UI for this release. It is a deployment-time configuration concern; agents set it once in `agent.json` and it takes effect at next launch. Story 4 AC4 stands as written.

~~2. **Executor validation on agent create?**~~ **Resolved 2026-03-27 by pm-morgan**: `CreateAgent` will not accept an `executor` parameter for this release. Agents default to `"claude"`; operators who need a different executor edit `agent.json` directly. This will be revisited when a second production-ready executor is available.

---

## References

- `Services/IAgentExecutor.cs` — interface definition
- `Services/ClaudeAgentExecutor.cs` — only current implementation (`Name = "claude"`)
- `Services/AgentRunnerService.cs` — line 10 (single `IAgentExecutor` constructor param), line 157 (`executor.BuildProcessStartInfo` call)
- `Services/AgentInfo.cs` — DTO returned to UI; needs `Executor` property added
- `Services/AgentService.cs` — `LoadAgent()` populates `AgentInfo`; `AgentJson` file-scoped class needs `executor` field
- `Program.cs` — line 25: `builder.Services.AddSingleton<IAgentExecutor, ClaudeAgentExecutor>()`
