# PRD: Agent Memory and Dreaming

## Problem

Agents in ai-dev are effective within a session but stateless across sessions. Each agent starts fresh with only its static `CLAUDE.md` system prompt and the current inbox. There is no mechanism for:

- An agent to carry learnings from one session into the next
- Multiple agents working in the same project to share discovered patterns
- The system to improve itself based on accumulated operational experience

The result is repeated mistakes, redundant investigation, and an inability to compound knowledge over time — despite transcripts already existing that contain exactly these learnings.

---

## Goals

1. Give agents a persistent, self-managed **memory store** they can read from and write to during sessions.
2. Give projects a **shared memory layer** that multiple agents can read (and optionally write) concurrently.
3. Introduce **Dreaming** — an out-of-band batch process that reads recent session transcripts across agents and automatically produces curated, up-to-date memory content.
4. Provide full **version history and attribution** on all memory writes, for auditability and debugging.

---

## Non-Goals

- Replacing `CLAUDE.md` — that remains the agent's static session protocol authored by humans or templates.
- External vector/semantic search over memory — this is file-system based, navigated by the agent using familiar tools.
- Real-time memory sync during a live session — memory is committed at explicit write points.

---

## Memory

### Concept

Memory is modelled as a managed file system — a directory of markdown files that an agent can read, create, update, and delete using its existing tools (bash, grep, file reads). Claude Opus 4.7 is already state-of-the-art at file-system memory management, so no special tooling is needed; the harness just needs to surface the right directory and set the rules.

### Directory layout

Two scopes of memory exist per project:

```
workspaces/{project-slug}/
├── memory/
│   └── shared/              # Project-wide knowledge (shared across all agents)
│       ├── MEMORY.md        # Index file — one-line pointer per memory file
│       ├── *.md             # Individual memory files (topic-scoped)
│       └── .history/        # Append-only audit log (see Version History)
│
agents/{agent-slug}/
├── memory/                  # Agent working memory (private to this agent)
│   ├── MEMORY.md
│   ├── *.md
│   └── .history/
```

**Shared memory** (`memory/shared/`) is the project-level knowledge base — runbooks, cross-agent patterns, discovered environment facts. Agents have read-write access by default but a flag in `agent.json` can restrict an agent to read-only (e.g. a guard or analyst that should not modify shared state).

**Agent working memory** (`memory/`) is private to the agent. It contains session-specific learnings, task context, and notes that are specific to that agent's role.

### `MEMORY.md` index

Each memory store has a `MEMORY.md` index file. This is the entry point Claude reads first. Each line is a one-line pointer to a memory file: `- [Title](file.md) — one-line hook`. Claude uses this to decide which files to load without reading the entire store.

The index is always loaded into the system prompt preamble (with a line cap to prevent token explosion). Full memory files are loaded on demand.

### Permission scopes

Controlled via a new field in `agent.json`:

```json
"memoryAccess": {
  "shared": "read-write",   // "read-only" | "read-write" | "none"
  "private": "read-write"   // always read-write for the agent's own memory
}
```

Default is `read-write` for shared memory. The `CLAUDE.md` system prompt informs the agent of its scope.

### Session integration

At session start, the `ExecutorContext` is extended with memory paths. The system prompt preamble injected by `AgentRunnerService` includes:

- The content of `MEMORY.md` for each accessible memory store
- The absolute paths to each store, so the agent knows where to write

The agent uses its existing file tools to read specific memory files, append notes, or create new files. At session end, the runner does not need to do anything — the agent has already written directly to disk.

### Optimistic concurrency

When two agents write to the same shared memory file concurrently, writes must not silently clobber each other. Each write is guarded by a **precondition hash** — the SHA-256 of the file's content at read time, stored as frontmatter in memory files:

```yaml
---
precondition: sha256:a3f1...
last-written-by: developer-standard
last-written-at: 2026-05-10T03:22:00Z
---
```

A helper MCP tool (`write_memory`) checks that the current file hash matches the precondition before applying the write. On mismatch, the tool returns a conflict error and the agent re-reads the file and merges manually. Direct file writes via bash bypass this check, which is acceptable for agent working memory (single-writer) but not for shared memory.

### Version history

All writes to shared memory are recorded in `.history/` as append-only log entries:

```
.history/{YYYY-MM-DD}T{HHmmss}Z-{agent-slug}.md
```

Each entry contains:
- The full file diff (unified diff format)
- Attribution: agent slug, session start timestamp, model used
- Precondition hash before and content hash after

This gives developers a full audit trail. Agents can also read `.history/` to understand how memory evolved.

---

## Dreaming

### Concept

Dreaming is a batch, asynchronous process that reads session transcripts from recent agent sessions across a project, identifies cross-agent patterns (mistakes, successful strategies, redundant entries, stale content), and produces an updated memory diff to apply to the shared memory store.

It runs **out of band** — never during a live agent session, never on the hot path. It is the mechanism that compounds knowledge at the system level, beyond what any single agent can observe from within its own task context.

### Trigger modes

Three supported trigger modes:

| Mode | How |
|------|-----|
| **Post-session** | Triggered automatically when an agent session completes (configurable per project) |
| **Scheduled** | Cron-style, configured in `project.json` (e.g. nightly at 02:00) |
| **Manual** | Via UI button or API call on a project |

Post-session dreaming is the default for projects with shared memory enabled.

### Inputs

A dreaming job is scoped to a project and configured with:

- `lookbackDays` — how many days of transcripts to scan (default: 7)
- `targetMemoryStore` — `"shared"` or a specific agent slug (defaults to `"shared"`)
- `agentSlugs` — optional filter; if empty, all agents in the project are included

The dreaming agent is given read-only access to:
- All transcript files within the lookback window across included agents: `agents/*/transcripts/*.md`
- The current state of the target memory store

### Process

Dreaming runs as a new agent session in an isolated scratch workspace (following the same pattern as `InsightsService`). The dreaming agent is instructed to:

1. **Read** the current `MEMORY.md` index of the target store and identify what is already known
2. **Scan** each transcript file and extract notable events: errors, retried strategies, resolved blockers, repeated investigations, successful patterns
3. **Cross-reference** findings across agents — look for patterns no single agent would have noticed (e.g. the same tool call failing 60 seconds after a CPU spike across multiple agents)
4. **Produce a diff** — a set of file operations:
   - New memory files to create
   - Existing files to update (with inline diff annotation)
   - Stale entries to remove
   - Duplicate entries to consolidate
   - Verification notes to append (confirming still-accurate existing content)
5. **Write the diff** to a staging area: `memory/shared/.dreaming/{job-id}/`

### Output and application

The dreaming job produces a structured result stored in `memory/shared/.dreaming/{job-id}/`:

```
{job-id}/
├── summary.md          # Human-readable summary of what was found and changed
├── diff/               # Proposed file changes (new or modified .md files)
│   └── *.md
├── removals.json       # List of files or sections to remove
└── metadata.json       # Job ID, timestamps, agents scanned, transcripts read
```

By default, diffs are **applied automatically** after the job completes. A project-level setting `dreamingRequiresApproval: true` puts them into a pending state requiring human review via the UI before application.

When applied, each change is committed to the memory store with attribution to the dreaming job (not to any individual agent session), and a `.history/` entry is written.

### New service: `DreamingService`

Responsible for:
- Scheduling and triggering dreaming jobs
- Building the executor context for the dreaming agent (isolated workspace, transcript read paths, memory store paths)
- Reading the diff output and applying changes to the target memory store
- Writing version history entries for applied changes
- Emitting a `ProjectStateChangeKind` to notify the UI

```csharp
public interface IDreamingService
{
    Task<DreamingJob> StartJobAsync(ProjectSlug project, DreamingJobOptions options, CancellationToken ct);
    Task<DreamingJob?> GetJobAsync(ProjectSlug project, DreamingJobId jobId);
    Task ApplyJobAsync(ProjectSlug project, DreamingJobId jobId, CancellationToken ct);
    IReadOnlyList<DreamingJob> GetRecentJobs(ProjectSlug project, int count = 10);
}
```

`DreamingJobOptions`:

```csharp
public record DreamingJobOptions(
    int LookbackDays = 7,
    string TargetMemoryStore = "shared",
    IReadOnlyList<string>? AgentSlugs = null,
    bool AutoApply = true);
```

---

## Data model changes

### `project.json` additions

```json
"memory": {
  "enabled": true,
  "dreaming": {
    "enabled": true,
    "trigger": "post-session",       // "post-session" | "scheduled" | "manual"
    "schedule": "0 2 * * *",         // cron expression (if trigger = "scheduled")
    "lookbackDays": 7,
    "requiresApproval": false
  }
}
```

### `agent.json` additions

```json
"memoryAccess": {
  "shared": "read-write"   // "read-only" | "read-write" | "none"
}
```

### New file path constants

```
memory/shared/              → AgentSharedMemoryDir(projectSlug)
memory/shared/.history/     → AgentSharedMemoryHistoryDir(projectSlug)
memory/shared/.dreaming/    → AgentDreamingStagingDir(projectSlug)
agents/{slug}/memory/       → AgentPrivateMemoryDir(projectSlug, agentSlug)
```

---

## System prompt integration

`AgentRunnerService` injects a memory preamble into the system prompt before the agent-specific `CLAUDE.md` content. The preamble includes:

- The agent's accessible memory stores and their permission scopes
- The full contents of each accessible `MEMORY.md` index (capped at 150 lines)
- Absolute paths to each store
- The rule: "Do not write shared memory using raw bash. Use the `write_memory` MCP tool to ensure concurrency safety."

---

## MCP tools

Two new MCP tools exposed to agents:

### `read_memory_file`
Reads a specific memory file and returns its content along with the current precondition hash. Required before any write.

### `write_memory`
Writes to a memory file with precondition checking. Parameters: `path`, `content`, `precondition` (hash from the most recent read). Returns success or a conflict error if the file was modified since the read.

These tools are implemented in the existing MCP server and registered per-session based on the agent's `memoryAccess` config.

---

## UI changes

### Agent detail page
- Show memory store links (private memory, shared memory)
- Show `memoryAccess` scope selector

### Project settings page
- Memory enable/disable toggle
- Dreaming configuration (trigger mode, lookback, approval required)

### Dreaming jobs panel (new, per project)
- List of recent and in-progress dreaming jobs
- Job detail: summary, diff viewer, apply/reject buttons (when `requiresApproval: true`)
- Manual "Run dreaming now" action

### Memory store viewer (new, per project)
- File tree of `memory/shared/`
- Inline content viewer
- Version history timeline (from `.history/`)

---

## Phased delivery

### Phase 1 — Private agent memory
- Create `agents/{slug}/memory/` directory structure
- Inject private `MEMORY.md` into system prompt preamble
- Add `read_memory_file` and `write_memory` MCP tools (no concurrency check needed for private memory)
- Validate with one agent role (developer)

### Phase 2 — Shared project memory
- Create `memory/shared/` structure
- Add `memoryAccess` to `agent.json` and project settings UI
- Add precondition hash checking to `write_memory`
- Add `.history/` audit log writes

### Phase 3 — Dreaming
- Implement `DreamingService` and `DreamingJob` model
- Post-session trigger (off by default, opt-in per project)
- Apply-immediately mode (no approval gate)
- Dreaming jobs panel in UI

### Phase 4 — Dreaming controls
- Scheduled trigger via cron
- Approval-required mode with diff viewer
- Manual trigger from UI
- Version history viewer

---

## Open questions

- **Memory staleness**: How does an agent know when memory is stale relative to the codebase it is working on? Should dreaming emit staleness signals?
- **Memory size limits**: What is the maximum acceptable size of a memory store before it degrades context quality? Should dreaming be responsible for pruning?
- **Dreaming model selection**: Should dreaming always use the project's default model, or should it have its own configurable model (potentially a more capable/expensive one run less frequently)?
- **Private memory and dreaming**: Should dreaming have read access to individual agent private memory stores to find cross-cutting patterns, or only shared memory?
