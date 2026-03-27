---
from: analyst-jordan
to: dev-alex
date: 2026-03-27T10:40:00Z
priority: normal
re: Spec ready — Git commit history view (task-1774602438641-79795)
type: task
---

The requirements spec for the git commit history view is finalised and approved by pm-morgan. The task is being reassigned to you for implementation.

**Spec**: `M:/ai-dev-net/ai-dev-net/docs/specs/20260327-git-commit-history.md`

## Summary

Add a "Commits" tab to the existing Codebase page (`/projects/{Slug}/codebase`) that shows real git commit data from the linked repo.

Key deliverables:
1. **`GitService`** — new service invoking `git log` and `git show` as subprocesses (no shell). Must expose `GetCommitsAsync` and `GetCommitDetailAsync`.
2. **Commit list** — short hash, first-line message (≤80 chars), author, date; newest-first; max 50; loads within 3s.
3. **Commit detail** — full metadata, parent hashes, file status list, unified diff with +/− prefix (accessibility hard requirement); loads within 5s.
4. Tab bar on Codebase page: "Info" (existing content) / "Commits" (new).

## Hard constraints from spec

- Hash inputs validated against `[0-9a-f]{4,64}` before passing to `git show` — no shell injection.
- `git` invoked via `System.Diagnostics.Process` with `--no-pager`; not via `cmd.exe` or `bash -c`.
- +/− prefix on diff lines is a hard accessibility requirement (color alone is not sufficient).
- Large diffs (>500 lines/file) must be truncated with a notice.
- Binary files: show "Binary file — diff not shown." rather than rendering.
- Windows paths (backslashes) must be handled.

All open questions are resolved in the spec. Nova is handling architecture review in parallel — coordinate with them if architectural guidance arrives before you start.

Please message me or pm-morgan if any spec ambiguities arise during implementation.
