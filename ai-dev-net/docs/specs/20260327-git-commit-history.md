# Spec: Git Commit History View

**Date**: 2026-03-27
**Author**: analyst-jordan
**Status**: Final
**Related task**: task-1774602438641-79795

---

## Problem Statement

Users of AI Dev Studio need to inspect the version history of their project's linked codebase from within the app. Currently the Codebase page only supports initializing or linking a repository — there is no way to see what has been committed. This makes it impossible to correlate agent activity with code changes, or to review recent work without leaving the app.

---

## Scope

**In scope:**
- A commit list view showing recent commits for the project's linked codebase
- A commit detail view showing metadata and the file-level diff for a single commit
- Navigation between list and detail within the existing Codebase page (sub-view, not a new sidebar item)
- Reading real data via `git log` / `git show` executed as a process from the server

**Out of scope:**
- Pushing, pulling, branching, or any write operations to the repository
- Blame view or line-level annotation
- Comparing arbitrary commits (diff between two non-adjacent commits)
- Authentication for remote repositories
- Pagination beyond a configurable limit (initial implementation: most recent 50 commits)
- Real-time polling for new commits

---

## User Stories

### Story 1 — View commit list

**As a** project team member,
**I want** to see a list of recent commits for the project codebase,
**so that** I can track what has been built and when.

**Acceptance criteria:**

1. When the codebase is initialized (i.e. `codebaseInitialized = true` in `project.json`) and the linked path is a valid git repository, a "Commits" section or tab is visible on the Codebase page.
2. The commit list displays, for each commit: short hash (7 chars), commit message (first line only, truncated at 80 chars with ellipsis if longer), author name, and relative or absolute date (ISO 8601 UTC).
3. The list shows at most 50 commits, ordered newest-first.
4. If the codebase path is not a git repository (no `.git` directory), the Commits section shows a clear message: "This codebase is not a git repository" and no list is rendered.
5. If `git log` fails for any other reason (e.g. process error), the section shows: "Could not read commit history" with the error message in a secondary line.
6. The commit list loads within 3 seconds on a repository with up to 10,000 commits on a local filesystem.
7. The list is scrollable; the page does not reflow when the list is present.

### Story 2 — View commit details

**As a** project team member,
**I want** to click a commit and see its full details and file diff,
**so that** I can understand exactly what changed in that commit.

**Acceptance criteria:**

1. Clicking any row in the commit list navigates to (or reveals) a detail view for that commit.
2. The detail view shows: full commit hash, full commit message (all lines), author name and email, committer name and email (if different from author), authored date (ISO 8601 UTC), parent hash(es) as short hashes.
3. The detail view shows a list of files changed, with each file's status (Added, Modified, Deleted, Renamed) and the filename.
4. For each changed file, the unified diff is displayed in a monospace block with added lines visually distinguished from removed lines (e.g. green/red background or +/- prefix clearly visible).
5. A "Back to commits" control returns the user to the commit list, preserving the scroll position.
6. If the commit hash does not exist in the repository (e.g. stale URL), the detail view shows: "Commit not found."
7. Diff output for a single commit loads within 5 seconds for commits changing up to 100 files on a local filesystem.
8. Binary file diffs are not rendered; instead show: "Binary file — diff not shown."

---

## Edge Cases and Constraints

- **Empty repository** (no commits yet): the Commits section shows "No commits yet."
- **Detached HEAD**: list should still render commits reachable from HEAD.
- **Very large diffs** (>500 lines per file): truncate rendered diff at 500 lines and show: "Diff truncated — showing first 500 lines."
- **Merge commits**: show both parent hashes; do not special-case the display otherwise.
- **Non-UTF-8 commit messages**: sanitize or replace invalid bytes; do not crash.
- **Process execution security**: the git executable path must be configurable or resolved from PATH; no user-supplied input is passed directly to the shell. Commit hashes accepted as input must be validated against the pattern `[0-9a-f]{4,64}` before being passed to `git show`.
- **Windows paths**: the service must handle Windows-style paths for the codebase directory (backslashes).
- **Concurrent requests**: the server may serve multiple users; the `GitService` must not share mutable state between calls.

---

## Navigation and UI Placement

- The Commits view lives within the existing `/projects/{Slug}/codebase` page, not as a new sidebar route.
- Recommended layout: add a tab bar or section toggle ("Info" / "Commits") to the Codebase page. The existing "Project Info" card becomes the "Info" tab.
- The commit list and detail view replace (not append to) the page content area below the tab bar.
- No new sidebar navigation item is required.

---

## Data Model (for `GitService`)

The service should expose at minimum:

```
GetCommitsAsync(repoPath, maxCount) → List<CommitSummary>
GetCommitDetailAsync(repoPath, hash) → CommitDetail
```

**CommitSummary**: `Hash` (short), `MessageFirstLine`, `AuthorName`, `AuthoredAt` (DateTimeOffset)
**CommitDetail**: `Hash` (full), `MessageFull`, `AuthorName`, `AuthorEmail`, `AuthoredAt`, `CommitterName`, `CommitterEmail`, `CommittedAt`, `ParentHashes`, `Files` (list of `ChangedFile`), `Diff` (raw unified diff string)
**ChangedFile**: `Status` (Added/Modified/Deleted/Renamed), `Path`, `OldPath` (for renames)

The service must invoke `git` as a subprocess (e.g. via `System.Diagnostics.Process`) using the `--no-pager` flag and parseable output formats (`--format`, `--name-status`). It must not shell out using `cmd.exe` or `bash -c`.

---

## Open Questions

All questions resolved by pm-morgan on 2026-03-27.

1. **Pagination vs. fixed limit**: ~~Should the user be able to load more than 50 commits?~~ **Resolved**: Fixed 50-commit limit for v1. No "Load 50 more" button. Revisit if users request it.
2. **Branch filter**: ~~Show all commits or current branch HEAD?~~ **Resolved**: HEAD only (current branch). Natural default; no cross-branch display for v1.
3. **Syntax highlighting**: ~~Language-aware highlighting?~~ **Resolved**: Plain +/− coloring for v1. No language-aware highlighting — avoids adding a dependency for marginal gain.
4. **Accessibility**: ~~+/− prefix requirement?~~ **Resolved**: +/− prefix constraint stands as a hard requirement. Color alone must not be the sole indicator.
