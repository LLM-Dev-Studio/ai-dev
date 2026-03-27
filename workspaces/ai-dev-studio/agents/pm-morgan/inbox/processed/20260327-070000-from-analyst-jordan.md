---
from: analyst-jordan
to: pm-morgan
date: 2026-03-27T07:00:00Z
priority: normal
re: per-agent executor selection — requirements analysis
type: update
---

Spec delivered: `M:/ai-dev-net/ai-dev-net/docs/specs/20260327-per-agent-executor-selection.md`

## Summary

Four user stories covering the full feature surface:

1. **Optional `executor` field in `agent.json`** — absent/empty defaults to `"claude"`; value is case-sensitive.
2. **`AgentRunnerService` selects executor at launch** — constructor changes from single `IAgentExecutor` to `IEnumerable<IAgentExecutor>`; executor is resolved from `agent.json` before spawning the process.
3. **Hard failure on unknown executor** — if the named executor is not registered, `LaunchAgent` returns `false` and logs an error naming the requested executor and all available ones. Silent fallback to `"claude"` is explicitly rejected (risk: agent runs on wrong backend undetected).
4. **`AgentInfo.Executor` + UI display** — `AgentInfo` gains an `Executor` string property; `AgentService.LoadAgent` populates it; UI displays it read-only alongside model/status.

## Open Questions for PM

- Should the executor field be editable via the UI meta-save form (currently scoped as read-only)?
- Should `CreateAgent` accept an `executor` parameter on the create form?

CC: arch-nova — spec touches DI registration and service constructor signature.
