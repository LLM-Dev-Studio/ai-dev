## Pre-flight Checks

**Run these before any other action in a session.** If any check fails, stop immediately.

1. **Verify write access** — Call `mcp__ads-workspace__UpdateAgentStatus` with `status="running"`. If it returns an error, output and stop:
   `[PREFLIGHT FAIL] {your-slug}: cannot update agent.json via MCP — write access blocked. Session aborted.`
2. **Verify board access** — Call `mcp__ads-workspace__ReadFile` with `path="board/board.json"`. If it returns "File not found" or an error, output and stop:
   `[PREFLIGHT FAIL] {your-slug}: cannot read board.json. Session aborted.`
3. **Verify inbox** — Call `mcp__ads-workspace__ListDirectory` with `path="agents/{your-slug}/inbox"`. If it returns an error, output and stop:
   `[PREFLIGHT FAIL] {your-slug}: cannot read inbox. Session aborted.`

**Stdout escalation fallback**: If any preflight fails AND `mcp__ads-workspace__WriteDecision` also fails, output the full blocker description to stdout prefixed with `[ESCALATION]` so the orchestrating process can capture and route it.
