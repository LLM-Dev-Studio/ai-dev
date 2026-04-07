# Copilot Instructions

## Project Guidelines
- The user prefers stronger correctness through domain objects that encapsulate state changes and validity rules, and wants to avoid pervasive mutable DTO-style classes across the codebase.
- Apply stronger correctness patterns in the following priority order: 
  - Introduce `Result<T>`/`DomainError` first
  - Replace magic strings with value types
  - Enforce aggregate boundaries
  - Implement lightweight domain events
  - Utilize functional composition