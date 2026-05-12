Yes — this app does **not appear to initialize an embedded local LLM server like Ollama/LM Studio/llama.cpp inside the repo**. Instead, it initializes and orchestrates **local coding-agent CLIs/processes** such as **OpenAI Codex**, **Claude Code**, and **Cursor Agent**, then streams their output through the app’s local server to the web UI.

Also: I couldn’t use deep research because that tool requires write access on the repo, so I traced this using code search and file reads instead. **Code search results may be incomplete** because GitHub search responses are capped. You can inspect more here:
- https://github.com/search?q=repo%3ABloopAI%2Fvibe-kanban+Ollama+OR+LMStudio+OR+llama.cpp+OR+OPENAI_API_KEY+OR+CODEX_HOME+OR+cursor-agent+OR+claude+OR+anthropic&type=code
- https://github.com/search?q=repo%3ABloopAI%2Fvibe-kanban+%22%2Fexecution%22+OR+%22thread%2Fstart%22+OR+%22turn%2Fstart%22+OR+JsonRpcPeer+OR+WebSocket&type=code

## Short version

The request-processing path looks roughly like this:

1. **App boots local deployment**
   - Loads config and cached executor profiles.
   - Chooses a recommended executor profile if onboarding isn’t complete.
2. **UI asks local backend for available executors/models**
   - Model/provider choices come from executor profile/config abstractions, not from a built-in model runtime.
3. **User starts a task / coding attempt**
   - Backend creates an `ExecutionProcess` DB record.
   - Container/execution service launches a local agent subprocess.
4. **Agent subprocess communicates with app**
   - **Codex** uses a JSON-RPC-style “app-server” protocol.
   - **Claude** uses a line-oriented control protocol over stdin/stdout.
   - **Cursor** is spawned as a CLI with streaming JSON output.
5. **Backend normalizes logs/events into patches**
   - Output is transformed into conversation/log entries and persisted.
6. **Frontend receives live updates**
   - Via local HTTP + WebSocket JSON patch streams.
7. **Optional remote/relay comms**
   - Remote auth/review services exist.
   - WebRTC relay can proxy HTTP/WebSocket traffic to the local backend.

---

## 1) Where initialization starts

The local app boot path is in `LocalDeployment::new(...)`.

```rust name=crates/local-deployment/src/lib.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/local-deployment/src/lib.rs#L93-L113
#[async_trait]
impl Deployment for LocalDeployment {
    async fn new(shutdown: CancellationToken) -> Result<Self, DeploymentError> {
        // Run one-time process logs migration from DB to filesystem
        services::services::execution_process::migrate_execution_logs_to_files()
            .await
            .map_err(|e| DeploymentError::Other(anyhow::anyhow!("Migration failed: {}", e)))?;

        let mut raw_config = load_config_from_file(&config_path()).await;

        let profiles = ExecutorConfigs::get_cached();
        if !raw_config.onboarding_acknowledged
            && let Ok(recommended_executor) = profiles.get_recommended_executor_profile().await
        {
            raw_config.executor_profile = recommended_executor;
        }
```

What matters here:
- startup performs a **logs migration**
- reads app config
- loads **executor profiles**
- may set a **recommended executor** automatically

That means “model initialization” is really **executor/profile initialization**, not GPU model bootstrapping.

---

## 2) How local models/executors are configured

The central abstraction is `ExecutorConfigs`, which loads default and user-overridden executor profiles.

```rust name=crates/executors/src/profile.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/executors/src/profile.rs#L243-L304
impl ExecutorConfigs {
    /// Get cached executor profiles
    pub fn get_cached() -> ExecutorConfigs {
        EXECUTOR_PROFILES_CACHE.read().unwrap().clone()
    }

    /// Reload executor profiles cache
    pub fn reload() {
        let mut cache = EXECUTOR_PROFILES_CACHE.write().unwrap();
        *cache = Self::load();
    }

    /// Load executor profiles from file or defaults
    pub fn load() -> Self {
        let profiles_path = workspace_utils::assets::profiles_path();

        // Load defaults first
        let mut defaults = Self::from_defaults();
        defaults.canonicalise();

        // Try to load user overrides
        let content = match fs::read_to_string(&profiles_path) {
            Ok(content) => content,
            Err(_) => {
                tracing::info!("No user profiles.json found, using defaults only");
                return defaults;
            }
        };

        // Parse user overrides
        match serde_json::from_str::<Self>(&content) {
            Ok(mut user_overrides) => {
                tracing::info!("Loaded user profile overrides from profiles.json");
                user_overrides.canonicalise();
                Self::merge_with_defaults(defaults, user_overrides)
            }
```

So model/executor setup comes from:
- built-in defaults
- optional `profiles.json` user overrides
- cached profile state at runtime

The UI-facing model schema is generic and provider-based:

```rust name=crates/executors/src/model_selector.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/executors/src/model_selector.rs#L1-L68
pub struct ModelProvider {
    pub id: String,
    pub name: String,
}

pub struct ModelInfo {
    pub id: String,
    pub name: String,
    pub provider_id: Option<String>,
    pub reasoning_options: Vec<ReasoningOption>,
}

pub struct ModelSelectorConfig {
    pub providers: Vec<ModelProvider>,
    pub models: Vec<ModelInfo>,
    pub default_model: Option<String>,
    pub agents: Vec<AgentInfo>,
    pub permissions: Vec<PermissionPolicy>,
}
```

This strongly suggests the app treats “models” as **selectable metadata exposed by executors**, rather than owning a direct inference engine.

---

## 3) What “local models” seem to mean in this repo

From the code and docs, the main active local paths are:

- **OpenAI Codex CLI**
- **Claude Code CLI**
- **Cursor Agent CLI**

### Codex
Docs show the user runs Codex separately / locally:

````mdx name=docs/agents/openai-codex.mdx url=https://github.com/BloopAI/vibe-kanban/blob/main/docs/agents/openai-codex.mdx#L1-L27
<Step title="Run OpenAI Codex">
  ```bash
  npx -y @openai/codex
  ```
</Step>
````

And the executor launches Codex’s app-server mode:

```rust name=crates/executors/src/executors/codex.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/executors/src/executors/codex.rs#L431-L444
impl Codex {
    pub fn base_command() -> &'static str {
        "npx -y @openai/codex@0.124.0"
    }

    fn build_command_builder(&self) -> Result<CommandBuilder, CommandBuildError> {
        let mut builder = CommandBuilder::new(Self::base_command());
        builder = builder.extend_params(["app-server"]);
        if self.oss.unwrap_or(false) {
            builder = builder.extend_params(["--oss"]);
        }
```

Also, it resolves Codex config from `CODEX_HOME` or `~/.codex`:

```rust name=crates/executors/src/executors/codex.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/executors/src/executors/codex.rs#L12-L24
pub fn codex_home() -> Option<PathBuf> {
    if let Ok(codex_home) = env::var("CODEX_HOME")
        && !codex_home.trim().is_empty()
    {
        return Some(PathBuf::from(codex_home));
    }
    dirs::home_dir().map(|home| home.join(".codex"))
}
```

### Claude Code
Docs show Claude Code is also an external local CLI:

````mdx name=docs/agents/claude-code.mdx url=https://github.com/BloopAI/vibe-kanban/blob/main/docs/agents/claude-code.mdx#L1-L20
<Step title="Run Claude Code">
  ```bash
  npx -y @anthropic-ai/claude-code
  ```
</Step>
````

There is Claude-specific session discovery from local filesystem:

```rust name=crates/review/src/claude_session.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/review/src/claude_session.rs#L28-L42
fn get_claude_projects_dir() -> Option<PathBuf> {
    dirs::home_dir().map(|home| home.join(".claude").join("projects"))
}
```

### Cursor
Cursor is launched via `cursor-agent`:

```rust name=crates/executors/src/executors/cursor.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/executors/src/executors/cursor.rs#L139-L167
impl CursorAgent {
    pub fn base_command() -> &'static str {
        "cursor-agent"
    }

    fn build_command_builder(&self) -> Result<CommandBuilder, CommandBuildError> {
        let mut builder =
            CommandBuilder::new(Self::base_command()).params(["-p", "--output-format=stream-json"]);

        if let Some(model) = self.resolved_model() {
            builder = builder.extend_params(["--model", model]);
        }
```

## Conclusion on local models
I **did not find evidence of built-in Ollama, LM Studio, or llama.cpp runtime initialization** in the code paths I traced. The app seems to rely on **external agent runtimes/CLIs** and model/provider selection within those runtimes.

---

## 4) How Codex is initialized and communicated with

This is the clearest “model init + request processing” path.

The Codex client binds to a JSON-RPC peer, then sends `initialize`, `thread/start`, and `turn/start`.

```rust name=crates/executors/src/executors/codex/client.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/executors/src/executors/codex/client.rs#L91-L172
pub fn connect(&self, peer: JsonRpcPeer) {
    let _ = self.rpc.set(peer);
}

pub fn set_resolved_model(&self, model: String) {
    let _ = self.resolved_model.set(model);
}

pub async fn initialize(&self) -> Result<(), ExecutorError> {
    let request = ClientRequest::Initialize {
        request_id: self.next_request_id(),
        params: InitializeParams {
            client_info: ClientInfo {
                name: "vibe-codex-executor".to_string(),
                title: None,
                version: env!("CARGO_PKG_VERSION").to_string(),
            },
            capabilities: Some(InitializeCapabilities {
                experimental_api: true,
                ..Default::default()
            }),
        },
    };

    self.send_request::<InitializeResponse>(request, "initialize")
        .await?;
    self.send_message(&ClientNotification::Initialized).await
}

pub async fn thread_start(
    &self,
    params: ThreadStartParams,
) -> Result<ThreadStartResponse, ExecutorError> {
    let request = ClientRequest::ThreadStart {
        request_id: self.next_request_id(),
        params,
    };
    self.send_request(request, "thread/start").await
}

pub async fn turn_start_with_mode(
    &self,
    thread_id: String,
    input: Vec<UserInput>,
    collaboration_mode: Option<CollaborationMode>,
) -> Result<TurnStartResponse, ExecutorError> {
    let request = ClientRequest::TurnStart {
        request_id: self.next_request_id(),
        params: TurnStartParams {
            thread_id,
            input,
            collaboration_mode,
            ..Default::default()
        },
    };
    self.send_request(request, "turn/start").await
}
```

Important points:
- the app starts a **Codex app-server subprocess**
- it attaches a **JSON-RPC peer**
- it performs an explicit **initialize handshake**
- then creates a **thread**
- then starts **turns** with user input

Model selection is folded into collaboration settings:

```rust name=crates/executors/src/executors/codex/client.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/executors/src/executors/codex/client.rs#L172-L206
fn collaboration_mode(&self, mode: ModeKind) -> Result<CollaborationMode, ExecutorError> {
    let model = self.resolved_model.get().cloned().ok_or_else(|| {
        tracing::error!("collaboration_mode called before resolved_model was set");
        ExecutorError::Io(io::Error::other(
            "resolved model not available for collaboration mode",
        ))
    })?;
    Ok(CollaborationMode {
        mode,
        settings: Settings {
            model,
            reasoning_effort: None,
            developer_instructions: None,
        },
    })
}
```

So for Codex, “local model init” =:
- resolve selected model
- connect to Codex RPC peer
- initialize session
- create/fork thread
- start turn with user inputs and mode

---

## 5) How Claude communicates

Claude is different: it uses a **control protocol over stdin/stdout** instead of the Codex JSON-RPC app-server.

The protocol peer spawns a background reader and parses line-delimited JSON messages:

```rust name=crates/executors/src/executors/claude/protocol.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/executors/src/executors/claude/protocol.rs#L20-L79
impl ProtocolPeer {
    pub fn spawn(
        stdin: ChildStdin,
        stdout: ChildStdout,
        client: Arc<ClaudeAgentClient>,
        cancel: CancellationToken,
    ) -> Self {
        let peer = Self {
            stdin: Arc::new(Mutex::new(stdin)),
        };

        let reader_peer = peer.clone();
        tokio::spawn(async move {
            if let Err(e) = reader_peer.read_loop(stdout, client, cancel).await {
                tracing::error!("Protocol reader loop error: {}", e);
            }
        });

        peer
    }
```

The read loop:
- reads stdout lines
- logs them
- parses control messages
- handles tool approval / interruption flow

Claude-specific client logic handles approval requests and responses:

```rust name=crates/executors/src/executors/claude/client.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/executors/src/executors/claude/client.rs#L30-L80
pub struct ClaudeAgentClient {
    log_writer: LogWriter,
    approvals: Option<Arc<dyn ExecutorApprovalService>>,
    auto_approve: bool,
    repo_context: RepoContext,
    commit_reminder_prompt: String,
    cancel: CancellationToken,
}
```

And normalized Claude output is converted into app conversation patches:

```rust name=crates/executors/src/executors/claude.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/executors/src/executors/claude.rs#L823-L878
let patches = processor.normalize_entries(
    &claude_json,
    &worktree_path,
    &entry_index_provider,
);
for patch in patches {
    msg_store.push_patch(patch);
}
```

So Claude path =:
- launch Claude CLI
- talk over stdio control protocol
- inspect tool-use requests
- convert line-delimited events into normalized patches
- stream those patches to UI

---

## 6) How requests enter the backend and become execution processes

The backend concept that represents a running task is `ExecutionProcess`.

```rust name=crates/db/src/models/execution_process.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/db/src/models/execution_process.rs#L32-L71
pub enum ExecutionProcessRunReason {
    SetupScript,
    CleanupScript,
    ArchiveScript,
    CodingAgent,
    DevServer,
}

pub struct ExecutionProcess {
    pub id: Uuid,
    pub session_id: Uuid,
    pub run_reason: ExecutionProcessRunReason,
    pub executor_action: sqlx::types::Json<ExecutorActionField>,
    pub status: ExecutionProcessStatus,
    pub exit_code: Option<i64>,
    pub dropped: bool,
    pub started_at: DateTime<Utc>,
    pub completed_at: Option<DateTime<Utc>>,
```

This tells you the app processes user actions by creating tracked process records in the DB.

The container service is the orchestration layer that starts executors and scripts:

```rust name=crates/services/src/services/container.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/services/src/services/container.rs#L59-L92
#[async_trait]
pub trait ContainerService {
    fn msg_stores(&self) -> &Arc<RwLock<HashMap<Uuid, Arc<MsgStore>>>>;
    fn db(&self) -> &DBService;
    fn git(&self) -> &GitService;
    fn notification_service(&self) -> &NotificationService;

    async fn touch(&self, workspace: &Workspace) -> Result<(), ContainerError>;

    fn workspace_to_current_dir(&self, workspace: &Workspace) -> PathBuf;

    async fn discover_executor_options(
        &self,
        executor_profile_id: ExecutorProfileId,
        session_id: Option<Uuid>,
        workspace_id: Option<Uuid>,
        repo_id: Option<Uuid>,
    ) -> Result<Option<BoxStream<'static, Patch>>, ContainerError> {
```

That’s the service layer that:
- finds workspace/session context
- discovers executor options
- launches subprocesses
- stores messages/log patches

---

## 7) How the frontend communicates with the local backend

The frontend uses a local API transport abstraction for both HTTP and WebSocket communication.

```typescript name=packages/web-core/src/shared/lib/localApiTransport.ts url=https://github.com/BloopAI/vibe-kanban/blob/main/packages/web-core/src/shared/lib/localApiTransport.ts#L91-L126
const defaultTransport: LocalApiTransport = {
  request: (pathOrUrl, init = {}) => {
    const {
      hostScope: _hostScope,
      hostId: _hostId,
      relayHostId: _relayHostId,
      ...requestInit
    } = init;
    return fetch(pathOrUrl, requestInit);
  },
  openWebSocket: (pathOrUrl, _options = {}) =>
    new WebSocket(toAbsoluteWsUrl(pathOrUrl)),
};
```

And requests are automatically scoped to the current local/host context:

```typescript name=packages/web-core/src/shared/lib/localApiTransport.ts url=https://github.com/BloopAI/vibe-kanban/blob/main/packages/web-core/src/shared/lib/localApiTransport.ts#L71-L116
export async function makeLocalApiRequest(
  pathOrUrl: string,
  init: LocalApiRequestOptions = {}
): Promise<Response> {
  return transport.request(resolveScopedPath(pathOrUrl, init), init);
}
```

So the frontend doesn’t talk directly to model runtimes. It talks to the **local backend API**, which then manages executors.

---

## 8) How live process/log streaming works

The UI listens to execution-process updates over WebSocket JSON patch streams.

```typescript name=packages/web-core/src/shared/hooks/useExecutionProcesses.ts url=https://github.com/BloopAI/vibe-kanban/blob/main/packages/web-core/src/shared/hooks/useExecutionProcesses.ts#L19-L60
if (sessionId) {
  const apiBasePath = hostId ? `/api/host/${hostId}` : '/api';
  const params = new URLSearchParams({ session_id: sessionId });
  if (typeof showSoftDeleted === 'boolean') {
    params.set('show_soft_deleted', String(showSoftDeleted));
  }
  endpoint = `${apiBasePath}/execution-processes/stream/session/ws?${params.toString()}`;
}
```

The server route upgrades to WebSocket and pushes events from the event service:

```rust name=crates/server/src/routes/execution_processes.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/server/src/routes/execution_processes.rs#L201-L253
async fn stream_execution_processes_by_session_ws(
    ws: SignedWsUpgrade,
    State(deployment): State<DeploymentImpl>,
    Query(query): Query<SessionExecutionProcessQuery>,
) -> impl IntoResponse {
    ws.on_upgrade(move |socket| async move {
        if let Err(e) = handle_execution_processes_by_session_ws(
            socket,
            deployment,
            query.session_id,
            query.show_soft_deleted.unwrap_or(false),
        )
        .await
        {
            tracing::warn!("execution processes by session WS closed: {}", e);
        }
    })
}
```

And the actual WS handler forwards stream items to the socket:

```rust name=crates/server/src/routes/execution_processes.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/server/src/routes/execution_processes.rs#L219-L285
let mut stream = deployment
    .events()
    .stream_execution_processes_for_session_raw(session_id, show_soft_deleted)
    .await?
    .map_ok(|msg| msg.to_ws_message_unchecked());
```

So communication chain is:
- executor process emits logs/events
- backend normalizes/persists them
- event service emits patches
- WebSocket route streams them
- React hooks apply patches into live UI state

---

## 9) How process logs are stored

Execution logs are persisted as JSONL files under a sessions directory:

```rust name=crates/utils/src/execution_logs.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/utils/src/execution_logs.rs#L1-L45
pub const EXECUTION_LOGS_DIRNAME: &str = "sessions";

pub fn process_log_file_path(session_id: Uuid, process_id: Uuid) -> PathBuf {
    process_log_file_path_in_root(&asset_dir(), session_id, process_id)
}

pub struct ExecutionLogWriter {
    path: PathBuf,
    file: tokio::fs::File,
}
```

At startup, old logs are migrated from DB to filesystem, which fits the “request processing history” story.

---

## 10) Remote and relay communication boundaries

There are multiple communication layers besides local HTTP/WS.

### A) Local frontend ↔ local backend
- `fetch`
- `WebSocket`
- scoped through `localApiTransport`

### B) Local/desktop/backend ↔ remote auth/review service
There’s a separate remote service for auth and review-related routes.

Example local login route on remote side:

```rust name=crates/remote/src/routes/oauth.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/remote/src/routes/oauth.rs#L114-L120
async fn local_login(
    State(state): State<AppState>,
    Json(payload): Json<LocalLoginRequest>,
) -> Result<Json<LocalLoginResponse>, LocalAuthError> {
    let response = local_login_flow(&state, &payload).await?;
    Ok(Json(response))
}
```

And the local server proxies/finalizes login via remote client:

```rust name=crates/server/src/routes/oauth.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/server/src/routes/oauth.rs#L215-L232
async fn local_login(
    State(deployment): State<DeploymentImpl>,
    Json(payload): Json<LocalLoginRequest>,
) -> Result<ResponseJson<ApiResponse<ProfileResponse>>, ApiError> {
    let client = deployment.remote_client()?;
    let response = client.local_login(&payload).await?;
```

### C) Relay/WebRTC path
There is a WebRTC relay layer that can proxy HTTP and bridge WebSocket connections to the local backend.

```rust name=crates/relay-webrtc/src/peer.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/relay-webrtc/src/peer.rs#L193-L209
/// Run the server-side peer.
///
/// Registers callbacks on the peer connection to handle incoming data channel
/// messages. HTTP requests are proxied to the local backend; WebSocket
/// connections are bridged.
pub async fn run_peer(
    peer_connection: Arc<RTCPeerConnection>,
    config: PeerConfig,
) -> Result<(), WebRtcError> {
```

And server exposes WebRTC endpoints:

```rust name=crates/server/src/routes/webrtc.rs url=https://github.com/BloopAI/vibe-kanban/blob/main/crates/server/src/routes/webrtc.rs#L9-L27
pub fn router() -> Router<DeploymentImpl> {
    Router::new()
        .route("/webrtc/offer", post(handle_offer))
        .route("/webrtc/candidate", post(handle_candidate))
}
```

This means the app can expose the local backend through a relay rather than requiring direct local-only access.

---

## 11) End-to-end lifecycle of a request

Here’s the likely end-to-end flow for a coding request:

### Step 1: User submits task from UI
The React app maintains create-mode/workspace state, including selected executor/model/profile.  
That state ultimately drives a backend execution request.

### Step 2: Backend resolves executor config
- `LocalDeployment` has loaded config + `ExecutorConfigs`
- selected executor profile determines agent type and model/provider options

### Step 3: Backend creates `ExecutionProcess`
- persisted in DB with `run_reason = codingagent`
- execution context includes session/workspace/repos

### Step 4: Container service spawns executor subprocess
Depending on executor:
- **Codex** → `npx -y @openai/codex ... app-server`
- **Claude** → Claude Code CLI over control protocol
- **Cursor** → `cursor-agent -p --output-format=stream-json`

### Step 5: Executor/session initialization
- **Codex**: connect peer → `initialize` → `thread/start`
- **Claude**: protocol setup → send/receive control messages
- **Cursor**: model passed via CLI flags

### Step 6: User request is converted to executor input
For Codex this is explicit:
- `thread_start(...)`
- `turn_start_with_mode(thread_id, input, collaboration_mode)`

### Step 7: Model/agent emits responses and tool-use events
- stdout / protocol / JSON-RPC events are read
- approval flow may interrupt or gate tool actions
- outputs are normalized into patches/messages

### Step 8: Logs and patches are stored and streamed
- logs written to JSONL
- execution/event streams emitted
- frontend subscribes via WebSocket patch streams

### Step 9: UI renders live conversation/process state
Hooks like `useExecutionProcesses` update task status and timeline in real time.

---

## 12) What I think is the key code to read first

If your goal is “understand the code that allows processing of requests,” I’d read these in order:

1. `crates/local-deployment/src/lib.rs`
   - app startup and profile/config init

2. `crates/executors/src/profile.rs`
   - executor profile loading and overrides

3. `crates/executors/src/model_selector.rs`
   - model/provider abstraction exposed to UI

4. `crates/services/src/services/container.rs`
   - orchestration layer for starting executions

5. `crates/db/src/models/execution_process.rs`
   - persistent representation of a request run

6. `crates/executors/src/executors/codex.rs`
   - codex executor setup / command building

7. `crates/executors/src/executors/codex/client.rs`
   - best file for actual request lifecycle: initialize → thread → turn

8. `crates/executors/src/executors/claude/protocol.rs`
   - how stdio-based agent communication works

9. `crates/server/src/routes/execution_processes.rs`
   - live streaming of process state/logs to clients

10. `packages/web-core/src/shared/lib/localApiTransport.ts`
    - frontend transport abstraction

11. `packages/web-core/src/shared/hooks/useExecutionProcesses.ts`
    - how UI subscribes to process updates

---

## 13) My interpretation of “local models”

If by “local models” you meant “models running entirely on-device like Ollama/llama.cpp,” I **don’t see that as a first-class integrated runtime in this repo**.

What I do see is:
- **local execution of agent CLIs**
- **model selection/config passed into those CLIs**
- **communication protocols between app and those agents**
- **local backend that orchestrates, stores, and streams request state**

So the app is more of a **local orchestration shell for coding agents** than a local inference engine itself.

If you want, I can do a **second pass focused only on the exact request path for one executor** — e.g.:
- **Codex path from UI click → subprocess spawn → `thread/start` → `turn/start`**
- **Claude path from UI click → protocol messages → approval/tool handling**
- or a **sequence diagram of frontend ↔ server ↔ executor subprocess**.