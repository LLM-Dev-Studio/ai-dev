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

## Hardening Work
- Prioritize startup consistency checks, timeout/cancellation policy, and full OpenTelemetry observability via Aspire, as these directly affect users.