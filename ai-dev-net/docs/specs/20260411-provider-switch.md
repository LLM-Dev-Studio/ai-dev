# Provider Switch

**Date**: 2026-04-11  
**Author**: analyst-standard  
**Status**: Draft — open questions flagged; see Flags table  

---

## Problem Statement

When an agent's configured executor is unavailable, `AgentRunnerService` fails the session immediately and sets the agent to `status: error`. `OverwatchService` detects this on the next stall scan and raises a human decision rather than retrying with an available executor. This means every executor outage requires human intervention, even when a capable fallback executor is online.

This feature adds two capabilities:

1. **Auto-failover**: At session launch, if the agent's primary executor is unhealthy, the system tries the next executor in the agent's or studio's preference list before failing.
2. **Bulk provider switch**: An operator action that reassigns all eligible agents in a scope to a different executor in a single step.

---

## Scope

**In scope**:
- Provider preference ordering — definition, storage, and resolution scope
- Auto-failover at session launch only
- Notification to the user when a failover occurs
- Behavior when all executors in the preference list are offline
- Bulk provider switch: scope, model handling, and permanence
- Per-agent opt-out of auto-failover
- UI restriction: executor override shows only online executors

**Out of scope**:
- Mid-session executor switching (not possible: `RunAsync` holds the session to completion with one executor)
- Implementing any new executor backend
- Changes to `ExecutorHealthMonitor` polling interval or discovery
- Automatic rebalancing once a preferred executor comes back online

---

## User Stories

---

### Story 1 — Define provider preference order

> As an operator, I want to configure a preferred order of executors so that the system knows which backend to try when the primary executor is unavailable.

#### Acceptance Criteria

1. A preference list can be defined that names executors in priority order (e.g. `["claude", "anthropic", "ollama"]`).
2. The preference list is validated on save: any name that does not match a registered executor (`IAgentExecutor.Name`) is rejected with a clear error message identifying the invalid name(s).
3. An executor name may appear in the list at most once; duplicates are rejected.
4. The list may be empty, which means "no failover" — the agent uses its configured executor only and fails if it is offline.
5. The scope of the preference list — whether it is global (studio-wide), per-project, or per-agent — is **[BLANK — see Flag F1]**.
6. The storage location and field name for the preference list is **[BLANK — see Flag F2]**.
7. The default preference order when no list is configured is **[BLANK — see Flag F3]**.

---

### Story 2 — Auto-failover at session launch

> As an operator, I want agents to automatically try the next executor in the preference list when their primary executor is offline at launch time, so that work continues without manual intervention.

#### Acceptance Criteria

1. `AgentRunnerService` evaluates the configured executor's health via `ExecutorHealthMonitor.GetHealth(executorName).IsHealthy` before starting a session.  
   **Source**: `EXTRACTED` — `OverwatchService` already reads `executorHealth.GetHealth(executorName).IsHealthy` before deciding to nudge or raise a decision.
2. If the primary executor is unhealthy, the service iterates the preference list in order and selects the first executor whose `GetHealth().IsHealthy` is `true`.
3. If the primary executor is healthy, no failover occurs regardless of the preference list.
4. The executor selected for the session — primary or fallback — is recorded in the transcript header (`executor: {name} · model: {modelId}`). **Source**: `EXTRACTED` — the transcript header format is already `executor: {executorName.Value} · model: {modelId}`.
5. A failover notification is produced when a fallback executor is used. The format and destination of this notification is **[BLANK — see Flag F4]**.
6. Failover is ephemeral or permanent (i.e., whether `agent.json` `executor` field is rewritten) is **[BLANK — see Flag F5]**.
7. Model resolution after failover: which model to use when the fallback executor is selected is **[BLANK — see Flag F6]**.
8. If all executors in the preference list are offline and the primary is also offline, the session must not start. The agent status is set to `error` with a message listing all attempted executors and their health messages. The agent's `agent.json` is not rewritten.  
   **Source**: `INFERRED` — consistent with the existing hard-failure pattern in `AgentRunnerService` (`status: error`) and the rationale in the per-agent-executor-selection spec ("fail loudly rather than silently").
9. The OTEL activity for the session records: the originally configured executor name (`agent.executor.configured`), the executor actually used (`agent.executor`), and a boolean `agent.executor.failover` tag.  
   **Source**: `INFERRED` — extends the existing `agent.executor` tag pattern in `AgentRunnerService`.
10. `OverwatchService.RaiseExecutorOfflineDecision` must not fire for an agent when failover succeeds (the task is being progressed; raising a decision would be misleading).  
    **Source**: `INFERRED` — Overwatch currently raises a decision when the executor is offline; once auto-failover is available, that path should only fire when all executors are exhausted.

---

### Story 3 — Failover notification

> As a user, I want to know when an agent used a fallback executor so I can take action if the primary executor remains offline.

#### Acceptance Criteria

1. When auto-failover occurs, a notification is produced. The format (transcript entry, UI badge, inbox message to the agent, or combination) is **[BLANK — see Flag F4]**.
2. The notification must include: the agent identifier, the originally configured executor, the executor actually used, and the time of the failover.
3. The notification must not be produced when no failover occurs (primary executor was healthy).

---

### Story 4 — Bulk provider switch

> As an operator, I want to switch all eligible agents to a different executor in a single action so that I can respond to a provider outage or policy change without editing each agent individually.

#### Acceptance Criteria

1. The bulk switch action accepts: a target executor name. The source scope is **[BLANK — see Flag F7]**.
2. Only agents currently using a specific source executor are switched, or all agents in scope regardless of current executor — this is **[BLANK — see Flag F7]**.
3. The target executor name must match a registered executor (`IAgentExecutor.Name`); the action is rejected with a clear error if it does not.
4. A bulk switch must not affect agents that are currently running (`AgentRunnerService.IsRunning()` returns `true`). Those agents are skipped; the result must report how many were skipped and why.  
   **Source**: `INFERRED` — modifying `agent.json` of a running agent mid-session would be a race condition; the consistent pattern across the codebase is to check `IsRunning` before acting.
5. Whether the bulk switch is permanent (writes new `executor` to each `agent.json`) or a temporary runtime override is **[BLANK — see Flag F5]**.
6. Which model to use after a bulk switch — whether to keep the agent's current model if compatible with the target executor, or use the executor's default — is **[BLANK — see Flag F6]**.
7. The bulk switch produces a result summary: number of agents switched, number skipped (running), number of failures (e.g. permission error on `agent.json`).
8. The bulk switch is exposed in the UI. The exact placement is at the implementor's discretion, but it must be reachable without navigating to each agent individually.

---

### Story 5 — Per-agent opt-out of auto-failover

> As an operator, I want to disable auto-failover for a specific agent so that it only ever runs on its configured executor, even if that executor is offline.

#### Acceptance Criteria

1. An agent can be configured to opt out of auto-failover. The field name and storage location is **[BLANK — see Flag F2]**, but the opt-out flag lives alongside the agent's existing `agent.json` configuration (consistent with `executor`, `model`, `skills` fields already there).  
   **Source**: `INFERRED` — `agent.json` is the established per-agent config file for runtime properties.
2. When opt-out is active, the system behaves as if the preference list were empty for that agent: failover is not attempted; if the primary executor is offline, the session fails with the existing error path.
3. The opt-out flag is visible in the agent detail UI alongside the executor field.
4. The opt-out flag is read-only in the UI meta-save form; it must be set by editing `agent.json` directly.  
   **Source**: `INFERRED` — consistent with the resolved question in the per-agent-executor-selection spec, where the `executor` field is read-only in the UI for this class of operational config.

---

### Story 6 — Executor override restricted to online executors

> As a user, I want the executor override dropdown in the agent detail UI to show only executors that are currently online so that I cannot accidentally assign an agent to an unavailable provider.

#### Acceptance Criteria

1. The executor selection control in `AgentDetailPage.razor` populates its option list from `ExecutorHealthMonitor.GetExecutorHealth()`.  
   **Source**: `EXTRACTED` — `ExecutorHealthMonitor.GetExecutorHealth()` returns `IReadOnlyList<(IAgentExecutor Executor, ExecutorHealthResult Health)>`, already designed for UI rendering.
2. Only executors where `ExecutorHealthResult.IsHealthy == true` are shown as selectable options.  
   **Source**: `EXTRACTED` — "online" is defined as `IsHealthy == true` in `OverwatchService.NudgeAgent()`.
3. If the agent's currently configured executor is offline, it is shown in a disabled/read-only state (not as a selectable option), so the user can see what is configured without being able to "re-select" an offline provider.
4. The list refreshes at the same cadence as the health poll — i.e., within 30 seconds of a health state change.  
   **Source**: `EXTRACTED` — `ExecutorHealthMonitor` fires `Changed` event after every poll cycle; the UI can subscribe to this.

---

## Edge Cases and Constraints

| Scenario | Expected Behaviour |
|---|---|
| Preference list contains an executor that is registered but offline | Skip it; try next in list |
| Preference list contains an executor name not registered in DI | Skip it and log a warning; treat as offline. Do not fail the whole failover. |
| Primary executor healthy but preference list is malformed | Use primary executor; log a warning about malformed preference list |
| Agent is running when bulk switch is triggered | Skip that agent; include it in the skipped count in the result summary |
| All executors healthy at launch | Use primary executor — no failover, no notification |
| Agent opts out of failover; primary offline | Fail session with error (existing behaviour); do not try preference list |
| Failover succeeds; primary comes back online later | The agent's configuration is unchanged (ephemeral) or updated (permanent) — **[BLANK — see Flag F5]**. No automatic reversion in scope. |
| Bulk switch targets an executor not registered | Reject with a clear error message before any agents are modified |

---

## Open Questions

All open questions are flagged below. These require a human decision before implementation can begin on the affected stories.

### Flags Table

| ID | Story | Question | Why it cannot be inferred |
|---|---|---|---|
| F1 | Story 1 AC5 | **Preference list scope**: Should the preference order be global (studio-wide), per-project, or per-agent? Or a combination (e.g. global default overridden per-agent)? | The codebase has both `StudioSettingsService` (global) and `agent.json` (per-agent) as established config locations. The right scope is a product decision about operator workflow, not a technical constraint. |
| F2 | Stories 1, 5 | **Storage field names and location**: Where is the preference list stored (e.g. new field in `agent.json`, new section in studio settings file)? What is the field name? Same question for the per-agent opt-out flag. | Depends on F1 (scope). Cannot specify field names without knowing the config file. |
| F3 | Story 1 AC7 | **Default preference order**: What is the default executor preference order when none is configured? (e.g. `["claude", "anthropic", "ollama", "github-models"]`?) | This is a business/product decision — depends on which executors are considered more reliable or cost-effective. Cannot be derived from code. |
| F4 | Stories 2, 3 | **Failover notification format and destination**: Should the notification appear in the session transcript, as a UI badge on the agent card, as an inbox message to the agent, or some combination? | The existing codebase has all three mechanisms available. The right choice depends on urgency, audience (human vs. agent), and desired visibility. |
| F5 | Stories 2, 4 | **Failover and bulk switch permanence**: Should a successful failover permanently rewrite the `executor` field in `agent.json`, or should it be an ephemeral runtime override that reverts when the preferred executor returns? Same question for bulk switch. | Permanent rewrite is simpler to implement but hides the original preference. Ephemeral is more complex (requires runtime state) but preserves operator intent. This is a significant UX and implementation trade-off requiring a decision. |
| F6 | Stories 2, 4 | **Model after failover or bulk switch**: Should the agent keep its currently configured model if that model exists on the fallback executor, or should it use the fallback executor's default model? | Depends on whether operators expect model identity to be preserved across executor switches. Some models are executor-specific (e.g. Ollama local models vs. Anthropic cloud models). |
| F7 | Story 4 | **Bulk switch scope and source filter**: (a) Should bulk switch apply to all agents globally, only agents in a specific project, or only agents currently assigned to a specific source executor? (b) Should it switch only agents on a specific source executor, or all agents regardless of current executor? | Multiple interpretations are valid. This is a product decision about what the most useful operator workflow is. |

---

## References

- `M:/ai-dev-net/ai-dev.core/Executors/IAgentExecutor.cs` — executor interface (`Name`, `CheckHealthAsync`)
- `M:/ai-dev-net/ai-dev.core/Services/OllamaHealthService.cs` (class: `ExecutorHealthMonitor`) — health polling, `GetHealth()`, `GetExecutorHealth()`
- `M:/ai-dev-net/ai-dev.core/Features/Agent/AgentRunnerService.cs` — session launch, executor selection, current hard-fail on missing/unhealthy executor
- `M:/ai-dev-net/ai-dev.core/Services/OverwatchService.cs` — `NudgeAgent()`, `RaiseExecutorOfflineDecision()`, existing health check before nudge
- `M:/ai-dev-net/ai-dev-net/Components/Pages/ProjectPages/AgentDetailPage.razor` — agent detail UI, executor selector
- `M:/ai-dev-net/ai-dev-net/docs/specs/20260327-per-agent-executor-selection.md` — prior spec for per-agent executor config (implemented; do not re-specify)
