---
name: commit-changes
description: Composes a well-structured commit message following the project's hybrid Conventional Commits format ([Scope] type: description). Inspects staged changes, infers the appropriate scope and type, and presents a draft for review. Use when asked to write a commit message, stage a commit, or check if a commit message follows project conventions.
argument-hint: [optional context or description of the change]
allowed-tools: Bash(git status), Bash(git diff*), Bash(git log*)
---

# Commit Message

This skill composes commit messages that follow the project's hybrid **Conventional Commits** format.

## Format

```
[Scope] type: short imperative description

Optional body — explain *why*, not *what*. Wrap at 72 characters.
Use multiple paragraphs if needed.

#12345
BREAKING CHANGE: description of what broke and how to migrate
```

### Rules

- **Subject line** (first line): `[Scope] type: description`
  - Max 72 characters total
  - Imperative mood — "add", "fix", "remove", not "added", "fixes"
  - No period at the end
  - Lowercase type and description; capitalised `[Scope]`
- **Blank line** between subject and body (if body is present)
- **Body**: explain the *why* behind the change, not what the diff already shows
- **`#<id>` footer is required** — links the commit to the Azure DevOps work item. Extract the number from the branch name (see Workflow step 1). If no number is found, ask the user before proceeding.
- **Footers**: each on its own line after a blank line; use `BREAKING CHANGE:` for breaking changes

---

## Scopes

Use the component(s) directly affected. If a commit touches multiple components, list the primary one or use `Multi`.

| Scope | When to use |
|---|---|
| `[InTruck]` | JJRichards.JTrack.InTruck.Commercial — WPF driver application |
| `[Console]` | JJRichards.JTrack.Console.Commercial — WPF operations console |
| `[ServiceBus]` | JJRichards.ServiceBus — MassTransit/RabbitMQ service bus |
| `[Contracts]` | JJRichards.ServiceContracts — service contract interfaces and message types |
| `[Shared]` | JJRichards.Shared — common utilities, configuration, feature flags |
| `[Devices]` | Device framework and drivers (CR203X, RUTX, RuggON, BlackMoth, Comet) |
| `[RFID]` | JJRichards.JTrack.InTruck.RFID — RFID reader integration |
| `[Camera]` | Camera/video system (CameraDaemon, CameraProvider, VideoRecorder) |
| `[UI.Shared]` | JJRichards.JTrack.UI.Shared — shared WPF controls and resources |
| `[Integration]` | JJRichards.JTrack.GCCCIntegrationService |
| `[PhoneIn]` | JJRichards.PhoneIn.Service |
| `[Upload]` | JJRichards.JTrack.UploadFiles.Commercial |
| `[Map]` | JJRichards.JTrack.MapControl, JTrackMapZipper |
| `[Tests]` | Test projects |
| `[CI]` | CI/CD pipelines, azure-pipelines-ci.yml |
| `[Docs]` | Documentation, CLAUDE.md, README, Docs/ |
| `[Multi]` | Commit spans more than two unrelated scopes |

---

## Types

| Type | Maps to | When to use |
|---|---|---|
| `feat` | MINOR | New feature or capability visible to users |
| `fix` | PATCH | Bug fix |
| `refactor` | — | Code restructure with no behaviour change |
| `perf` | PATCH | Performance improvement |
| `test` | — | Adding or updating tests only |
| `docs` | — | Documentation only |
| `chore` | — | Tooling, config, dependencies, migrations with no logic change |
| `style` | — | Formatting, whitespace — no logic change |
| `ci` | — | CI/CD pipeline changes |
| `build` | — | Build system or NuGet/package changes |

**Breaking changes** — append `!` to the type and add a `BREAKING CHANGE:` footer:
```
[Contracts] feat!: rename TruckEvents to VehicleEvents

BREAKING CHANGE: TruckEvents contract removed. Use VehicleEvents.
Existing consumers must update their subscriptions.
```

---

## Workflow

### 1. Inspect staged changes and extract work item number

```bash
git branch --show-current
git status
git diff --staged
```

**Extract the work item number** from the branch name by finding the first run of digits (4+ digits). Common branch patterns:

| Branch | Work item |
|---|---|
| `feature/12345-add-foo` | `12345` |
| `feedback/58340/from-luke` | `58340` |
| `JTC-56566-missing-screen` | `56566` |
| `docs/59478` | `59478` |
| `bugfix/99-fix-bar` | `99` |

If no number is found in the branch name, ask the user to provide the Azure DevOps work item ID before continuing — it is required.

If nothing is staged, check unstaged changes with `git diff` and ask the user whether they want to stage everything or specific files before continuing.

### 2. Infer scope and type

- Match changed file paths to the scope table above
- Determine the type from the nature of the changes (new screen = `feat`, bug correction = `fix`, config change = `chore`, etc.)
- If the change is breaking (renamed/removed contract, service endpoint changed), note `!` and prepare a `BREAKING CHANGE:` footer

### 3. Draft the message

Produce a candidate commit message in the correct format. Then:

- Check the subject line is ≤ 72 characters
- Check imperative mood
- Add a body if the *why* is non-obvious from the diff
- **Always include `#<id>` as the first footer line** — use the number extracted in step 1
- Add `BREAKING CHANGE:` footer if applicable

### 4. Present for review

Show the proposed message clearly (in a code block) and briefly explain the scope/type choice. Ask the user to confirm or request changes before committing.

### 5. Commit (only on confirmation)

Once confirmed:
```bash
git commit -m "$(cat <<'EOF'
[Scope] type: short description

Optional body here.

Optional footers.
EOF
)"
```

Never commit without explicit user approval.

---

## Examples

```
[Console] feat: add trailer management screen

Implements basic trailer entry and listing for ops console
with run selection integration.

#55448
```

```
[InTruck] fix: correct event handler detachment in else block

OnAccessCompleted was using += instead of -= in the else
branch, causing duplicate event handling and memory leaks.

#57122
```

```
[ServiceBus] feat: send GPS updates to RabbitMQ

#56257
```

```
[RFID] feat: implement TID-based tag identification

Adds TID reading support with feature flag for gradual rollout.
Only reads TID identifier, ignoring other tag memory banks.

#56257
```

```
[Shared] fix: UTC-normalize all datetime conversions

DateTime.Now calls replaced with DateTime.UtcNow across
shared utility methods to prevent timezone-related bugs.

#12345
```

```
[Contracts] feat!: rename TruckEvents to VehicleEvents

TruckEvents contract and all related types renamed to
VehicleEvents to align with updated domain terminology.

#9900
BREAKING CHANGE: TruckEvents removed. Use VehicleEvents.
All consumers must update their subscriptions.
```

```
[Multi] chore: update NuGet packages to latest patch versions

#12345
```
