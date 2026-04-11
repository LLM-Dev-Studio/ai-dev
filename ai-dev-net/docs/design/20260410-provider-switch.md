# Design Spec — Provider Switch

**Feature:** Provider Switch — executor visibility and control  
**Date:** 2026-04-10  
**Author:** designer-standard  
**Status:** Final  
**Decision applied:** Offline executor selection → Option A (soft-warn), confirmed by human 2026-04-11

---

## 1. Overview

This spec covers four UI areas that together give users visibility over executor health and control over which executor each agent uses:

1. **ExecutorStatusPanel** — health overview of all registered executors (already partially implemented via `LocalAiStatus.razor`; this spec refines its placement and adds a settings page section).
2. **BulkProviderSwitchModal** — project-scoped modal to reassign all agents from one executor to another.
3. **AgentDetailPage executor selector** — offline-executor soft-warn enhancement.
4. **FailoverNotificationBanner** — inline banner in TranscriptPage when an agent has been auto-switched to a fallback executor.

---

## 2. Existing Code Reference

- `LocalAiStatus.razor` — renders a health row per executor (emerald dot = online, red dot = offline) with tooltip showing health message + last-checked time. Currently lives in the layout sidebar.
- `AgentDetailPage.razor` — executor `<select>` with `executorOptions` list; health message already shown below dropdown in amber when unhealthy. Executor options are not currently marked as offline within the dropdown itself.
- `StatusBadge.razor` — reusable dot+label badge pattern (border, bg-secondary).
- `ReconnectModal.razor` — `<dialog>` modal pattern used for reconnection UI; same pattern to use for BulkProviderSwitchModal.

---

## 3. Area 1: Executor Status Panel

### 3.1 Current state
`LocalAiStatus.razor` already satisfies the sidebar health indicator requirement. No new component is needed.

### 3.2 Settings page section (new)
Add an **Executors** section to the project Settings page. This gives users a dedicated view of executor health independent of the sidebar.

**Layout:**
```
[ Settings page ]
  Section: "Executors"
  ┌─────────────────────────────────────────────────┐
  │  ● Claude CLI          Online                   │
  │  ● Anthropic API       Online                   │
  │  ● Ollama              Offline  "model not found..."  │
  │  ● GitHub Models       Online                   │
  └─────────────────────────────────────────────────┘
  Last checked: 09:12:34
```

Each row:
- Emerald dot (online) or red dot (offline) — 6×6 px rounded-full, matching `LocalAiStatus.razor`
- Executor display name — `text-sm text-foreground`
- Status label — `text-xs` emerald-400 (online) or red-400 (offline), right-aligned
- If offline: health message shown in `text-xs text-muted-foreground` on a second line, truncated at 80 chars with full text in `title` tooltip
- "Last checked: HH:mm:ss" below the list — `text-xs text-muted-foreground`

**States:**
- All online: rows show emerald dots
- Some offline: offline rows show red dot + message
- No executors registered: "No executors registered." in `text-sm text-muted-foreground`
- Loading (initial): skeleton rows (3×), same height as real rows

**Accessibility:**
- Each row: `role="status"` on the dot span; `aria-label="[ExecutorName]: online"` or `aria-label="[ExecutorName]: offline — [message]"`
- Last checked timestamp: `aria-live="polite"` so screen readers announce updates

---

## 4. Area 2: Bulk Provider Switch Modal

### 4.1 Trigger
A **"Switch executor…"** button in the Project Settings page, placed in the Executors section header (right side, secondary style).

**Button copy:** "Switch executor…"  
**Button style:** `text-xs px-3 py-1.5 rounded-md border border-border text-muted-foreground hover:text-foreground hover:bg-accent/50 transition-colors`

### 4.2 Modal layout

```
┌──────────────────────────────────────────────────────────┐
│  Switch all agents to a new executor                     │
│  ───────────────────────────────────────────       │
│  Current distribution:                                   │
│    Claude CLI      ████████ 8 agents                    │
│    Ollama          ██ 2 agents                          │
│    Anthropic API   — 0 agents                           │
│    GitHub Models   — 0 agents                           │
│                                                          │
│  Switch all agents to:  [ Anthropic API (online) ▼ ]   │
│                                                          │
│  ⚠ This will update 10 agents.                         │
│                                                          │
│  [ Cancel ]                    [ Switch 10 agents ]     │
└──────────────────────────────────────────────────────────┘
```

**Component:** `BulkProviderSwitchModal` — Blazor `<dialog>` element, same pattern as `ReconnectModal.razor`.

**Distribution list:**
- One row per registered executor
- Bar: `bg-primary/20 rounded-full h-1.5` proportional width (min visible width: 4px for non-zero counts)
- Count label: `text-xs text-muted-foreground` — "N agents"
- Zero-agent executors: bar omitted, count shows "— 0 agents" in muted colour

**Target executor dropdown:**
- Lists only **online** executors (offline executors excluded from this dropdown — hard exclusion is correct here since bulk-switching to an offline executor would strand all agents)
- Each option: `{DisplayName} (online)`
- If an executor goes offline while modal is open: remove from dropdown; if it was selected, reset selection to first available online executor and show: "The selected executor went offline. Please choose another."

**Confirmation area:**
- Amber `⚠` info line: "This will update N agents." — shown when a target is selected; N = total agent count
- Confirm button: `bg-primary text-primary-foreground` — "Switch N agents" (disabled until a target executor is selected)
- Cancel button: secondary style

**States:**
- Default open: distribution shown, no target selected, confirm button disabled
- Target selected: confirm button enabled, warning count shown
- No online executors: dropdown shows "No online executors available" disabled option; confirm button disabled; add note: "All executors are currently offline. Bring at least one online before switching."
- Switching (in-progress): confirm button shows "Switching…" with spinner; cancel button disabled
- Success: modal closes; project page shows a toast: "All agents switched to {ExecutorName}."
- Error: inline error below confirm button: "Something went wrong. {error message}. No agents were changed."

**Copy:**
- Modal title: "Switch all agents to a new executor"
- Distribution header: "Current distribution:"
- Warning: "This will update N agents."
- Confirm button: "Switch N agents"
- Cancel: "Cancel"
- Toast (success): "All agents switched to {ExecutorName}."
- Error: "Something went wrong. [message]. No agents were changed."
- No online executors notice: "All executors are currently offline. Bring at least one online before switching."

**Keyboard:**
- `Escape` closes modal (cancel)
- `Tab` cycles: target dropdown → Cancel → Switch button
- Focus on open: first focusable element (target dropdown)
- Focus on close: returns to "Switch executor…" trigger button

**Accessibility:**
- `<dialog>` element; `aria-modal="true"`; `aria-labelledby` pointing to title heading
- Distribution rows: `role="list"` / `role="listitem"`
- Confirm button: `aria-disabled="true"` when no target selected
- Spinner: `aria-label="Switching…"`, `aria-busy="true"` on button

---

## 5. Area 3: Agent Detail Page — Executor Selector Enhancement

### 5.1 Current behaviour (as-built)
- Executor `<select>` lists all executors without marking offline ones.
- After selection, a health message is shown below the dropdown in amber when the chosen executor is unhealthy (`text-amber-300`).

### 5.2 Required change (Option A — soft-warn)

**Decision:** Option A confirmed. Offline executors remain selectable. Visual indicators guide the user.

**Dropdown option label format:**
- Online: `{DisplayName}` — e.g. "Ollama"
- Offline: `{DisplayName} (offline)` — e.g. "Ollama (offline)"

**Inline warning (below dropdown):**
- When a **healthy** executor is selected: show `{DisplayName}` in `text-xs text-muted-foreground` (already implemented)
- When an **unhealthy** executor is selected: existing amber message from `health.Message` is correct; no change needed — the current code already handles this

**Specific change to the Razor template:**  
In the executor `<select>` loop, change the `<option>` text to append ` (offline)` when the executor is unhealthy:

```
@option.DisplayName@(option.Health.IsHealthy ? "" : " (offline)")
```

That is the only template change required for this area. The downstream inline message is already handled.

**States:**
- Online executor selected: label = DisplayName, no warning below
- Offline executor selected: label = "DisplayName (offline)" in dropdown; amber message below = `health.Message`
- Executor transitions online→offline while page is open: dropdown option label updates reactively (already handled by `OnHealthChanged` event subscription); inline message updates automatically

**Copy:**
- Offline suffix in dropdown: " (offline)"
- No other copy changes needed

---

## 6. Area 4: Failover Notification Banner

### 6.1 Trigger
When `AgentRunnerService` auto-switches an agent to a fallback executor (because its configured executor went offline mid-run or at launch time), the agent’s state is updated. The TranscriptPage should display a persistent banner until dismissed.

### 6.2 Placement
Amber banner at the top of `TranscriptPage`, below the page header, above the transcript messages. A single banner per failover event (not stacked if multiple failovers occurred; show the most recent).

### 6.3 Layout

```
┌──────────────────────────────────────────────────────────────┐
│  ⚠  Auto-switched executor                           [×]    │
│     {OriginalExecutor} was offline at launch. This agent    │
│     ran using {FallbackExecutor} instead.                   │
└──────────────────────────────────────────────────────────────┘
```

**Component:** `FailoverNotificationBanner` — inline Blazor component, not a modal.

**Styling:**
- Container: `rounded-lg border border-amber-500/30 bg-amber-500/10 p-4 mb-4`
- Icon: `SvgIcon Name="shield-alert"` — `w-4 h-4 text-amber-400 mt-0.5 flex-shrink-0`
- Title: `text-sm font-medium text-amber-200` — "Auto-switched executor"
- Body: `text-sm text-amber-100/90` — "{OriginalExecutor} was offline at launch. This agent ran using {FallbackExecutor} instead."
- Dismiss button (×): `text-xs text-amber-300 hover:text-amber-100 ml-auto` — icon `SvgIcon Name="x"` or plain ×
- Same visual pattern as the existing last-error amber alert in `AgentDetailPage.razor`

**States:**
- Banner visible: failover occurred and not yet dismissed
- Banner dismissed: banner hidden for this session (no persistence across page reloads needed unless otherwise specified — see §8.3)
- No failover: banner not rendered

**Parameters (component inputs):**
- `OriginalExecutorName` (string) — display name of the executor that was offline
- `FallbackExecutorName` (string) — display name of the executor that was actually used
- `OnDismiss` (EventCallback) — called when user clicks ×

**Copy:**
- Banner title: "Auto-switched executor"
- Banner body: "{OriginalExecutor} was offline at launch. This agent ran using {FallbackExecutor} instead."
- Dismiss button: aria-label="Dismiss failover notification"

### 6.4 ProjectEventLog entry (secondary notification path)

When a failover occurs, also add an event to the ProjectEventLog (if one exists):
- Icon: `shield-alert` in `text-amber-400`
- Text: "Agent {AgentName} auto-switched from {OriginalExecutor} to {FallbackExecutor}"
- Timestamp: UTC ISO 8601

**Keyboard:**
- Dismiss button is focusable; `Enter`/`Space` triggers dismiss
- Banner has `role="alert"` so screen readers announce it on appearance

**Accessibility:**
- `role="alert"` on the banner container
- Dismiss button: `aria-label="Dismiss failover notification"`

---

## 7. Responsive Behaviour

All components use TailwindCSS and are responsive by default.

- **ExecutorStatusPanel (Settings):** Single-column list; no breakpoint changes needed.
- **BulkProviderSwitchModal:** `max-w-md` on mobile; `max-w-lg` on desktop. Distribution bar widths scale proportionally.
- **AgentDetailPage executor select:** Already in a `grid-cols-2` card; no changes needed.
- **FailoverNotificationBanner:** Full-width block; text wraps naturally.

---

## 8. Open Questions

| # | Question | Default | Status |
|---|----------|---------|--------|
| 8.1 | Hard-block vs soft-warn for offline executor selection on AgentDetailPage | Option A (soft-warn) | **Resolved — Option A confirmed by human 2026-04-11** |
| 8.2 | ExecutorStatusPanel in sidebar vs Settings page | Settings > Executors section (sidebar `LocalAiStatus.razor` already covers sidebar) | Open |
| 8.3 | FailoverNotificationBanner persistence — dismiss survives page reload? | No — dismissed on reload | Open |

---

## 9. Component Summary

| Component | Type | File location (suggested) | Depends on |
|-----------|------|--------------------------|------------|
| ExecutorStatusPanel (settings section) | New Blazor component | `Components/Pages/ProjectPages/ProjectSettingsPage.razor` (inline) | `ExecutorHealthMonitor` |
| BulkProviderSwitchModal | New Blazor component | `Components/Shared/BulkProviderSwitchModal.razor` | `ExecutorHealthMonitor`, `AgentService` |
| AgentDetailPage executor dropdown | Modify existing | `Components/Pages/ProjectPages/AgentDetailPage.razor` | `ExecutorHealthMonitor` (already injected) |
| FailoverNotificationBanner | New Blazor component | `Components/Shared/FailoverNotificationBanner.razor` | None (parameters only) |
| ProjectEventLog failover entry | Modify existing | TBD (depends on existing event log implementation) | `shield-alert` icon (already in registry) |
