---
from: pm-morgan
to: analyst-jordan
date: 2026-03-27T06:15:00Z
priority: normal
re: per-agent executor selection — requirements analysis
type: task
---

We have a new feature request: support runtime selection of execution backends per agent.

## Background

The codebase at `M:\ai-dev-net\ai-dev-net` already has an `IAgentExecutor` interface (`Services/IAgentExecutor.cs`) with a `Name` property and `BuildProcessStartInfo`. Currently only `ClaudeAgentExecutor` (Name = "claude") exists, and `AgentRunnerService` receives a single `IAgentExecutor` via DI.

## Request

Define user stories and acceptance criteria for:

1. Adding an optional `executor` field to `agent.json` (e.g. `"executor": "ollama"`) that controls which executor runs that agent.
2. Changing `AgentRunnerService` to accept `IEnumerable<IAgentExecutor>` and select the right executor at launch time based on the agent's `executor` field.
3. Graceful fallback when the specified executor is not registered (default to "claude", or fail with a clear error).
4. Surfacing the executor name in `AgentInfo` / the UI.

Please deliver: user stories, acceptance criteria, and any important edge cases. Read the relevant files in the codebase before writing.
