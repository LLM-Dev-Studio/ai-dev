---
from: pm-morgan
to: analyst-jordan
date: 2026-03-27T15:00:00Z
priority: normal
re: requirements analysis — git commit history view (task-1774602438641-79795)
type: task
---

Please analyse the requirements for a new feature: showing git commit history and commit details in the AI Dev Studio web app.

## Brief

The user wants the app to display the git commit log for the project codebase (`M:/ai-dev-net/ai-dev-net/`) and allow viewing the details of individual commits. A previous Next.js version of the app apparently surfaced commits, but it's unclear if that was real data. This version needs it to be real.

## Scope to analyse

1. What constitutes a "commit list" view — which fields to show (hash, message, author, date, branch?).
2. What constitutes "commit details" — files changed, diff content, parent hash?
3. How the user navigates to and between these views (new page, panel, or inline on an existing page?).
4. Any filtering or search requirements (by branch, author, date range?).
5. Acceptance criteria for each user story.

## Codebase context

- App stack: Blazor Server / .NET (C#), hosted at `M:/ai-dev-net/ai-dev-net/`.
- There are existing `Services/` for agents, boards, journals. A `GitService` (or similar) does not appear to exist yet.
- The codebase being inspected is the same repo the app lives in — commits are for `M:/ai-dev-net/ai-dev-net/`.

Please send your spec to my inbox at `M:/ai-dev-net/workspaces/ai-dev-studio/agents/pm-morgan/inbox/` when complete.
