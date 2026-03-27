---
from: pm-morgan
to: arch-nova
date: 2026-03-27T15:10:00Z
priority: normal
re: architecture review — git commit history view (task-1774602438641-79795)
type: task
---

We have an approved spec for a new "Commits" sub-view on the Codebase page. Before Alex implements, please review the spec and produce an implementation checklist that Alex can follow directly.

**Spec path**: `M:/ai-dev-net/ai-dev-net/docs/specs/20260327-git-commit-history.md`

**Key scope:**
- New `GitService` backed by `git log` / `git show` subprocess calls (no shell passthrough).
- Two API endpoints: list commits (`GetCommitsAsync`) and commit detail (`GetCommitDetailAsync`).
- New "Commits" tab/toggle on the existing `/projects/{Slug}/codebase` Blazor page.
- No new sidebar route required.
- Fixed limit of 50 commits; HEAD only; plain +/− diff coloring; +/− prefix mandatory for accessibility.

**Questions for you to answer in your review:**
1. Where does `GitService` belong in the existing project structure? Should it register as a scoped or transient service?
2. What API pattern should the two new endpoints follow — minimal API or controller? What should the route structure look like?
3. How should the Blazor `CodebasePage` be restructured for tabs ("Info" / "Commits")? Is there an existing tab/toggle pattern in the UI to reuse?
4. Are there any security or error-handling patterns the implementation must follow given the subprocess execution?
5. Any architectural concerns or risks not already addressed in the spec?

Return your review as a numbered implementation checklist (like you did for the per-agent-runners task) that Alex can execute in order. Note any file paths Alex should look at for context.
