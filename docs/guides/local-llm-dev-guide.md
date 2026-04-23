# Local LLM Feature — Developer Guide

**Last updated:** 22 April 2026

This guide covers everything needed to set up, use, test, and extend the local LLM
orchestration feature (`ai-dev.core.local`). The feature runs AI objectives against a
locally-hosted model (Ollama or LM Studio) through a structured planning loop — no cloud
calls, no API keys.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Enabling the feature](#2-enabling-the-feature)
3. [How the orchestration loop works](#3-how-the-orchestration-loop-works)
4. [Sub-agent roles](#4-sub-agent-roles)
5. [Built-in tools](#5-built-in-tools)
6. [Running an objective in code](#6-running-an-objective-in-code)
7. [Configuration reference](#7-configuration-reference)
8. [Extending the feature](#8-extending-the-feature)
9. [Running the tests](#9-running-the-tests)
10. [Observing telemetry](#10-observing-telemetry)

---

## 1. Prerequisites

### Ollama (recommended)

1. Download and install Ollama from [ollama.com](https://ollama.com).
2. Pull a model. Recommended defaults:

   | Model class | Suggested model | Pull command |
   |---|---|---|
   | `small-local` | Llama 3.2 3B | `ollama pull llama3.2:3b` |
   | `large-local` | Llama 3.1 8B | `ollama pull llama3.1:8b` |

3. Verify Ollama is running:

   ```
   curl http://localhost:11434/api/tags
   ```

   The base URL is configured in the app via **Settings → Ollama Base URL**
   (stored as `StudioSettingsService.OllamaBaseUrl`). The default is
   `http://localhost:11434`.

### LM Studio (alternative)

1. Download LM Studio from [lmstudio.ai](https://lmstudio.ai).
2. Load a GGUF model and start the local inference server.
3. Note the server port (default: `1234`) and update **Settings → LM Studio Base URL**.

---

## 2. Enabling the feature

The local LLM feature is **off by default**. It is gated behind the `LocalFunctionalityEnabled`
feature flag so it can be merged to `master` and deployed without affecting existing users.

**To enable:**

1. Open the app.
2. Navigate to **Preferences** (footer nav icon, bottom of the left rail).
3. Toggle **Enable Local Functionality** to on.
4. The setting persists to `feature-flags.json` in the app's base directory.

All orchestration paths check this flag at call time via `FeatureFlagsService.GetFlags().LocalFunctionalityEnabled`.
Disabling the toggle reverts the app to its previous behaviour without any restart.

---

## 3. How the orchestration loop works

```
LocalObjective + RuntimeModelProfile
        │
        ▼
IModelStrategyResolver.Resolve()
        │  → RuntimeModelStrategy (planning depth, budget, compaction ratio)
        ▼
┌─────────────────────────────────────────────────────────┐
│ Loop (up to MaxIterations)                              │
│                                                         │
│  1. ILocalPlanner.PlanNextAsync(state)                  │
│        └─ RoleBasedLlmPlanner selects role,             │
│           builds prompt, calls ILlmClient               │
│           └─ Returns RuntimeActionPlan                  │
│                                                         │
│  2. Empty ToolRequests + !RequiresUserInput             │
│        └─ Objective complete → FinalCompact → Ok        │
│                                                         │
│  3. ILocalToolBroker.ExecuteAsync(toolRequests)         │
│        └─ Runs read_file / list_dir / grep / glob       │
│           → IReadOnlyList<ToolOutcome>                  │
│                                                         │
│  4. Append ToolOutcomes as RuntimeObservations          │
│     to state.Transcript                                 │
│                                                         │
│  5. Compact when:                                       │
│        a. iteration % CompactionInterval == 0, OR       │
│        b. estimatedTokens ≥ MaxContextTokens            │
│           *   CompactionRatio                           │
│     IContextCompactor.Compact(state)                    │
│     IRuntimeMemoryStore.SaveSnapshotAsync(...)          │
│                                                         │
│  6. Check tool-call budget                              │
│        EffectiveBudget = ToolCallBudget > 0             │
│                         ? ToolCallBudget                │
│                         : DefaultBudget.MaxToolCalls    │
│        Observations.Count ≥ EffectiveBudget → BudgetExhausted Err  │
└─────────────────────────────────────────────────────────┘
        │
        ▼
Result<CompactionSnapshot>
  Ok   → CompactionSnapshot with Facts, OpenQuestions, CompactSummary
  Err  → DomainError (code: see §7)
```

The loop is implemented in [LocalOrchestrator.cs](../../ai-dev.core.local/Orchestration/LocalOrchestrator.cs).
It is pure orchestration — no AI calls happen inside it directly; those are delegated to
`ILocalPlanner` → `ILlmClient`.

---

## 4. Sub-agent roles

`RoleBasedLlmPlanner` automatically selects one of three roles each iteration based on the
current loop state. Each role uses a different persona prompt and a restricted tool whitelist.

| Role | When selected | Persona | Available tools |
|---|---|---|---|
| `Planner` | Iteration 0 or no observations yet | Maps the objective, broad exploration | `list_dir`, `glob` |
| `Researcher` | Structural tools only in recent observations | Evidence gathering | `read_file`, `grep`, `glob` |
| `Coder` | `read_file` or `grep` calls in recent observations | Synthesis / conclusions | `read_file`, `grep`, `glob`, `list_dir` |

**Selection logic** (in `RoleBasedLlmPlanner.SelectRole`):

```
iteration == 0 || no observations  →  Planner
last-3 sources contain read_file or grep  →  Coder
otherwise  →  Researcher
```

Tools returned by the LLM that are not in the role's whitelist are stripped before execution.
This prevents the model from accidentally invoking a disallowed tool.

---

## 5. Built-in tools

`LocalToolBroker` exposes four built-in tools. These map directly to the file system under the
objective's `CodebaseRoot`. Ignored paths: `obj/`, `bin/`, `.git/`, `node_modules/`, `.vs/`.

| Tool name | Required args | Optional args | Description |
|---|---|---|---|
| `read_file` | `path` | — | Reads file content (relative to workspace root) |
| `list_dir` | — | `path` | Lists files/dirs in a directory |
| `grep` | `pattern` | `dir`, `extension` | Searches for a regex pattern across files |
| `glob` | `pattern` | `dir` | Matches files by glob pattern |

The LLM addresses tools by name in the `toolName` field of its JSON response. Arguments are
key/value string pairs in `arguments`.

---

## 6. Running an objective in code

### Via DI (production path)

Add the local core services during app startup. In WinUI this happens in `App.xaml.cs`:

```csharp
builder.Services.AddLocalCore();  // uses LocalOrchestratorOptions.Default
```

Custom options (e.g., tighter budget for testing):

```csharp
builder.Services.AddLocalCore(new LocalOrchestratorOptions(
    MaxIterations: 10,
    DefaultBudget: new RuntimeBudget(
        MaxToolCalls: 20,
        MaxExpandedFiles: 5,
        MaxRetriesPerError: 2,
        MaxContextTokens: 16_000)));
```

Then inject and use `ILocalOrchestrator`:

```csharp
public class MyService(ILocalOrchestrator orchestrator, FeatureFlagsService flags)
{
    public async Task RunAsync(CancellationToken ct)
    {
        if (!flags.GetFlags().LocalFunctionalityEnabled)
            return;

        var objective = new LocalObjective(
            Goal: "Find all IAgentExecutor implementations in this codebase",
            SuccessCriteria: "Return the fully-qualified type names of every class that implements IAgentExecutor",
            CodebaseRoot: WorkspaceRoot,
            CorrelationId: Guid.NewGuid());

        var profile = new RuntimeModelProfile(
            ModelId: "llama3.1:8b",
            ModelClass: "large-local",
            Provider: "ollama",
            MaxInputTokens: 32_000,
            SupportsParallelTools: false);

        var result = await orchestrator.RunAsync(objective, profile, ct);

        if (result is Ok<CompactionSnapshot> { Value: var snapshot })
        {
            // snapshot.Facts — structured facts extracted from the codebase
            // snapshot.OpenQuestions — unresolved questions
            // snapshot.CompactSummary — narrative summary
        }
        else if (result is Err<CompactionSnapshot> { Error: var error })
        {
            // error.Code — see error codes in §7
        }
    }
}
```

### Directly (tests / tools)

Wire real implementations without the DI container:

```csharp
var root = "/path/to/workspace";
var llmClient = new OllamaLlmClient(httpClientFactory, settingsService);

var orchestrator = new LocalOrchestrator(
    planner:        new LlmPlanner([llmClient]),
    toolBroker:     new LocalToolBroker(root, maxParallelTools: 1),
    compactor:      new RuleBasedContextCompactor(),
    memoryStore:    new FileSystemRuntimeMemoryStore(root + "/.ai-dev/memory"),
    resolver:       new StaticModelStrategyResolver(),
    options:        LocalOrchestratorOptions.Default);
```

For integration tests without a live Ollama instance, substitute `ILlmClient`:

```csharp
var mock = Substitute.For<ILlmClient>();
mock.Provider.Returns("ollama");
mock.CompleteAsync(default, default, default).ReturnsForAnyArgs(
    new Ok<string>("""{"intent":"done","toolRequests":[],"expectedOutcome":"done","requiresUserInput":false}"""));
```

---

## 7. Configuration reference

### `LocalOrchestratorOptions`

| Property | Default | Description |
|---|---|---|
| `MaxIterations` | 20 | Hard upper bound on loop iterations |
| `DefaultBudget.MaxToolCalls` | 50 | Max cumulative tool calls (overridden by `ToolCallBudget` in strategy) |
| `DefaultBudget.MaxExpandedFiles` | 10 | Max distinct files opened via `read_file` |
| `DefaultBudget.MaxRetriesPerError` | 3 | Retries on LLM parse failure |
| `DefaultBudget.MaxContextTokens` | 32,000 | Token budget for context compaction trigger |

### `RuntimeModelStrategy` (per model class)

| Property | `small-local` | `large-local` | Description |
|---|---|---|---|
| `PlanningDepth` | 1 | 3 | Lookahead depth hint passed to planner |
| `DiscoveryBreadth` | 3 | 8 | Max candidates per discovery phase |
| `MaxParallelTools` | 1 | 4 | Parallel tool execution concurrency |
| `MinimumConfidenceToProceed` | 0.6 | 0.5 | Confidence threshold below which discovery continues |
| `CompactionInterval` | 3 | 5 | Compact every N iterations |
| `CompactionRatio` | 0.6 | 0.8 | Force compaction when tokens exceed this fraction of `MaxContextTokens` |
| `ToolCallBudget` | 20 | 50 | Strategy-level ceiling on tool calls (0 = use `DefaultBudget.MaxToolCalls`) |

### `RuntimeModelProfile`

| Field | Description |
|---|---|
| `ModelId` | Exact model name as the LLM server expects it (e.g. `"llama3.1:8b"`) |
| `ModelClass` | Must be `"small-local"` or `"large-local"` — selects strategy |
| `Provider` | `"ollama"` or `"lmstudio"` — selects which `ILlmClient` handles the call |
| `MaxInputTokens` | Model's context window; informational only |
| `SupportsParallelTools` | Informational hint; actual parallelism is governed by `MaxParallelTools` |

### Error codes

| Code | Cause |
|---|---|
| `LocalOrchestrator.BlockedOnInput` | Planner returned `RequiresUserInput: true` |
| `LocalOrchestrator.BudgetExhausted` | Tool call count reached effective budget ceiling |
| `LocalOrchestrator.MaxIterationsReached` | Loop ran for `MaxIterations` without completing |
| `ModelStrategy.UnknownModelClass` | `ModelClass` is not `small-local` or `large-local` |
| `LlmPlanner.NoClient` | No `ILlmClient` registered for the profile's `Provider` |
| `LlmPlanner.ParseFailed` | LLM response did not contain parseable JSON after all retries |

---

## 8. Extending the feature

### Custom `ILlmClient` (e.g., LM Studio, custom endpoint)

```csharp
internal sealed class LmStudioLlmClient(IHttpClientFactory httpClientFactory) : ILlmClient
{
    public string Provider => "lmstudio";

    public async Task<Result<string>> CompleteAsync(string prompt, string modelId, CancellationToken ct = default)
    {
        // POST to LM Studio's OpenAI-compatible endpoint
        // Extract response content
        // Return new Ok<string>(content) or new Err<string>(new DomainError(...))
    }
}
```

Register alongside `OllamaLlmClient`. `RoleBasedLlmPlanner` resolves the correct client by
matching `ILlmClient.Provider` to `RuntimeModelProfile.Provider`. Registering multiple clients
is safe — the planner picks by string match.

### Custom `ILocalPlanner`

Replace `RoleBasedLlmPlanner` entirely for full control over role selection and prompting:

```csharp
internal sealed class MyPlanner(IEnumerable<ILlmClient> clients) : ILocalPlanner
{
    public async Task<Result<RuntimeActionPlan>> PlanNextAsync(
        LocalRuntimeState state, CancellationToken ct = default)
    {
        // Build your own prompt, call client, return RuntimeActionPlan
    }
}
```

Swap the registration in `LocalCoreExtensions.AddLocalCore`:
```csharp
services.AddSingleton<ILocalPlanner, MyPlanner>();
```

### Custom `IContextCompactor`

`RuleBasedContextCompactor` is a pure function — replace or subclass it to change the
keep/drop rules. The compactor receives the full `LocalRuntimeState` and must return a
`Result<CompactionSnapshot>`.

### Adding a new built-in tool

Add a case to `LocalToolBroker.ExecuteSingleAsync`:

```csharp
"my_tool" => await RunMyToolAsync(request.Arguments, ct),
```

Return a `ToolOutcome` with `Succeeded: true/false`, a `Summary` string, and an optional
`Evidence` list of strings. The new tool name becomes available to the LLM in any role that
lists it in its whitelist (`RoleBasedLlmPlanner.ToolWhitelists`).

---

## 9. Running the tests

### Unit tests

```bash
dotnet test ai-dev-net.tests.unit --filter "FullyQualifiedName~LocalOrchestrator"
dotnet test ai-dev-net.tests.unit --filter "FullyQualifiedName~StaticModelStrategyResolver"
dotnet test ai-dev-net.tests.unit --filter "FullyQualifiedName~RuleBasedContextCompactor"
dotnet test ai-dev-net.tests.unit --filter "FullyQualifiedName~InMemoryRuntimeMemoryStore"
```

Or run the entire unit suite:

```bash
dotnet test ai-dev-net.tests.unit
```

### Integration tests

The integration tests in `LocalOrchestratorEndToEndTests` use a faked `ILlmClient` and run
without a live Ollama instance:

```bash
dotnet test ai-dev-net.tests.integration --filter "FullyQualifiedName~LocalOrchestratorEndToEndTests"
```

The existing `OllamaExecutorTests` and `LmStudioExecutorTests` **do** require live endpoints.
They are skipped automatically when the executor is unavailable.

### All tests

```bash
dotnet test
```

---

## 10. Observing telemetry

The orchestrator emits OpenTelemetry spans and metrics into the existing `ServiceDefaults`
pipeline. Both are visible in the **.NET Aspire dashboard** when running the `AppHost` project.

### Spans (Activities)

| Span name | Parent | Tags |
|---|---|---|
| `LocalOrchestrator.Run` | — | `objective.id`, `model.id`, `model.class` |
| `LocalOrchestrator.Iteration` | `LocalOrchestrator.Run` | `iteration`, `tool.count`, `compaction.tokens_saved` |

### Metrics

| Metric name | Type | Description |
|---|---|---|
| `aidev.loop.iterations` | Counter | Total loop iterations across all objectives |
| `aidev.loop.tool_calls` | Counter | Total tool calls dispatched |
| `aidev.loop.successes` | Counter | Objectives that completed with `Ok` |
| `aidev.loop.failures` | Counter | Objectives that terminated with `Err` |
| `aidev.compaction.tokens_saved` | Histogram | Estimated tokens eliminated per compaction event |

### Structured logs

Each iteration emits a structured log line at `Debug` level:

```
[aidev.loop] objective={CorrelationId} iteration={N} tool_calls={M} model={ModelId} role_tools_allowed={MaxParallelTools}
```

Compaction events emit an `Information` line:

```
[aidev.loop] objective={CorrelationId} iteration={N} compaction_tokens_saved={K} estimated_tokens={T}
```

Filter by the `[aidev.loop]` prefix in any structured log viewer to isolate orchestrator output.

---

## Quick-start checklist

- [ ] Ollama installed and `ollama serve` running (or LM Studio server started)
- [ ] Model pulled: `ollama pull llama3.1:8b`
- [ ] App **Settings → Ollama Base URL** set (default: `http://localhost:11434`)
- [ ] **Preferences → Enable Local Functionality** toggled on
- [ ] Construct a `LocalObjective` with `Goal`, optional `SuccessCriteria`, and `CodebaseRoot`
- [ ] Choose `ModelClass: "small-local"` or `"large-local"` in `RuntimeModelProfile`
- [ ] Call `ILocalOrchestrator.RunAsync(objective, profile, ct)` and pattern-match on `Result<CompactionSnapshot>`
