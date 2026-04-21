# Design Spec: Agent Descriptions — Expandable and Resizable Fields

**Date**: 2026-04-20
**Task**: task-1776144486438-88735
**Author**: designer-standard

---

## 1. Scope — Where Agent Descriptions Appear

After reviewing the WinUI codebase (`ai-dev.ui.winui`), descriptions appear in **two locations** and are absent from a third where they arguably should appear:

| Location | File | Current State |
|---|---|---|
| **A. Agent Detail Page — Configuration form** | `Views/Pages/AgentDetailPage.xaml` line 185 | Single-line `TextBox`, no `MinHeight`, no `AcceptsReturn` — too small for multi-sentence descriptions |
| **B. Agent Dashboard — Cards** | `Views/Pages/AgentDashboardPage.xaml` | Description is **not displayed at all** on cards |
| **C. New Agent Dialog** | `Views/Dialogs/NewAgentDialog.xaml.cs` | Description field **does not exist** in the creation form |

Scope of this spec: **Location A** (fix the edit TextBox) and **Location B** (add read-only description to cards). Location C (creation dialog) is noted as a gap but is out of scope for this ticket unless the PM confirms otherwise.

---

## 2. Location A — Agent Detail Page: Description Edit Field

### 2.1 Current State

```
Row 0, Col 1 of the configuration grid:
  Label: "Description"  (CaptionTextBlockStyle, secondary colour)
  TextBox: single-line, no height constraint, no wrapping
```

The TextBox renders at approximately 28–32 px tall (one line), making it impractical for descriptions longer than ~60 characters.

### 2.2 Interaction Pattern: Auto-grow textarea with explicit resize handle

Replace the single-line `TextBox` with a multi-line `TextBox` that:
- Auto-grows vertically as the user types (up to `MaxHeight`)
- Shows a vertical scrollbar when content exceeds `MaxHeight`
- Occupies the full column width (no fixed width)

> **Rationale**: WinUI's `TextBox` supports `AcceptsReturn="True"` and `MinHeight`, which gives a native auto-grow experience consistent with the existing `ClaudeEditor` textarea on the same page (lines 282–289). A resize handle is not natively supported in WinUI `TextBox`; auto-grow is the idiomatic substitute.

### 2.3 Dimensions

| Property | Value |
|---|---|
| `MinHeight` | `80px` (≈ 3 lines at default font size) |
| Default height | Grows with content |
| `MaxHeight` | `240px` (≈ 9 lines) — scrollbar appears above this |
| Width | Fills column (`HorizontalAlignment="Stretch"`) |

### 2.4 States

| State | Appearance |
|---|---|
| **Default / unfocused** | 3-line tall box, no scrollbar visible, placeholder text if empty |
| **Focused** | WinUI focus ring, scrollbar visible on hover/focus if content overflows |
| **Typing** | Box grows line-by-line up to `MaxHeight`, then scrolls |
| **Full (at MaxHeight)** | Fixed height, vertical scrollbar always visible |
| **Disabled** | Not applicable — field is always editable on this page |
| **Error** | Handled by the existing `SaveError` TextBlock below the form — no per-field decoration needed |
| **Saved** | Existing "Saved" confirmation TextBlock applies to the whole form |

### 2.5 Empty State

When `EditDescription` is an empty string, display placeholder text:

> **Placeholder**: `"Describe what this agent does…"`

### 2.6 Truncation / Long Description

- No truncation in edit mode — the user sees full content with scroll.
- Descriptions have no enforced character limit in the data model; the UI should not impose one. (The `agent.json` `description` field is a free-form string.)

### 2.7 Animation / Transition

None. WinUI `TextBox` height changes are instantaneous; a smooth resize animation is not standard for WinUI controls and would feel out of place.

### 2.8 Accessibility

- **Keyboard**: Tab into the field moves focus; Enter inserts a newline (standard textarea behaviour with `AcceptsReturn="True"`). Ctrl+A selects all. Scrolling with keyboard arrow keys works natively.
- **ARIA / AutomationProperties**: Set `AutomationProperties.Name="Description"` on the `TextBox` so screen readers announce the field label.
- **Focus indicator**: WinUI default focus ring is sufficient; no additional styling required.
- **Colour contrast**: Inherits theme colours (`TextFillColorPrimaryBrush` on `ControlFillColorDefaultBrush`), which meet WCAG AA in both light and dark themes.

### 2.9 Layout Impact

The description `TextBox` sits in `Grid.Row="0" Grid.Column="1"` alongside the Name `TextBox` in `Grid.Column="0"`. Both are in an `Auto`-height row. Growing the description box will expand that row and push subsequent rows (Model, Executor, Thinking level, Skills) downward — this is the desired behaviour since the form is already inside a `ScrollViewer`.

### 2.10 XAML Change Summary (for developer reference)

**Before** (line 185):
```xml
<TextBox Text="{Binding EditDescription, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
```

**After**:
```xml
<TextBox Text="{Binding EditDescription, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
         PlaceholderText="Describe what this agent does…"
         AcceptsReturn="True"
         TextWrapping="Wrap"
         MinHeight="80"
         MaxHeight="240"
         ScrollViewer.VerticalScrollBarVisibility="Auto"
         AutomationProperties.Name="Description" />
```

---

## 3. Location B — Agent Dashboard Cards: Read-Only Description

### 3.1 Current State

Cards are 300 px wide with 4 rows: name+status / role+model / inbox+last-run / action buttons. No description is shown.

### 3.2 Interaction Pattern: Truncated read-only text with tooltip for full content

Add a read-only description line between the role+model row (row 1) and the inbox+last-run row (row 2). The description is displayed truncated to 2 lines with a tooltip showing the full text on hover.

> **Rationale**: Cards are compact browse surfaces. Showing 2 lines gives enough context to identify the agent's purpose without disrupting the fixed-width card layout. The tooltip provides the full description without needing a separate expand control.

### 3.3 Dimensions

| Property | Value |
|---|---|
| Card width | 300 px (unchanged) |
| Description area max lines | 2 |
| Description area min height | ~32 px (2 lines at caption size) |

### 3.4 States

| State | Appearance |
|---|---|
| **Default** | Up to 2 lines of caption-size text in secondary colour, ellipsis if truncated |
| **Hover (truncated)** | System tooltip showing full description text |
| **Hover (not truncated)** | No tooltip (tooltip set to same text; WinUI suppresses tooltip if it matches display) |
| **Empty description** | Row hidden (`Visibility="Collapsed"`) — no blank space |

### 3.5 Empty State

If `Agent.Description` is null or empty string, the entire description row should be collapsed (`Visibility` bound to a null/empty converter). This prevents a blank gap in cards for agents that have no description yet.

### 3.6 Truncation Rules

- `MaxLines="2"` with `TextTrimming="CharacterEllipsis"`.
- `TextWrapping="Wrap"` so text uses both lines before truncating.
- Full description available via `ToolTipService.ToolTip="{Binding Agent.Description}"`.

### 3.7 Copy

No new interactive controls — no labels or buttons needed. The tooltip shows the raw description text with no preamble.

### 3.8 Accessibility

- **Screen readers**: The `TextBlock` description text is read inline as part of the card. Tooltip content is also exposed to accessibility APIs by WinUI automatically.
- **Keyboard tooltip**: WinUI tooltips are shown on keyboard focus by default — no extra work required.
- **Colour contrast**: Use `TextFillColorSecondaryBrush` (same as the role text above it) — meets WCAG AA.

### 3.9 Responsive Behaviour

Cards are fixed-width (300 px) in a `GridView` that wraps at the container boundary. No breakpoint-specific behaviour is needed.

### 3.10 XAML Change Summary (for developer reference)

Insert a new row between `Grid.Row="1"` (role+model) and `Grid.Row="2"` (inbox+last-run). Shift the existing rows 2 and 3 to rows 3 and 4. Add to `Grid.RowDefinitions`:

```xml
<RowDefinition Height="Auto" />  <!-- new description row -->
```

New description TextBlock in the new row:

```xml
<TextBlock Grid.Row="2"
           Margin="0,0,0,8"
           Text="{Binding Agent.Description}"
           Style="{StaticResource CaptionTextBlockStyle}"
           Foreground="{ThemeResource TextFillColorSecondaryBrush}"
           TextWrapping="Wrap"
           MaxLines="2"
           TextTrimming="CharacterEllipsis"
           ToolTipService.ToolTip="{Binding Agent.Description}"
           Visibility="{Binding Agent.Description, Converter={StaticResource NullToVisible}}" />
```

> **Note**: The existing `NullToVisibilityConverter` already handles null/empty → Collapsed. No new converter is required.

---

## 4. Location C — New Agent Dialog (Out of Scope, Flagged)

The `NewAgentDialog` currently collects: template, display name, agent slug. It does not include a description field. Agents created via this dialog will have an empty description, which will then not appear on the dashboard card (collapsed state) and will show an empty textarea on the detail page.

This is acceptable for now — the description can be filled in after creation on the detail page. If the PM wants description added to the creation dialog, that is a separate ticket.

---

## 5. Edge Cases

| Case | Handling |
|---|---|
| Empty description | Dashboard: row collapsed. Detail form: placeholder text shown. |
| Very short description (< 1 line) | Dashboard: single line, no ellipsis. Detail form: box stays at MinHeight (80px). |
| Very long description (1000+ chars) | Dashboard: truncated to 2 lines + tooltip. Detail form: scrollable at MaxHeight (240px). |
| Description with newlines | Dashboard: newlines render as spaces (TextBlock collapses whitespace); full text in tooltip. Detail form: newlines preserved (AcceptsReturn). |
| Description with only whitespace | Treat as empty — `NullToVisibilityConverter` should handle empty string; verify it trims. If not, bind with a StringToVisibilityConverter that trims first. |
| RTL text | WinUI handles RTL via system locale; no additional work required. |
| Very narrow window / card wrap | Cards maintain 300px fixed width; description truncation handles narrow rendering. |

---

## 6. Summary of Changes

| Location | Change Type | Complexity |
|---|---|---|
| `AgentDetailPage.xaml` line 185 | Modify one `TextBox` element — add 5 attributes | Low |
| `AgentDashboardPage.xaml` — card DataTemplate | Add one `RowDefinition` + one `TextBlock`, shift 2 existing rows | Low |

No ViewModel changes are required. No new converters are required. No new styles are required.
