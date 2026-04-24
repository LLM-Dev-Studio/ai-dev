# Design Spec: Agent Description — Expandable and Resizable Fields

**Date:** 2026-04-20
**Author:** designer-standard
**Task:** task-1776144486438-88735
**Status:** Draft

---

## 1. Background

Agent description fields are currently rendered as single-line `TextBox` controls. Long descriptions overflow horizontally and cannot be read or edited comfortably. This spec covers every location where an agent description is displayed or edited and defines the expand/resize behaviour for each.

---

## 2. Affected Locations

| # | Location | File | Current Control | Role |
|---|---|---|---|---|
| 1 | Agent Detail Page — edit form | `ai-dev.ui.winui/Views/Pages/AgentDetailPage.xaml` L183–186 | `TextBox` (single-line) | Edit |
| 2 | Templates Page — edit form | `ai-dev.ui.winui/Views/Pages/TemplatesPage.xaml` L136–139 | `TextBox` (single-line) | Edit |
| 3 | Agent Dashboard — agent cards | `ai-dev.ui.winui/Views/Pages/AgentDashboardPage.xaml` | None (description not shown) | Read-only display (new) |

> **Out of scope:** Project description (`ProjectSettingsPage.xaml`) — not an agent description. Skill descriptions (shown as tooltips on `AgentDetailPage`) — tooltip pattern is already appropriate for short text.

---

## 3. Design Decisions

### 3.1 Edit Fields (Locations 1 & 2)

**Chosen pattern: Auto-grow multi-line TextBox**

Convert the single-line `TextBox` to a multi-line, auto-growing `TextBox` — matching the existing task description pattern in `BoardPage.xaml` (L191–195). This is the lowest-friction change: no new controls, no modal, consistent with established in-app precedent.

Do **not** add a separate "Expand" modal. The auto-grow pattern keeps focus in-context and avoids mode switching.

### 3.2 Dashboard Cards (Location 3)

**Chosen pattern: Truncated read-only TextBlock with tooltip**

Add a read-only description line below the existing role/model/executor badges. Truncate at two lines with an ellipsis. Show the full description in a tooltip on hover/focus. Do **not** add an interactive expand here — the dashboard is a navigation surface, not an editor.

---

## 4. Detailed Specifications

### 4.1 Agent Detail Page — Description TextBox

**Current XAML (simplified):**
```xaml
<TextBox Text="{Binding EditDescription, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
```

**Replacement:**
```xaml
<TextBox Text="{Binding EditDescription, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
         AcceptsReturn="True"
         TextWrapping="Wrap"
         MinHeight="80"
         MaxHeight="300"
         ScrollViewer.VerticalScrollBarVisibility="Auto" />
```

**Layout context:** The description field sits in a 2-column `Grid` (Name | Description). The `Grid` uses `ColumnSpacing="12"`. The description column should use `Width="*"` (already the case) so the TextBox fills available width.

#### States

| State | Appearance |
|---|---|
| **Default (empty)** | 80 px tall, no placeholder text (none currently; add `PlaceholderText="Describe this agent's purpose"`) |
| **Default (content ≤ 2 lines)** | Matches content height; capped at 80 px minimum |
| **Expanding (content grows)** | TextBox grows vertically as user types; no animation needed — WinUI TextBox auto-grow is immediate |
| **At max height (300 px)** | Vertical scrollbar appears; content scrolls within the box |
| **Focused** | Standard WinUI focus ring — no custom treatment needed |
| **Disabled** | Standard WinUI disabled style |

#### Dimensions

| Property | Value | Rationale |
|---|---|---|
| `MinHeight` | `80` | Signals that multi-line input is expected; matches task description |
| `MaxHeight` | `300` | Prevents the field from pushing other config fields (Model, Executor) off screen |
| `TextWrapping` | `Wrap` | Required for multi-line display |
| `AcceptsReturn` | `True` | Allows Shift+Enter line breaks (Enter remains the default submit key for any parent form) |
| Width | inherits column `*` | No change needed |

#### Keyboard behaviour

- **Tab** moves focus to the next field (standard).
- **Enter** inside the TextBox inserts a line break (because `AcceptsReturn="True"`). This does **not** trigger the Save button — Save remains explicitly button-activated.
- **Shift+Tab** moves focus back.
- **Escape** reverts to last saved value (existing ViewModel behaviour; no change needed).

#### Accessibility

- Label (`TextBlock` with `CaptionTextBlockStyle` above the control) already exists — no change.
- `AutomationProperties.Name` not explicitly set; the label is a sibling, not a labelled-by reference. Add `AutomationProperties.LabeledBy` binding or `AutomationProperties.Name="Description"` to the TextBox for screen reader announcement.
- Colour contrast: default WinUI TextBox meets WCAG AA. No change.
- Scroll within the field is accessible via keyboard once the field has focus.

---

### 4.2 Templates Page — Description TextBox

Identical spec to §4.1. Apply the same `AcceptsReturn`, `TextWrapping`, `MinHeight`, `MaxHeight`, and placeholder changes.

**Placeholder text:** `"Describe what this template is for"`

All other keyboard, accessibility, and dimension rules from §4.1 apply without modification.

---

### 4.3 Agent Dashboard — Description on Agent Cards

#### Current card structure

Each agent card is a 300 px wide `Border` containing:
1. Agent name (bold)
2. Status badge row (Role · Model · Executor)
3. Action area (buttons)

#### Proposed addition

Insert a read-only description `TextBlock` between the status badges (item 2) and the action area (item 3).

**XAML:**
```xaml
<TextBlock Text="{Binding Description}"
           Style="{StaticResource CaptionTextBlockStyle}"
           Foreground="{ThemeResource TextFillColorSecondaryBrush}"
           TextWrapping="Wrap"
           MaxLines="2"
           TextTrimming="CharacterEllipsis"
           ToolTipService.ToolTip="{Binding Description}"
           Visibility="{Binding Description, Converter={StaticResource StringToVisibilityConverter}}" />
```

#### States

| State | Appearance |
|---|---|
| **Empty description** | TextBlock hidden (Visibility=Collapsed via StringToVisibilityConverter) |
| **Short description (≤ 2 lines at 300 px)** | Full text shown; no ellipsis; no tooltip needed but tooltip still present |
| **Long description (> 2 lines)** | Truncated at 2 lines with `…` ellipsis; full text in tooltip |

#### Tooltip behaviour

- **Mouse:** tooltip appears on hover after standard WinUI delay (~500 ms).
- **Keyboard:** tooltip appears when the TextBlock receives focus (set `IsTabStop="True"` to enable focus).
- **Screen reader:** the tooltip content is read via `ToolTipService.ToolTip` — this is accessible in WinUI 3 via `AutomationProperties.HelpText` if tooltip alone is insufficient. Prefer setting `AutomationProperties.HelpText="{Binding Description}"` as well.

#### Dimensions

| Property | Value |
|---|---|
| Card width | 300 px (unchanged) |
| `MaxLines` | 2 |
| `TextTrimming` | `CharacterEllipsis` |
| Bottom margin from description to action area | 8 px (`Margin="0,4,0,0"`) |

#### Copy

No new interactive labels are introduced. The description appears inline — no "Expand" button. If in future an expand action is added to cards, the label should read **"Show full description"** (tooltip: "Show full description").

---

## 5. Edge Cases

| Case | Handling |
|---|---|
| **Empty description** | Dashboard: hide TextBlock. Edit forms: show placeholder text. |
| **Very long description (>1000 chars)** | Edit form grows to `MaxHeight` (300 px) and scrolls. Dashboard truncates to 2 lines. No character limit enforced at UI layer (model already accepts any string). |
| **Single very long word / URL** | `TextWrapping="Wrap"` breaks at character boundary in WinUI when no space is available — acceptable. |
| **Description with line breaks** | Edit form renders them as entered (AcceptsReturn=True). Dashboard collapses line breaks into spaces (WinUI TextBlock default) — acceptable since we show the full text in tooltip. |
| **RTL languages** | WinUI TextBox/TextBlock handle RTL natively via `FlowDirection`. No special handling needed. |

---

## 6. Responsive Behaviour

This is a WinUI 3 desktop application. The main window can be resized by the user.

- **Agent Detail Page:** The 2-column grid in the configuration card is fluid. As the window narrows, both columns shrink proportionally. The description TextBox fills the right column. At very narrow widths (< ~600 px logical), the existing grid may already wrap or clip — this is not introduced by this change and is out of scope.
- **Templates Page:** Same as Agent Detail Page — no additional behaviour needed.
- **Dashboard cards:** Fixed 300 px card width wraps in the `WrapGrid` / `ItemsWrapGrid` container. No change to card width. The description TextBlock inside the card reflows within the 300 px constraint.

---

## 7. Animation / Transition

- **Edit TextBox growth:** No animation. WinUI TextBox grows immediately as content changes. An animated height transition would require custom attached behaviours — disproportionate effort for the benefit.
- **Tooltip reveal:** Standard WinUI tooltip fade-in. No custom animation.

---

## 8. Accessibility Summary

| Location | Requirement |
|---|---|
| Agent Detail — TextBox | Add `AutomationProperties.Name="Description"` to TextBox |
| Templates — TextBox | Add `AutomationProperties.Name="Description"` to TextBox |
| Dashboard — TextBlock | Set `IsTabStop="True"` and `AutomationProperties.HelpText="{Binding Description}"` |

Colour contrast, focus rings, and keyboard navigation all use WinUI 3 defaults, which meet WCAG AA.

---

## 9. Copy Reference

| Control | Copy |
|---|---|
| Agent Detail TextBox placeholder | `"Describe this agent's purpose"` |
| Templates TextBox placeholder | `"Describe what this template is for"` |
| Dashboard tooltip (full text) | _(dynamic — bound to Description)_ |

No other new labels, buttons, or error messages are introduced.

---

## 10. Implementation Notes for Developer

1. **Converter:** `StringToVisibilityConverter` is already referenced elsewhere in the codebase — confirm it returns `Collapsed` for null/empty and `Visible` otherwise. If not present, create it.
2. **ViewModel:** No ViewModel changes needed for the edit fields. For the dashboard, `AgentInfo.Description` is already a public property — the card `DataContext` just needs to bind to it.
3. **No data model changes** — `AgentInfo.Description` already accepts any string.
4. **Reference implementation:** `BoardPage.xaml` L191–195 shows the multi-line TextBox pattern already in use.
