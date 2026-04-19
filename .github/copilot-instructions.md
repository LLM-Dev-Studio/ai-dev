# Copilot Instructions

## Project Guidelines
- The user prefers stronger correctness through domain objects that encapsulate state changes and validity rules, and wants to avoid pervasive mutable DTO-style classes across the codebase.
- Apply stronger correctness patterns in the following priority order: 
  - Introduce `Result<T>`/`DomainError` first
  - Replace magic strings with value types
  - Enforce aggregate boundaries
  - Implement lightweight domain events
  - Utilize functional composition
- Prioritize introducing an `IDomainEventDispatcher` before adding more event handlers.
- Convert `AgentService` and `WorkspaceService` to `Result<T>`.
- Implement a `TaskAssigned` inbox handler after the above services.
- Follow up with converting `PlaybookService` and `KbService`.
- Leave mostly read-only services like `GitService`, `MessagesService`, and `JournalsService` on null/empty semantics.
- Model supported executor types in `agent.json` as a first-class supported type rather than a magic string, and ensure executor details are editable in the agent detail UI.

### Testing & UI
- Prefer testing WinUI behavior through ViewModel/unit tests before adding UI automation.
- Provide UI automation only as exploratory samples or documentation; do not rely on them as primary CI tests.
- In ViewModel tests, assert command execution, property changes, navigation intents, and interactions with services via interfaces/mocks.

## Application Hosting
- Aspire must start both the MCP server and the web app; do not simplify AppHost to only run the web project.

## Hardening Work
- Prioritize startup consistency checks, timeout/cancellation policy, and full OpenTelemetry observability via Aspire, as these directly affect users.