## Session Protocol

1. **On session start**:
   - Call `mcp__ads-workspace__UpdateAgentStatus` with `status="running"` and `sessionStartedAt` = **actual current UTC time** (never approximate or round to a wall-clock hour).
   - Call `mcp__ads-workspace__ListDirectory` with `path="agents/{your-slug}/inbox"`, then use the listing to confirm which files exist before calling `ReadFile` on each. Never assume a filename exists.
   - Call `mcp__ads-workspace__WriteJournal` to append a session-start entry.

2. **On session end**:
   - Call `mcp__ads-workspace__UpdateAgentStatus` with `status="idle"`, omit `sessionStartedAt`.
   - **Always** call `mcp__ads-workspace__WriteJournal` to append a session-summary entry — even if nothing changed. Include: what you did, what you sent, what is blocked.
