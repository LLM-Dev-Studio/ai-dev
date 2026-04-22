# Implementation Plan: Local Functionality Core

**Date**: 2026-04-22
**Branch**: feature/local-functionality-core
**Status**: Draft — awaiting review

---

## Pre-work Completed

The following work was completed before Phase 1 began and is already committed to this branch.

### Feature flag infrastructure

A lightweight custom feature flag system was introduced rather than `Microsoft.FeatureManagement`. The Microsoft package is designed for server-side progressive rollout (percentage filters, time windows, user targeting via ASP.NET Core middleware) and adds friction in a WinUI desktop context. For user-controlled opt-in flags the simpler approach is sufficient; `Microsoft.FeatureManagement` can be layered in later if rules-based evaluation is ever needed.

**Files added:**
- `ai-dev.core/Models/AppFeatureFlags.cs` — flag model (`LocalFunctionalityEnabled`, default `false`)
- `ai-dev.core/Services/FeatureFlagsService.cs` — singleton that reads/writes `feature-flags.json` in `AppContext.BaseDirectory`; in-memory cache avoids repeated disk reads
- `ai-dev.core/FilePathConstants.cs` — added `FeatureFlagsFileName = "feature-flags.json"`
- `ai-dev.core/Extensions/CoreServiceExtensions.cs` — registers `FeatureFlagsService` as singleton

### Preferences page (WinUI only)

A dedicated Preferences page was added to the WinUI app as the permanent home for feature flags going forward. This allows new experimental work to be merged to `master` without affecting users or other developers until they explicitly opt in.

**Files added:**
- `ai-dev.ui.winui/ViewModels/PreferencesViewModel.cs` — loads flags on navigation; auto-saves on toggle change (loading guard prevents spurious saves during load)
- `ai-dev.ui.winui/Views/Pages/PreferencesPage.xaml` — card-based layout matching existing Settings page style; toggle switches with descriptions
- `ai-dev.ui.winui/Views/Pages/PreferencesPage.xaml.cs` — standard code-behind pattern
- `ai-dev.ui.winui/App.xaml.cs` — `PreferencesViewModel` registered as Transient
- `ai-dev.ui.winui/MainWindow.xaml.cs` — `preferences` added to `_pageMap`; "Preferences" footer nav item (`Symbol.Manage`) added to both home and project nav states

---

## Research Summary

### What exists today

| Layer | State |
|---|---|
| `ai-dev.core.local` | Contracts only — 6 interfaces, ~20 records, zero implementations |
| `ai-dev.core` | Rich domain: `Result<T>`, `DomainError`, `AgentRunnerService`, executor plugin model |
| Executors | 6 plugin executors (`IAgentExecutor`); Ollama + LM Studio are local-capable |
| Orchestration loop | None — each executor owns its own loop internally |
| Feature flags | Implemented — `FeatureFlagsService` + `PreferencesPage` (WinUI opt-in toggle per user) |
| Tests | xUnit + NSubstitute + Shouldly; two test projects (unit + integration) |
| DI | `AddAiDevCore()` extension; executors each register via their own extension method |

### Key integration points already available

- `Result<T>` / `DomainError` from `ai-dev.core` — all new code reuses this
- `AgentRunnerService` — the existing "outer" loop that selects executors and manages sessions; Phase 1 hooks in here behind a flag
- `ExecutorHealthMonitor` + `IModelRegistry` — provides runtime model profiles needed by `IModelStrategyResolver`
- OpenTelemetry already wired in `ai-dev-net.ServiceDefaults` — Phase 3 telemetry is essentially free

---

## Phase 1 — Foundation

**Goal:** Wire the loop skeleton end-to-end with null-object implementations behind the `LocalFunctionalityEnabled` feature flag. No real AI calls yet — just the plumbing compiles, runs, and is covered by unit tests. The feature flag infrastructure and Preferences page are already in place (see Pre-work above).

**Estimated effort:** ~1 week

### Commit 1a — `LocalOrchestratorOptions` + DI registration

Files:
- `ai-dev.core.local/Extensions/LocalCoreExtensions.cs` — `AddLocalCore(this IServiceCollection)` extension method
- `ai-dev.core.local/Orchestration/LocalOrchestratorOptions.cs` — `record LocalOrchestratorOptions(int MaxIterations, RuntimeBudget DefaultBudget)` — no `Enabled` property here; enabled state is read from `FeatureFlagsService.GetFlags().LocalFunctionalityEnabled` at call sites so the Preferences page remains the single source of truth
- Register all interfaces bound to null-objects
- Wire `AddLocalCore()` into `ai-dev.core/Extensions/CoreServiceExtensions.cs`

### Commit 1b — Null-object implementations

Files:
- `ai-dev.core.local/Implementation/Null/NullPlanner.cs` — returns `Ok(plan with empty tool list)`
- `ai-dev.core.local/Implementation/Null/NullToolBroker.cs` — returns `Ok([])`
- `ai-dev.core.local/Implementation/Null/NullDiscoveryEngine.cs`
- `ai-dev.core.local/Implementation/Null/NullCompactor.cs`
- `ai-dev.core.local/Implementation/Null/NullModelStrategyResolver.cs` — returns safe defaults
- `ai-dev.core.local/Implementation/InMemoryRuntimeMemoryStore.cs` — `ConcurrentDictionary` backing, no persistence yet

### Commit 1c — `LocalOrchestrator` loop

File: `ai-dev.core.local/Orchestration/LocalOrchestrator.cs`

Implements the loop contract from the blueprint:
1. Read state → `ILocalPlanner.PlanNextAsync`
2. → `ILocalToolBroker.ExecuteAsync`
3. → append observations to transcript
4. → `IContextCompactor.Compact` on interval
5. → `IRuntimeMemoryStore.SaveSnapshotAsync`
6. → repeat until success / blocked / budget exhausted

Guarded by `FeatureFlagsService.GetFlags().LocalFunctionalityEnabled`.

### Commit 1d — `AgentRunnerService` integration point

- Add optional `ILocalOrchestrator` parameter to `AgentRunnerService`
- When present and `FeatureFlagsService.GetFlags().LocalFunctionalityEnabled`, route local-model executors through the orchestrator instead of direct `RunAsync`
- One thin call-site change; existing behaviour is unaffected when disabled

### Commit 1e — Unit tests

Files in `ai-dev-net.tests.unit`:
- `LocalOrchestratorTests.cs` — loop terminates on empty plan, budget exhaustion, and planner error; NSubstitute stubs for all interfaces
- `InMemoryRuntimeMemoryStoreTests.cs` — save/load round-trip
- `NullImplementationTests.cs` — smoke tests that null-objects return `Ok`

**Exit criteria:** Solution builds; unit tests pass; when `Enabled = false` (the default) behaviour is identical to today.

---

## Phase 2 — Real Implementations

**Goal:** Replace null-objects with working implementations. Local models (Ollama / LM Studio) can complete a single objective through the full loop.

**Estimated effort:** ~2 weeks

### Commit 2a — `StaticModelStrategyResolver`

- Maps `RuntimeModelProfile.ModelClass` → `RuntimeModelStrategy`
- Classes:
  - `"small-local"` — conservative: planning depth 1, discovery breadth 3, 1 parallel tool
  - `"large-local"` — depth 3, breadth 8, 4 parallel tools
- Unit tested with a property table

### Commit 2b — `RuleBasedContextCompactor`

- Implements blueprint §6 keep/drop rules
- Pure function on `LocalRuntimeState` → `CompactionSnapshot`
- **Keeps:** open decisions, last success/fail, stable facts, unresolved errors with direct evidence
- **Drops:** redundant command output, superseded hypotheses, repeated observations
- Unit tested with deterministic transcript fixtures

### Commit 2c — `FileSystemRuntimeMemoryStore`

- Persists `CompactionSnapshot` as JSON under `{workspace}/.ai-dev/memory/{objectiveId}.json`
- Replaces `InMemoryRuntimeMemoryStore` in production DI registration
- Integration tested against a temp directory

### Commit 2d — `ProgressiveDiscoveryEngine`

Implements blueprint §5 four-phase contract:
1. Candidate discovery — glob + grep
2. Targeted slice reads — file line ranges
3. Evidence synthesis — assembles `DiscoverySlice` list
4. Confidence + next-step recommendation

Hard rule: never reads a full file until slice confidence < threshold.

Integration tested with real files in the test workspace.

### Commit 2e — `LocalToolBroker`

- Wraps existing executor tool calls via `IAgentExecutor.RunAsync`
- Maps `ToolRequest` → `ExecutorContext` → `ToolOutcome`
- Respects `RuntimeModelStrategy.MaxParallelTools`

### Commit 2f — `LlmPlanner`

- Calls the local executor (Ollama / LM Studio) with a structured planning prompt
- Prompt template produces a JSON `RuntimeActionPlan`
- Retries up to `RuntimeBudget.MaxRetriesPerError` on parse failure
- Unit tested with canned JSON responses via NSubstitute

### Commit 2g — Regression tests

In `ai-dev-net.tests.integration`:
- End-to-end: one objective through the full loop using real Ollama / LM Studio
- Tests skipped automatically when executor is unavailable
- Assertions: context tokens stay under budget; `DomainError` surfaces with evidence on failure

**Exit criteria:** A `LocalObjective` with `Goal = "List all IAgentExecutor implementations"` completes through the loop against a running Ollama instance and produces a `CompactionSnapshot` with correct facts.

---

## Phase 3 — Sub-agent Roles + Telemetry

**Goal:** Decompose the loop into bounded roles; add quality metrics.

**Estimated effort:** ~1–2 weeks

### Commit 3a — Sub-agent role decomposition

- Define role enum: `PlannerRole`, `ResearcherRole`, `CoderRole`
- Each role gets a dedicated prompt template and tool whitelist
- `LocalOrchestrator` dispatches to role-specific `ILocalPlanner` implementations based on current loop state

### Commit 3b — OpenTelemetry instrumentation

- `ActivitySource("AiDev.LocalOrchestrator")` spans per iteration
- Metrics:
  - `aidev.loop.iterations`
  - `aidev.loop.tool_calls`
  - `aidev.compaction.tokens_saved`
  - `aidev.loop.success_rate`
- Wired into existing `ServiceDefaults` OpenTelemetry setup — no new packages needed

### Commit 3c — Policy tuning

- `RuntimeModelStrategy` extended with `CompactionRatio` and `ToolCallBudget` per model class
- Dashboard-ready metric names logged to structured output

**Exit criteria:** A complete objective run produces OTel spans visible in the Aspire dashboard; telemetry shows tool-call budget and compaction ratio per run.

---

## Commit Cadence

Each lettered commit (1a through 3c) is independently buildable, tested, and reviewable. Phase boundaries are natural PR merge points.

| Commit | Description | Status | Phase gate |
|---|---|---|---|
| P0 | Project scaffold + design blueprint | Done | |
| P1 | `AppFeatureFlags`, `FeatureFlagsService`, `PreferencesPage` (WinUI) | Done | **Pre-work** |
| 1a | `LocalOrchestratorOptions` + DI registration | Done | |
| 1b | Null-object implementations | Done | |
| 1c | `LocalOrchestrator` loop | Done | |
| 1d | `AgentRunnerService` integration | Done | |
| 1e | Unit tests | Done | **Phase 1 PR** |
| 2a | `StaticModelStrategyResolver` | Done | |
| 2b | `RuleBasedContextCompactor` | Done | |
| 2c | `FileSystemRuntimeMemoryStore` | Done | |
| 2d | `ProgressiveDiscoveryEngine` | Done | |
| 2e | `LocalToolBroker` | Done | |
| 2f | `LlmPlanner` + `OllamaLlmClient` | Done | |
| 2g | Regression tests | Done | **Phase 2 PR** |
| 3a | Sub-agent role decomposition | | |
| 3b | OpenTelemetry instrumentation | | |
| 3c | Policy tuning | | **Phase 3 PR** |
