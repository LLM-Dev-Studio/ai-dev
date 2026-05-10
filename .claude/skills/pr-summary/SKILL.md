---
name: pr-summary
description: Generates a structured PR summary for the current branch by inspecting commits and file changes since master. Use when creating a PR or when asked for a summary of branch changes.
argument-hint: [optional base branch, defaults to master]
allowed-tools: Bash(git log*), Bash(git diff*), Bash(git branch*)
---

# PR Summary

This skill generates a structured summary of all changes on the current branch relative to a base branch (default: `master`).

## Workflow

### 1. Gather branch context

```bash
git branch --show-current
git log master..HEAD --oneline
git diff master...HEAD --stat
git log master..HEAD --format="%s%n%b"
```

If the user supplied a different base branch as an argument, substitute it for `master`.

Extract the **work item number** from the branch name (first run of 4+ digits):

| Branch | Work item |
|---|---|
| `feature/12345-add-foo` | `12345` |
| `feedback/58340/from-luke` | `58340` |
| `JTD-56566-missing-screen` | `56566` |
| `docs/59478` | `59478` |

### 2. Analyse the changes

Group changes into logical themes by reading the commit messages and diff stat. Common themes for this project:

- **New features** — new screens, controls, device integrations, service bus consumers
- **Bug fixes** — correctness fixes (mention what was wrong and what was corrected)
- **Renames / breaking changes** — entity or contract renames, API changes
- **Service bus / messaging** — MassTransit consumers, message contracts, RabbitMQ configuration
- **Device integration** — RFID, camera, BlackMoth, or other device changes
- **UI changes** — WPF views, XAML resources, InTruck or Console screens
- **Dead code removal** — deleted files, unused services, obsolete utilities
- **Tooling / config** — CLAUDE.md, skills, CI, migrations, feature flags

### 3. Produce the title and description

**Title** — one line following the project's PR title format: `JTD <work-item> - Brief description`
- Use the work item number extracted from the branch name
- Brief description: imperative, ≤ 70 characters total
- Example: `JTD 58106 - Handle missing RORO bin weights using tipping records`

Output the title first in a code block, then the description below it.

**Description** — output the entire description inside a single fenced markdown code block (` ```markdown `) so the user can copy-paste it directly into Azure DevOps. Use this structure inside the block:

````markdown
```markdown
## Description — Work Item #<id>

### <Theme 1 — e.g. "New Feature: Trailer Management Screen">
- Bullet describing the change. Mention affected components (InTruck, Console, ServiceBus, Shared, Devices) where relevant.

### <Theme 2>
- ...

### Bug Fixes
- **<Short label>** — what was wrong and what was changed to fix it.

### Dead Code / Cleanup
- List deleted files or removed abstractions with a one-line rationale.

### Tooling & Config
- Any non-code changes (CLAUDE.md, skills, CI, feature flags).

---
**Files changed:** X  |  **Insertions:** +Y  |  **Deletions:** -Z
```
````

Rules:
- Use `###` headings for each theme; omit sections that have no relevant changes.
- Keep bullets concise — one sentence per item, no padding.
- Always include the `Work Item #<id>` in the description heading.
- Always include the files/insertions/deletions line at the bottom (read from `git diff --stat` summary line).
- If the branch has zero commits ahead of master, say so and exit.
- **The entire description (everything inside the markdown code block) must not exceed 3500 characters.** If the draft exceeds this limit, shorten bullets — summarise groups of related changes into a single bullet, drop minor cleanup items, and abbreviate file lists — until it fits. Never truncate mid-sentence; always produce complete, grammatical bullets.

### 4. Offer next steps

After the summary, ask if the user wants to:
- Create the PR now (`gh pr create`) using the summary as the body
- Review the changes first (`/code-review`)
- Adjust any section of the summary
