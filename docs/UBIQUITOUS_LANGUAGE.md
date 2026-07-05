# Ubiquitous Language

## Actors

| Term | Definition | Aliases to avoid |
|---|---|---|
| **Agent** | An AI persona configured with a role, model, and executor that works autonomously on tasks | Bot, assistant, worker |
| **Executor** | The LLM backend platform an Agent uses to run (e.g. Claude, Ollama, LM Studio, GitHub Models) | Provider, model, backend |
| **Developer** | The human who monitors agents, resolves decisions, and processes messages via the UI | User, operator |

## Projects & Workspaces

| Term | Definition | Aliases to avoid |
|---|---|---|
| **Project** | A named workspace that owns a Board, a set of Agents, and all associated data | Workspace (as a synonym — see note below) |
| **ProjectSlug** | The URL-safe identifier for a Project (lowercase, hyphens) | Project ID, project name |
| **Workspace** | The root file-system directory that contains one or more Projects | Repo root, working directory |

> Note: "Workspace" refers to the directory; "Project" refers to the domain entity within it. These are distinct concepts at different levels of scope.

## Work Items

| Term | Definition | Aliases to avoid |
|---|---|---|
| **Task** | A unit of work on the Board, owned by a column, optionally assigned to an Agent | Ticket, item, issue, card |
| **Decision** | A question raised by an Agent that requires a human resolution before work can continue | Question, blocker, prompt |
| **Message** | A notification sent to an Agent's Inbox, carrying an assignment, decision reply, or nudge | Notification, event, alert |

## Agent Execution

| Term | Definition | Aliases to avoid |
|---|---|---|
| **Agent Session** | A single invocation of an Agent from start to completion or failure | Run (as a noun — see note below) |
| **Launch** | The act of starting an Agent Session | Start, run (as a verb — prefer Launch in code, Run in UI labels) |
| **Stop** | The act of terminating a running Agent Session | Kill, cancel, abort |
| **Transcript** | The full conversation history of an Agent Session, including tool calls and model responses | Log, history, output |
| **Failover** | The automatic switch to a fallback Executor when the primary Executor is unavailable | Fallback, retry |

> Note: "Run" is acceptable as a UI label (button text) but should not appear in code — use **Launch** in service and API names.

## Planning

| Term | Definition | Aliases to avoid |
|---|---|---|
| **Planning Session** | A structured multi-phase conversation that produces a plan DSL from a business goal | Session (alone — too ambiguous; always qualify as Planning Session) |
| **Phase** | One of the three sequential stages of a Planning Session: Business Discovery, Solution Shaping, or Planning Decomposition | Step, stage |
| **DSL Artefact** | A structured text output produced at the end of a Planning Session phase (BusinessDsl, SolutionDsl, PlanDsl) | Output, document, result |

## Board

| Term | Definition | Aliases to avoid |
|---|---|---|
| **Board** | The Kanban board for a Project containing all Tasks organised into Columns | Kanban, sprint board |
| **Column** | A named stage in the Board workflow (Backlog, In Progress, Review, Done) | Lane, bucket, status |

## Inbox & Decisions

| Term | Definition | Aliases to avoid |
|---|---|---|
| **Inbox** | The queue of unprocessed Messages for an Agent | Mailbox, queue, feed |
| **Process** | The act of acknowledging and clearing a Message from the Inbox | Read, dismiss, archive |
| **Resolve** | The act of supplying an answer to a Decision, unblocking the waiting Agent | Answer, close, respond |
| **Nudge** | A message sent by the Overwatch system to prompt an idle Agent to re-engage | Reminder, ping |

## Status & Priority

| Term | Definition | Aliases to avoid |
|---|---|---|
| **Idle** | Agent state: no active Session | Stopped, inactive, offline |
| **Running** | Agent state: an Agent Session is currently in progress | Active, busy, executing |
| **Pending** | Decision state: not yet resolved | Open, unanswered, active |
| **Resolved** | Decision state: a human has supplied an answer | Closed, answered, done |
| **Priority** | Urgency level of a Task, Decision, or Message: Low, Normal, High, Critical | Severity, importance |

## VS Code Extension

| Term | Definition | Aliases to avoid |
|---|---|---|
| **Sidebar Panel** | A VS Code WebviewView that surfaces Agent status, Messages, or Decisions without leaving the editor | Tool window, pane, view |
| **Backend Process** | The locally-running `ai-dev.api` .NET host managed by the extension | Server, service, daemon |
| **Project Config** | The `.ai-dev/project.json` file committed to the repository that identifies the Project and API port | Settings file, workspace file |

---

## Relationships

- A **Project** owns one **Board**, many **Agents**, many **Decisions**, and a **Workspace** root directory.
- A **Board** contains one or more **Columns**; each **Column** contains zero or more **Tasks**.
- An **Agent** has one **Inbox**, produces **Agent Sessions**, and may raise **Decisions**.
- A **Decision** is raised by an **Agent** and resolved by a **Developer**; resolving it sends a **Message** back to the Agent's **Inbox**.
- A **Planning Session** produces **DSL Artefacts** at each **Phase** and is distinct from an **Agent Session**.
- A **Message** belongs to exactly one **Agent's Inbox** and has exactly one **MessageSource** (Board, Human, Overwatch, or another Agent).

---

## Example dialogue

> **Dev:** "When an **Agent** gets stuck on a **Task**, does it raise a **Decision** or send a **Message**?"

> **Domain expert:** "It raises a **Decision** — that's specifically for questions that need a human answer before work can continue. A **Message** is more like a notification or an assignment. The **Decision** also puts a **Message** in the Agent's own **Inbox** so the reply comes back through the same channel."

> **Dev:** "So the **Developer** resolves the **Decision**, and that automatically sends a **Message**?"

> **Domain expert:** "Exactly. And once the **Message** is processed, the **Agent** can continue its **Agent Session**. The **Decision** moves to **Resolved** state; the **Task** stays on the **Board** in whatever **Column** it was in."

> **Dev:** "What if the **Executor** goes down mid-session?"

> **Domain expert:** "That triggers a **Failover** — the **Agent** switches to the fallback **Executor** and continues the **Agent Session**. If no fallback is available, the session ends in failure and the **Agent** status shows as Error, not Idle."

---

## Flagged ambiguities

- **"Session"** is overloaded. The codebase uses it for both **Agent Sessions** (a single invocation of an Agent) and **Planning Sessions** (a structured multi-phase planning conversation). Always qualify: never say "session" alone — say **Agent Session** or **Planning Session**.

- **"Run" (verb vs. noun)**: UI labels say "Run" and "Stop" on buttons, but service code says `LaunchAgent` / `StopAgent`. Canonical choice: use **Launch** in all code (service names, API routes, method names); **Run** is acceptable only in UI-visible labels. Never use Run as a noun (say **Agent Session** instead).

- **"Task" vs. "BoardTask"**: The code class is `BoardTask` but the domain term is **Task**. Outside of the code layer, always say **Task**. The `BoardTask` class name is an implementation detail to avoid collision with .NET's `System.Threading.Tasks.Task`.

- **"Workspace" vs. "Project"**: These were used interchangeably in early design conversation. They are distinct: **Workspace** is the directory; **Project** is the domain entity. The setting being migrated to `.ai-dev/project.json` is **Project Config**, not workspace config.

- **`PlanningPhase` vs. `SessionPhase`**: Two enums in the codebase represent the same three conceptual phases (Business Discovery, Solution Shaping, Planning Decomposition). The domain term is **Phase**. The enum split is an implementation detail.
