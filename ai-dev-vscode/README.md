# AI Dev Studio

AI Dev Studio is a VS Code extension that provides a live sidebar for monitoring and controlling AI agent workflows running on your local machine. It automatically detects AI Dev Studio projects in your workspace, starts the backend API, and keeps three panels in sync via real-time SignalR updates.

---

## Features

### Agents panel

Lists every agent configured for the current project, showing whether each one is running or rate-limited. You can start or stop any agent directly from the panel without leaving your editor.

### Messages panel

Shows unprocessed inbox messages queued for each agent. Badge counts on the panel tab give you an at-a-glance count. You can trigger processing of a message from the panel.

### Decisions panel

Lists pending decisions that are blocking agent progress. Badge counts highlight how many decisions are waiting. You can submit a resolution inline, unblocking the relevant agent immediately.

### Status bar

A status bar item in the bottom-left corner shows the current backend state — **Starting**, **Running**, or **Disconnected**. Clicking it triggers a backend restart.

---

## Requirements

- VS Code 1.90 or later
- An AI Dev Studio project with a `.ai-dev/project.json` file in the workspace root

The extension bundles the backend API binary. No separate installation is required.

---

## Getting started

1. Open a folder (or workspace) that contains a `.ai-dev/project.json` file.
2. The extension detects the project automatically and starts the backend on the configured port.
3. The **AI Dev Studio** activity bar icon appears. Click it to open the Agents, Messages, and Decisions panels.

### project.json format

The extension reads `.ai-dev/project.json` from your workspace root:

```json
{
  "projectSlug": "my-project",
  "apiPort": 5100
}
```

| Field | Description |
|---|---|
| `projectSlug` | Unique identifier for the project. Used to scope all API and SignalR calls. |
| `apiPort` | Port the backend API will listen on. |

---

## Commands

| Command | Description |
|---|---|
| `AI Dev: Restart Backend` | Tears down the current backend process and SignalR connection, then rescans the workspace and reconnects. |

---

## How it works

1. On activation, `WorkspaceDetector` watches for `.ai-dev/project.json` in each open workspace folder.
2. When a config file is found, `BackendProcessManager` spawns the bundled platform binary (`bin/ai-dev-api.exe` on Windows, `bin/ai-dev-api` on macOS/Linux) and polls `/api/health` until the backend is ready (up to 30 attempts, 1 s apart).
3. If the binary is absent (development mode), the extension skips spawning and connects to a backend that is already running on the configured port.
4. `StudioSignalRClient` connects to `/hubs/project` and joins the project room. `StateChanged` events from the hub drive incremental refreshes of each panel — only the affected panel fetches new data.
5. REST calls through `StudioApiClient` handle all reads and writes (list agents, run/stop, list messages, process, list decisions, resolve).

---

## Development

```bash
# Install dependencies
cd ai-dev-vscode
npm install

# Build once
npm run build

# Watch mode
npm run watch

# Run tests
npm test

# Package VSIX
npm run package
```

To test against a live backend, run the `ai-dev.api` project externally on the port matching your `.ai-dev/project.json`, then press **F5** in VS Code to launch the extension in the Extension Development Host.

---

## License

MIT — see [LICENSE](LICENSE).
