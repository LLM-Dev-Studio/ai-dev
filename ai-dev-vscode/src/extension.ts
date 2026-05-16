import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { spawn } from 'child_process';
import { WorkspaceDetector } from './WorkspaceDetector';
import { BackendProcessManager } from './BackendProcessManager';
import { StatusBarManager } from './StatusBarManager';
import { StudioApiClient } from './StudioApiClient';
import { StudioSignalRClient } from './StudioSignalRClient';
import { AgentsPanelProvider } from './panels/AgentsPanelProvider';
import { MessagesPanelProvider } from './panels/MessagesPanelProvider';
import { DecisionsPanelProvider } from './panels/DecisionsPanelProvider';
import { LogsPanelProvider } from './panels/LogsPanelProvider';
import { Logger } from './Logger';
import type { ProjectConfig } from './types';

let backendManager: BackendProcessManager | undefined;
let signalRClient: StudioSignalRClient | undefined;
let statusBar: StatusBarManager | undefined;
export let log: Logger;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  log = new Logger('AI Dev Studio');
  context.subscriptions.push(log);
  log.appendLine(`Activating — extensionPath: ${context.extensionPath}`);

  statusBar = new StatusBarManager();
  context.subscriptions.push(statusBar);

  // Register placeholder panel providers immediately so panels never show "—"
  const agentsProvider = new AgentsPanelProvider(context.extensionUri);
  const messagesProvider = new MessagesPanelProvider(context.extensionUri);
  const decisionsProvider = new DecisionsPanelProvider(context.extensionUri);
  const logsProvider = new LogsPanelProvider(context.extensionUri, log);

  context.subscriptions.push(
    vscode.window.registerWebviewViewProvider('aidev.agents', agentsProvider),
    vscode.window.registerWebviewViewProvider('aidev.messages', messagesProvider),
    vscode.window.registerWebviewViewProvider('aidev.decisions', decisionsProvider),
    vscode.window.registerWebviewViewProvider('aidev.logs', logsProvider),
    logsProvider,
  );

  const detector = new WorkspaceDetector(
    glob => {
      const watcher = vscode.workspace.createFileSystemWatcher(glob);
      return {
        onCreated: handler => watcher.onDidCreate(uri => handler(uri.fsPath)),
        onDeleted: handler => watcher.onDidDelete(uri => handler(uri.fsPath)),
        dispose: () => watcher.dispose(),
      };
    },
    filePath => {
      try { return fs.readFileSync(filePath, 'utf8'); }
      catch { return undefined; }
    },
  );
  context.subscriptions.push(detector);

  detector.on('projectDetected', async ({ config, workspaceFolderPath }) => {
    log.appendLine(`Project detected: slug=${config.projectSlug} port=${config.apiPort} at ${workspaceFolderPath}`);
    if (backendManager) {
      log.appendLine('Backend already running — skipping.');
      return;
    }
    await connectBackend(context, config, workspaceFolderPath, agentsProvider, messagesProvider, decisionsProvider);
  });

  detector.on('projectRemoved', wsPath => {
    log.appendLine(`Project removed: ${wsPath}`);
    void teardown(agentsProvider, messagesProvider, decisionsProvider);
  });

  context.subscriptions.push(
    vscode.commands.registerCommand('aidev.restartBackend', async () => {
      log.appendLine('Restart backend command invoked.');
      await teardown(agentsProvider, messagesProvider, decisionsProvider);
      scanWorkspace(detector);
    }),
  );

  context.subscriptions.push(
    vscode.workspace.onDidChangeWorkspaceFolders(e => {
      log.appendLine(`Workspace folders changed: +${e.added.length} -${e.removed.length}`);
      if (e.added.length > 0)
        detector.start(e.added.map(f => f.uri.fsPath));
    }),
  );

  scanWorkspace(detector);
}

function scanWorkspace(detector: WorkspaceDetector): void {
  const folders = vscode.workspace.workspaceFolders ?? [];
  log.appendLine(`Scanning ${folders.length} folder(s): ${folders.map(f => f.uri.fsPath).join(', ') || '(none — open a folder containing .ai-dev/project.json)'}`);
  detector.start(folders.map(f => f.uri.fsPath));
}

async function connectBackend(
  context: vscode.ExtensionContext,
  config: ProjectConfig,
  workspaceFolderPath: string,
  agentsProvider: AgentsPanelProvider,
  messagesProvider: MessagesPanelProvider,
  decisionsProvider: DecisionsPanelProvider,
): Promise<void> {
  const binaryName = process.platform === 'win32' ? 'ai-dev-api.exe' : 'ai-dev-api';
  const binaryPath = path.join(context.extensionPath, 'bin', binaryName);
  log.appendLine(`Binary: ${binaryPath} (exists: ${fs.existsSync(binaryPath)})`);

  backendManager = new BackendProcessManager(
    {
      binaryPath,
      port: config.apiPort,
      maxAttempts: 30,
      retryDelayMs: 1000,
      onOutput: (line, source) => {
        const msg = `[backend:${source}] ${line}`;
        if (source === 'stderr') log.warn(msg);
        else log.info(msg);
      },
    },
    (binary, args, options) => spawn(binary, args, {
      ...options,
      stdio: 'pipe',
      env: { ...process.env, WORKSPACE_ROOT: workspaceFolderPath },
    }),
    async url => {
      const r = await fetch(url);
      log.appendLine(`Health check ${url} → ${r.status}`);
      return { ok: r.ok };
    },
  );

  statusBar?.setStarting();
  log.appendLine(`Waiting for backend on port ${config.apiPort}…`);
  try {
    await backendManager.start();
    log.appendLine('Backend ready.');
  } catch (e) {
    log.appendLine(`Backend failed: ${e}`);
    statusBar?.setDisconnected();
    vscode.window.showErrorMessage('AI Dev Studio: backend failed to start. See Output > AI Dev Studio.');
    return;
  }

  const baseUrl = `http://localhost:${config.apiPort}`;
  const api = new StudioApiClient(baseUrl);

  signalRClient = new StudioSignalRClient(`${baseUrl}/hubs/project`);
  signalRClient.onConnectionStateChanged(state => {
    log.appendLine(`SignalR: ${state}`);
    if (state === 'connected') statusBar?.setRunning();
    else if (state === 'connecting') statusBar?.setStarting();
    else statusBar?.setDisconnected();
  });

  try {
    await signalRClient.start(config.projectSlug);
    log.appendLine('SignalR connected.');
  } catch (e) {
    log.appendLine(`SignalR failed (non-fatal, REST still works): ${e}`);
  }

  statusBar?.setRunning();

  // Wire up the already-registered providers with live API + SignalR
  agentsProvider.connect(config.projectSlug, api, signalRClient);
  messagesProvider.connect(config.projectSlug, api, signalRClient);
  decisionsProvider.connect(config.projectSlug, api, signalRClient);
  log.appendLine('Panels connected.');
}

async function teardown(
  agentsProvider: AgentsPanelProvider,
  messagesProvider: MessagesPanelProvider,
  decisionsProvider: DecisionsPanelProvider,
): Promise<void> {
  log?.appendLine('Tearing down.');
  await signalRClient?.stop();
  signalRClient = undefined;
  backendManager?.stop();
  backendManager = undefined;
  statusBar?.setDisconnected();
  agentsProvider.disconnect();
  messagesProvider.disconnect();
  decisionsProvider.disconnect();
}

export function deactivate(): void {
  log?.appendLine('Deactivating.');
  backendManager?.stop();
  void signalRClient?.stop();
}
