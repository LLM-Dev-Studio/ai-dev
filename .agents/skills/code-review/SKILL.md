---
name: code-review
description: Conducts professional and thorough code reviews for local changes or remote PRs. Checks correctness, project-specific conventions (repository/caching pattern, service contracts, MassTransit messaging, WPF patterns, NUnit tests), security, tests, and code quality. Use when asked to review code, review a PR, or check implementation against project standards.
argument-hint: [pr-number or URL]
allowed-tools: Read, Grep, Glob, Bash(git *), Bash(gh *), Bash(msbuild *), Bash(nuget *), Bash(vstest.console*)
---

# Code Review

This skill guides a professional and thorough code review for both local development and remote Pull Requests.

## Workflow

### 1. Determine Review Target

- **Remote PR**: User provides a PR number or URL (e.g., "Review PR #123") → target that remote PR.
- **Local changes**: No PR specified, or user says "review my changes" → target current local file system state (staged and unstaged).

### 2. Preparation

**For Remote PRs:**
1. Checkout the PR: `gh pr checkout <PR_NUMBER>`
2. Read the PR description and existing comments to understand goal and history: `gh pr view <PR_NUMBER>`
3. Preflight — run the standard verification suite to catch automated failures early:
   ```bash
   nuget restore JJRichards.Commercial.sln
   msbuild JJRichards.Commercial.sln /t:Rebuild /p:Configuration=Release /p:vsVersion=17.0
   vstest.console.exe "**\bin\Release\*.Tests.dll"
   ```

**For Local Changes:**
1. Check status: `git status`
2. Read diffs: `git diff` (working tree) and/or `git diff --staged` (staged)
3. Preflight (optional): If changes are substantial, ask the user whether to run the build and tests before reviewing.

### 3. In-Depth Analysis

Analyze the code changes across two categories: **universal pillars** and **project-specific rules**.

#### Universal Pillars

- **Correctness** — Does the code achieve its stated purpose without bugs or logical errors?
- **Maintainability** — Is the code clean, well-structured, and easy to modify? Does it follow established design patterns?
- **Readability** — Is formatting consistent? Are comments present only where logic isn't self-evident?
- **Efficiency** — Are there obvious performance bottlenecks or resource inefficiencies?
- **Security** — Any potential vulnerabilities? (injection, XSS, insecure deserialization, exposed secrets, OWASP Top 10)
- **Edge Cases & Error Handling** — Does the code handle edge cases and errors appropriately?
- **Testability** — Is new/modified code adequately covered by tests?

#### Project-Specific Rules

**Architecture & Patterns**
- Solution has 62 projects across Backend, UI, Tests, Devices, and Deployment — changes should respect the existing project boundaries
- Repository pattern with caching: `IRepository` / `CachingRepository` implementations for data access
- Service layer: `IService` / `Service` implementations for business logic
- Service contracts in `JJRichards.ServiceContracts` — shared interface definitions between components
- No business logic in UI code-behind; no data-access logic in services
- DI through constructor injection — services and repositories not instantiated directly

**WPF / UI Conventions**
- MVVM pattern: views in XAML, logic in code-behind or view models
- Shared UI controls live in `JJRichards.JTrack.UI.Shared`
- Custom controls and UserControls follow existing naming and structure
- XAML resources and styles should be consistent with existing themes

**Service Bus & Messaging (MassTransit / RabbitMQ)**
- Message contracts defined in `JJRichards.ServiceBus.MessageContracts`
- Service contracts in `JJRichards.ServiceContracts` and `JJRichards.ServiceContracts.Common`
- Consumer-based endpoints follow MassTransit patterns
- New consumers/messages must be registered in DI/bus configuration

**Device Integration**
- Device drivers live under `Devices/` with a common framework in `JTrack.Devices`
- Each device type has its own project (CR203X, RUTX, RuggON, BlackMoth, Comet, etc.)
- RFID integration in `JJRichards.JTrack.InTruck.RFID`

**DateTime — Always UTC**
- `DateTime.UtcNow` only — never `DateTime.Now`
- `new DateTime(...)` must pass `DateTimeKind.Utc`

**Code Conventions (.editorconfig)**
- Braces always required (`csharp_prefer_braces = true:error`)
- `var` preferred for all variable declarations
- Interfaces prefixed with `I`
- Private fields: `_camelCase`
- Public members & constants: PascalCase
- Line endings: CRLF, UTF-8
- Max line length: 200 characters
- Indentation: 4 spaces

**Compiler Hygiene**
- Zero warnings — flag any new warnings introduced
- No unused variables, unreachable code, or suppressed analyzer warnings without justification

**Tests**
- NUnit 3.x test framework
- Test projects: `*.Tests` naming convention (8 test projects in solution)
- New logic should have corresponding unit tests
- Tests run via: `vstest.console.exe "**\bin\Release\*.Tests.dll"`
- Suggest specific additional test cases that would improve coverage or robustness

**Configuration & Feature Flags**
- Feature flags in `JJRichards.Shared/FeatureFlags/`
- App.config for application configuration
- Connection strings reference SQL Server environments (dev, test, prod)
- Never hardcode connection strings or secrets

### 4. Provide Feedback

**Structure:**

- **Summary** — High-level overview of the changes and overall impression
- **Findings** (grouped by severity):
  - **Critical** — Bugs, security issues, breaking changes, or violations of project-critical rules (UTC datetimes, repository pattern, service bus contracts)
  - **Improvements** — Better code quality, missing patterns, performance, or test coverage
  - **Nitpicks** — Formatting, minor naming inconsistencies, optional style issues
- **Conclusion** — Clear recommendation: **Approved** or **Request Changes**

**Tone:**
- Constructive, professional, and specific
- Always explain *why* a change is requested, not just *what* to change
- For approvals, acknowledge the specific value of the contribution

### 5. Cleanup (Remote PRs Only)

After completing the review, ask the user if they want to switch back to the main branch:
```bash
git checkout master
```
