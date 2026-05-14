# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### .NET (run from repo root)

```bash
# Build solution
dotnet build ai-dev-net.slnx

# Run all unit tests
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj

# Run a single test class or method
dotnet test ai-dev-net.tests.unit/ai-dev-net.tests.unit.csproj --filter "FullyQualifiedName~AgentSlugTests"

# Run integration tests
dotnet test ai-dev-net.tests.integration/ai-dev-net.tests.integration.csproj

# Run the Aspire host (starts all services)
dotnet run --project ai-dev-net.AppHost

# Run just the API
dotnet run --project ai-dev.api

# Run the WinUI desktop app (requires Windows, x64)
dotnet run --project ai-dev.ui.winui -p:Platform=x64
```

### VS Code Extension (run from `ai-dev-vscode/`)

```bash
npm run build      # one-shot bundle
npm run watch      # incremental rebuild
npm test           # Jest unit tests
npm run package    # produce .vsix for sideloading
```

### CI mirrors

CI runs `dotnet restore ai-dev-net.slnx`, then `dotnet build` with `-c Release -p:Platform=x64`, then the unit tests. `Directory.Build.props` sets `TreatWarningsAsErrors=true` globally — the build will fail on any warning.

---

## Architecture

This is a multi-agent AI orchestration platform. The .NET backend manages projects, agents, tasks (a Kanban board), decisions, and planning sessions. A WinUI 3 desktop app and an ASP.NET Core web app provide UI. A VS Code extension surfaces agent activity in the editor sidebar.

### Layer map

```
ai-dev-net.AppHost          Aspire orchestration host — entry point for dev/deploy
ai-dev.api                  ASP.NET Core Minimal API + SignalR hub; consumed by the VS Code extension
ai-dev.ui.winui             WinUI 3 desktop app (Windows App SDK 1.8), x86/x64/ARM64
ai-dev.core                 Domain: entities, value types, service contracts, domain events
ai-dev.core.local           Local orchestration: planning sessions, transcript compaction, progressive context discovery
ai-dev.mcp                  Model Context Protocol server — workspace tools (file I/O, git, journal) exposed to LLMs
ai-dev.executor.*           Six pluggable LLM backends (Anthropic, Claude CLI, Ollama, LM Studio, GitHub Models, Copilot CLI)
ai-dev-net.ServiceDefaults  Shared DI/service configuration used by all host projects
ai-dev-vscode               VS Code extension: React 19 webviews, SignalR client, backend process manager
```

### How the pieces connect

- **Executors** implement a common contract in `ai-dev.core` and are registered via their own `Add*Executor()` extension methods. The `ModelResolver` and `AgentRunnerService` in `ai-dev.core` select and invoke them.
- **`ai-dev.core.local`** sits above `ai-dev.core` and handles the stateful session lifecycle: building prompts (`AgentPromptBuilder`), compacting transcripts (`RuleBasedContextCompactor`), running progressive discovery (`ProgressiveDiscoveryEngine`), and routing tool calls (`LocalToolBroker`).
- **`ai-dev.mcp`** exposes workspace tools (file reads, git ops, journal writes) as MCP tools. Executors that support MCP (Anthropic, Claude CLI) call these tools; the responses are routed back through the session.
- **`ai-dev.api`** is a thin HTTP layer over `ai-dev.core` services. It also hosts a SignalR hub (`ProjectStateHub`) that pushes `ProjectStateChangedEvent` domain events to connected clients (the VS Code extension).
- **`ai-dev.ui.winui`** uses the same `ai-dev.core` services directly (in-process) via DI. View models are MVVM Community Toolkit `ObservableObject` partials.
- **`ai-dev-vscode`** connects to `ai-dev.api` via REST + SignalR. `WorkspaceDetector` discovers projects by watching for `.ai-dev/project.json` files across open workspace folders; `BackendProcessManager` spawns the API process from the bundled binaries.

### Workspace layout on disk

```
<workspace-root>/
  .ai-dev/
    workspaces.json           project registry (slug → name)
    studio-settings.json
    <project-slug>/
      project.json            project metadata (codebasePath, codebaseInitialized, …)
      agents/
      board/
      decisions/
      kb/
      playbooks/
      sessions/
```

`WorkspacePaths` in `ai-dev.core` is the single source of truth for all path resolution. Every service resolves paths through it — never string-concatenate paths directly.

The VS Code extension discovers a project via `<codebase>/.ai-dev/project.json` (fields: `projectSlug`, `apiPort`). This file is separate from the studio workspace storage above.

### Domain model

See `docs/UBIQUITOUS_LANGUAGE.md` for the canonical glossary. Key terms:

- **Agent** — an autonomous LLM worker with a slug, executor, and inbox. Runs one session at a time.
- **Task / BoardTask** — a Kanban card. Assigned to an agent via `TaskAssigned` domain event.
- **Decision** — a pending question surfaced by an agent; resolved by a human or another agent.
- **Planning Session** — a multi-turn LLM conversation that produces a structured plan before execution.
- **Executor** — the runtime backend that sends prompts to an LLM and streams tool calls back.

### Value types

All domain identifiers are strongly-typed immutable records (`AgentSlug`, `ProjectSlug`, `TaskId`, `ColumnId`, `DecisionId`, …) defined in `ai-dev.core/Models/Types/`. Each has:
- Constructor validation (throws `ArgumentException` on bad input)
- A companion `*JsonConverter` registered at the serializer level
- `TryParse()` for safe deserialization

Do not use raw `string` for any domain identifier. Add a new value type if one is missing.

### Result pattern

`Result<T>` (in `ai-dev.core/Models/Result.cs`) is used instead of exceptions for recoverable errors. Use `Result.Ok(value)` / `Result.Fail(reason)` and the extension methods in `ResultExtensions`. Do not throw from service methods that can legitimately fail.

### Testing conventions

- Framework: xUnit 3, Shouldly assertions, NSubstitute mocks.
- All global usings are declared in `GlobalUsings.cs` — no per-file `using` for the standard set.
- Tests use temp directories (`Path.GetTempPath() + Guid`) for any file I/O — never relative paths.
- Value type tests follow the pattern in `AgentSlugTests.cs`: constructor validation, round-trip serialisation, equality, `TryParse`.
