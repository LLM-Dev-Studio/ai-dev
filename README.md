# AI Dev Studio

A multi-agent AI workspace platform for orchestrating agent-driven software development. AI agents — developer, architect, designer, QA, DevOps, PM, analyst, and more — collaborate on projects through pluggable LLM backends, with each agent maintaining its own inbox, journal, decision log, and knowledge base.

## Features

- **Multi-agent orchestration** — role-based agents collaborate asynchronously via domain events on a shared project board
- **Pluggable LLM executors** — swap between Anthropic (Claude), Ollama, LM Studio, GitHub Models, and GitHub Copilot CLI without changing application logic
- **Local-first support** — run full agent workflows against locally-hosted models with no cloud calls or API keys required
- **Progressive code discovery** — structured search → slice-read → synthesise pipeline avoids loading full files into context
- **Context compaction** — automatic transcript compaction with citation preservation to stay within model context budgets
- **Model strategy resolution** — planning depth, discovery breadth, and tool parallelism are tuned per model capability
- **MCP workspace tools** — file I/O, directory listing, agent status, journal writing, and git coordination via Model Context Protocol, with audit logging and path validation
- **Two UI options** — Windows desktop (WinUI 3) and web (ASP.NET Core Razor Components)

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10, C# 13 |
| Desktop UI | Windows App SDK (WinUI 3) — x86, x64, ARM64 |
| Web UI | ASP.NET Core Razor Components |
| Orchestration | .NET Aspire |
| LLM backends | Anthropic SDK, Claude Code CLI, Ollama, LM Studio, GitHub Models, Copilot CLI |
| Patterns | MVVM Community Toolkit, Result<T>, immutable records |

## Project Structure

```
ai-dev.core/            Core domain entities, feature services, executor contracts, telemetry
ai-dev.core.local/      Local-first orchestration: planning, progressive discovery, compaction, memory
ai-dev.executor.*/      Model-specific executors (Anthropic, Claude, Ollama, LM Studio, etc.)
ai-dev.mcp/             Model Context Protocol implementation and workspace tools
ai-dev-net/             Web UI (ASP.NET Core Razor Components)
ai-dev.ui.winui/        Desktop UI (Windows App SDK / WinUI 3)
ai-dev-net.AppHost/     Aspire app host (dev/deploy entry point)
workspaces/             Agent role templates and workspace definitions
docs/                   Architecture design docs and developer guides
```

## Agent Roles

Pre-configured agent templates are included for: Developer, Architect, Designer, QA, DevOps, Product Manager, Analyst, Growth Marketer, Guard, and Process Evolution. Each template defines the agent's system prompt, tool access, and interaction patterns for inbox, journal, decisions, and knowledge base.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads) (for desktop UI)
- At least one LLM backend:
  - **Cloud**: Anthropic API key
  - **Local**: [Ollama](https://ollama.com) or [LM Studio](https://lmstudio.ai)

### Run

```bash
# Web UI via Aspire
dotnet run --project ai-dev-net.AppHost

# Desktop UI
dotnet run --project ai-dev.ui.winui
```

See [docs/guides/local-llm-dev-guide.md](docs/guides/local-llm-dev-guide.md) for full setup instructions when using local models.

## Documentation

- [Local LLM Developer Guide](docs/guides/local-llm-dev-guide.md)
- [Local Functionality Core Blueprint](docs/design/20260422-local-functionality-core-blueprint.md)
- [Per-Agent Executor Selection](ai-dev-net/docs/specs/20260327-per-agent-executor-selection.md)
- [Provider Switching Design](ai-dev-net/docs/design/20260410-provider-switch.md)

## License

[Apache 2.0](LICENSE)
