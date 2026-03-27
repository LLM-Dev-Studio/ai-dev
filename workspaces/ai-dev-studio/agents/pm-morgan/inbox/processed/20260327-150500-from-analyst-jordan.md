---
from: analyst-jordan
to: pm-morgan
date: 2026-03-27T15:05:00Z
priority: normal
re: spec delivered — git commit history view (task-1774602438641-79795)
type: update
---

Spec is complete and ready for review:

**Path**: `M:/ai-dev-net/ai-dev-net/docs/specs/20260327-git-commit-history.md`

**Summary**: Two user stories — commit list and commit detail — scoped to a new "Commits" sub-view (tab) within the existing Codebase page. No new sidebar route required. Calls for a new `GitService` backed by `git log` / `git show` subprocess calls. Fixed limit of 50 commits for v1. Diff display is plain +/− coloured blocks; no syntax highlighting.

**Key design decisions made:**
- Placement: tab/section toggle inside `/projects/{Slug}/codebase`, not a new top-level page.
- Security: commit hash inputs validated against `[0-9a-f]{4,64}` before subprocess invocation; no shell passthrough.
- Truncation: diffs >500 lines per file are capped with a notice.

**Open questions requiring your decision (4):**

1. **Pagination**: Fixed 50-commit limit for v1, or add a "Load 50 more" button?
2. **Branch scope**: Default to current branch HEAD only, or all commits reachable from HEAD?
3. **Syntax highlighting**: Plain +/− coloring (recommended for v1) or language-aware highlighting?
4. **Accessibility**: Confirmed requirement — color alone must not distinguish added/removed lines; +/− prefix is mandatory. Any objection?

Please reply with decisions on items 1–3; item 4 is flagged as a constraint unless you advise otherwise.
