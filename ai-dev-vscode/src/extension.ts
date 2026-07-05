import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';
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
import { KanbanPanelProvider } from './panels/KanbanPanelProvider';
import { Logger } from './Logger';
import type { ProjectConfig, GitHubRepoInfo } from './types';

let backendManager: BackendProcessManager | undefined;
let signalRClient: StudioSignalRClient | undefined;
let activeApiClient: StudioApiClient | undefined;
let activeProjectSlug: string | undefined;
let statusBar: StatusBarManager | undefined;
export let log: Logger;

const DEFAULT_PLACEHOLDER = 'Waiting for AI Dev Studio backend...';

type TemplatePrecedence = 'global-first' | 'packaged-first';

type ExtensionSettings = {
  bootstrapEnabled: boolean;
  bootstrapApiPort: number;
  backendMaxAttempts: number;
  backendRetryDelayMs: number;
  templatesGlobalPath?: string;
  templatesPrecedence: TemplatePrecedence;
};

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
  const debugLogsProvider = new LogsPanelProvider(context.extensionUri, log);
  const kanbanProvider = new KanbanPanelProvider(context.extensionUri);

  context.subscriptions.push(
    vscode.window.registerWebviewViewProvider('aidev.agents', agentsProvider),
    vscode.window.registerWebviewViewProvider('aidev.messages', messagesProvider),
    vscode.window.registerWebviewViewProvider('aidev.decisions', decisionsProvider),
    vscode.window.registerWebviewViewProvider('aidev.logs', logsProvider),
    vscode.window.registerWebviewViewProvider('aidev.logsDebug', debugLogsProvider),
    logsProvider,
    debugLogsProvider,
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
    await connectBackend(context, config, workspaceFolderPath, agentsProvider, messagesProvider, decisionsProvider, kanbanProvider);
  });

  detector.on('projectRemoved', wsPath => {
    log.appendLine(`Project removed: ${wsPath}`);
    void teardown(agentsProvider, messagesProvider, decisionsProvider, kanbanProvider);
  });

  context.subscriptions.push(
    vscode.commands.registerCommand('aidev.restartBackend', async () => {
      log.appendLine('Restart backend command invoked.');
      await teardown(agentsProvider, messagesProvider, decisionsProvider, kanbanProvider);
      scanWorkspace(context, detector, agentsProvider, messagesProvider, decisionsProvider, kanbanProvider);
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand('aidev.openBoard', () => {
      kanbanProvider.openEditor();
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand('aidev.openGlobalTemplatesFolder', async () => {
      const settings = getSettings();
      const globalDir = getGlobalTemplateRoot(settings.templatesGlobalPath, context);
      fs.mkdirSync(globalDir, { recursive: true });
      await vscode.commands.executeCommand('revealFileInOS', vscode.Uri.file(globalDir));
      log.appendLine(`Opened global templates folder: ${globalDir}`);
    }),
  );

  context.subscriptions.push(
    vscode.workspace.onDidChangeWorkspaceFolders(e => {
      log.appendLine(`Workspace folders changed: +${e.added.length} -${e.removed.length}`);
      scanWorkspace(context, detector, agentsProvider, messagesProvider, decisionsProvider, kanbanProvider);
    }),
  );

  scanWorkspace(context, detector, agentsProvider, messagesProvider, decisionsProvider, kanbanProvider);
}

function scanWorkspace(
  context: vscode.ExtensionContext,
  detector: WorkspaceDetector,
  agentsProvider: AgentsPanelProvider,
  messagesProvider: MessagesPanelProvider,
  decisionsProvider: DecisionsPanelProvider,
  kanbanProvider: KanbanPanelProvider,
): void {
  const settings = getSettings();
  const folders = vscode.workspace.workspaceFolders ?? [];
  log.appendLine(`Scanning ${folders.length} folder(s): ${folders.map(f => f.uri.fsPath).join(', ') || '(none — open a folder containing .ai-dev/project.json)'}`);

  if (folders.length === 0) {
    setDisconnectedPlaceholder('No folder opened.', agentsProvider, messagesProvider, decisionsProvider);
    detector.start([]);
    return;
  }

  let validConfigs = 0;
  let createFailures = 0;

  for (const folder of folders) {
    const ensured = ensureProjectConfig(context, settings, folder.uri.fsPath);
    if (!ensured.ok) {
      createFailures++;
      continue;
    }

    validConfigs++;
  }

  if (validConfigs === 0) {
    setDisconnectedPlaceholder('No .ai-dev folder created.', agentsProvider, messagesProvider, decisionsProvider);
    log.warn('No valid .ai-dev/project.json found in opened folder(s). Backend connection requires projectSlug and apiPort.');
    if (createFailures > 0) {
      log.warn(`Failed to initialize .ai-dev in ${createFailures} folder(s).`);
    }
  } else {
    setDisconnectedPlaceholder(DEFAULT_PLACEHOLDER, agentsProvider, messagesProvider, decisionsProvider);
  }

  detector.start(folders.map(f => f.uri.fsPath));
}

function ensureProjectConfig(
  context: vscode.ExtensionContext,
  settings: ExtensionSettings,
  workspaceFolderPath: string,
): { ok: true; configPath: string } | { ok: false } {
  const aiDevDir = path.join(workspaceFolderPath, '.ai-dev');
  const configPath = path.join(aiDevDir, 'project.json');
  const folderName = path.basename(workspaceFolderPath);

  try {
    if (!settings.bootstrapEnabled) {
      if (fs.existsSync(configPath)) {
        return { ok: true, configPath };
      }
      log.warn(`Bootstrap disabled and no project config found at ${configPath}`);
      return { ok: false };
    }

    if (!fs.existsSync(aiDevDir)) {
      fs.mkdirSync(aiDevDir, { recursive: true });
      log.appendLine(`Created ${aiDevDir}`);
    }

    if (!fs.existsSync(configPath)) {
      const projectSlug = slugifyFolderName(folderName);
      const initialConfig: Record<string, unknown> = {
        projectSlug,
        apiPort: settings.bootstrapApiPort,
        name: folderName,
        description: '',
        createdAt: new Date().toISOString(),
      };
      fs.writeFileSync(configPath, JSON.stringify(initialConfig, null, 2), 'utf8');
      log.appendLine(`Created default project config at ${configPath} (slug=${projectSlug}, port=${settings.bootstrapApiPort})`);
    }

    if (!repairProjectConfig(configPath, folderName, settings.bootstrapApiPort)) {
      log.warn(`Invalid project config at ${configPath} — requires projectSlug and apiPort.`);
      return { ok: false };
    }

    ensureDefaultAgents(context, settings, aiDevDir);

    return { ok: true, configPath };
  } catch (e) {
    log.warn(`Failed to initialize .ai-dev for ${workspaceFolderPath}: ${e}`);
    return { ok: false };
  }
}

function slugifyFolderName(name: string): string {
  const slug = name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
  return slug || 'ai-dev-project';
}

function repairProjectConfig(configPath: string, folderName: string, defaultPort: number): boolean {
  const fallbackSlug = slugifyFolderName(folderName);

  let parsed: Record<string, unknown>;
  try {
    const raw = fs.readFileSync(configPath, 'utf8');
    parsed = JSON.parse(raw) as Record<string, unknown>;
  } catch {
    parsed = {};
  }

  const normalized = normalizeProjectConfig(parsed, fallbackSlug, defaultPort);
  if (!normalized) {
    return false;
  }

  const currentSlug = typeof parsed.projectSlug === 'string' ? parsed.projectSlug : undefined;
  const currentPort = typeof parsed.apiPort === 'number'
    ? parsed.apiPort
    : (typeof parsed.apiPort === 'string' ? Number(parsed.apiPort) : undefined);

  const needsWrite = currentSlug !== normalized.projectSlug || currentPort !== normalized.apiPort;
  if (needsWrite) {
    const repaired: Record<string, unknown> = {
      ...parsed,
      projectSlug: normalized.projectSlug,
      apiPort: normalized.apiPort,
    };
    fs.writeFileSync(configPath, JSON.stringify(repaired, null, 2), 'utf8');
    log.appendLine(`Repaired project config at ${configPath} (slug=${normalized.projectSlug}, port=${normalized.apiPort})`);
  }

  return true;
}

function normalizeProjectConfig(
  parsed: Record<string, unknown>,
  fallbackSlug: string,
  defaultPort: number,
): ProjectConfig | undefined {
  const projectSlug =
    firstNonEmptyString(parsed.projectSlug, parsed.slug, parsed.projectId) ?? fallbackSlug;

  const apiPort =
    firstPositiveNumber(parsed.apiPort, parsed.port) ?? defaultPort;

  if (!projectSlug || !apiPort) {
    return undefined;
  }

  return { projectSlug, apiPort };
}

function firstNonEmptyString(...values: unknown[]): string | undefined {
  for (const value of values) {
    if (typeof value !== 'string') continue;
    const trimmed = value.trim();
    if (trimmed) return trimmed;
  }
  return undefined;
}

function firstPositiveNumber(...values: unknown[]): number | undefined {
  for (const value of values) {
    if (typeof value === 'number' && Number.isFinite(value) && value > 0) {
      return Math.trunc(value);
    }
    if (typeof value === 'string') {
      const candidate = Number(value.trim());
      if (Number.isFinite(candidate) && candidate > 0) {
        return Math.trunc(candidate);
      }
    }
  }
  return undefined;
}

type AgentTemplateSeed = {
  slug?: string;
  name?: string;
  role?: string;
  model?: string;
  executor?: string;
  description?: string;
  skills?: string[];
  thinking?: string;
  thinkingLevel?: string;
};

function ensureDefaultAgents(
  context: vscode.ExtensionContext,
  settings: ExtensionSettings,
  aiDevDir: string,
): void {
  const globalTemplateRoot = getGlobalTemplateRoot(settings.templatesGlobalPath, context);
  const templateRoots = getTemplateRoots(globalTemplateRoot, context.extensionPath, settings.templatesPrecedence);
  const discovered = discoverTemplates(templateRoots);
  if (discovered.length === 0) {
    log.warn('No agent templates discovered. Skipping default agent bootstrap.');
    return;
  }

  const partialsCache = new Map<string, Record<string, string>>();
  const agentsDir = path.join(aiDevDir, 'agents');
  fs.mkdirSync(agentsDir, { recursive: true });

  let created = 0;
  for (const discoveredTemplate of discovered) {
    const targetAgentDir = path.join(agentsDir, discoveredTemplate.slug);
    if (fs.existsSync(targetAgentDir)) continue;

    let template: AgentTemplateSeed;
    try {
      template = JSON.parse(fs.readFileSync(discoveredTemplate.jsonPath, 'utf8')) as AgentTemplateSeed;
    } catch (e) {
      log.warn(`Failed to parse template ${discoveredTemplate.jsonPath}: ${e}`);
      continue;
    }

    const slug = typeof template.slug === 'string' && template.slug.length > 0 ? template.slug : discoveredTemplate.slug;
    const name = typeof template.name === 'string' && template.name.length > 0 ? template.name : discoveredTemplate.slug;

    const partials = partialsCache.get(discoveredTemplate.root) ?? loadTemplatePartials(discoveredTemplate.root);
    partialsCache.set(discoveredTemplate.root, partials);

    const content = fs.existsSync(discoveredTemplate.mdPath)
      ? renderTemplate(readComposedTemplate(discoveredTemplate.mdPath, partials, discoveredTemplate.slug), name, slug)
      : `# ${name}\n\nYou are ${name}.\n`;
    const compactContent = discoveredTemplate.compactMdPath && fs.existsSync(discoveredTemplate.compactMdPath)
      ? renderTemplate(readComposedTemplate(discoveredTemplate.compactMdPath, partials, discoveredTemplate.slug), name, slug)
      : '';

    fs.mkdirSync(targetAgentDir, { recursive: true });
    fs.mkdirSync(path.join(targetAgentDir, 'inbox'), { recursive: true });
    fs.mkdirSync(path.join(targetAgentDir, 'outbox'), { recursive: true });
    fs.mkdirSync(path.join(targetAgentDir, 'journal'), { recursive: true });

    const agentJson: Record<string, unknown> = {
      slug,
      name,
      role: template.role ?? '',
      model: template.model ?? 'claude-sonnet-4-6',
      executor: template.executor ?? 'claude',
      status: 'idle',
      description: template.description ?? '',
    };
    if (Array.isArray(template.skills) && template.skills.length > 0) {
      agentJson.skills = template.skills;
    }
    const thinking = typeof template.thinking === 'string'
      ? template.thinking
      : typeof template.thinkingLevel === 'string'
        ? template.thinkingLevel
        : '';
    if (thinking && thinking !== 'off') {
      agentJson.thinking = thinking;
    }

    fs.writeFileSync(path.join(targetAgentDir, 'agent.json'), JSON.stringify(agentJson, null, 2), 'utf8');
    fs.writeFileSync(path.join(targetAgentDir, 'CLAUDE.md'), content, 'utf8');
    if (compactContent) {
      fs.writeFileSync(path.join(targetAgentDir, 'CLAUDE.compact.md'), compactContent, 'utf8');
    }
    created++;
  }

  log.appendLine(`Created ${created} default agent(s) from templates.`);
}

type DiscoveredTemplate = {
  slug: string;
  root: string;
  jsonPath: string;
  mdPath: string;
  compactMdPath?: string;
};

function discoverTemplates(templateRoots: string[]): DiscoveredTemplate[] {
  const discovered = new Map<string, DiscoveredTemplate>();
  for (const root of templateRoots) {
    let entries: fs.Dirent[];
    try {
      entries = fs.readdirSync(root, { withFileTypes: true });
    } catch {
      continue;
    }

    for (const entry of entries) {
      if (!entry.isFile() || !entry.name.endsWith('.json')) continue;
      const slug = entry.name.replace(/\.json$/i, '');
      if (discovered.has(slug)) continue;

      const jsonPath = path.join(root, `${slug}.json`);
      const mdPath = path.join(root, `${slug}.md`);
      if (!fs.existsSync(mdPath)) continue;

      const compactMdPath = path.join(root, `${slug}.compact.md`);
      discovered.set(slug, {
        slug,
        root,
        jsonPath,
        mdPath,
        compactMdPath: fs.existsSync(compactMdPath) ? compactMdPath : undefined,
      });
    }
  }

  return [...discovered.values()].sort((a, b) => a.slug.localeCompare(b.slug));
}

function getTemplateRoots(
  globalTemplateRoot: string,
  extensionPath: string,
  precedence: TemplatePrecedence,
): string[] {
  const packaged = path.join(extensionPath, 'assets', 'agent-templates');
  const devFallback = path.resolve(extensionPath, '..', 'workspaces', 'agent-templates');
  const packagedRoot = fs.existsSync(packaged) ? packaged : fs.existsSync(devFallback) ? devFallback : undefined;

  const roots: string[] = [];
  if (precedence === 'global-first') {
    roots.push(globalTemplateRoot);
    if (packagedRoot) roots.push(packagedRoot);
  } else {
    if (packagedRoot) roots.push(packagedRoot);
    roots.push(globalTemplateRoot);
  }

  return roots.filter((value, index, arr) => arr.indexOf(value) === index);
}

function getGlobalTemplateRoot(configuredPath: string | undefined, context: vscode.ExtensionContext): string {
  if (configuredPath && configuredPath.trim().length > 0) {
    if (path.isAbsolute(configuredPath)) return configuredPath;
    const firstWorkspace = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
    if (firstWorkspace) return path.resolve(firstWorkspace, configuredPath);
    return path.resolve(os.homedir(), configuredPath);
  }

  return path.join(context.globalStorageUri.fsPath, 'agent-templates');
}

function getSettings(): ExtensionSettings {
  const config = vscode.workspace.getConfiguration('aidev');
  const bootstrapApiPort = clampInt(config.get<number>('bootstrap.apiPort', 5191), 1, 65535, 5191);
  const backendMaxAttempts = clampInt(config.get<number>('backend.health.maxAttempts', 120), 1, 3600, 120);
  const backendRetryDelayMs = clampInt(config.get<number>('backend.health.retryDelayMs', 1000), 100, 60000, 1000);
  const templatesPrecedenceRaw = config.get<string>('templates.precedence', 'global-first');
  const templatesPrecedence: TemplatePrecedence = templatesPrecedenceRaw === 'packaged-first'
    ? 'packaged-first'
    : 'global-first';

  return {
    bootstrapEnabled: config.get<boolean>('bootstrap.enabled', true),
    bootstrapApiPort,
    backendMaxAttempts,
    backendRetryDelayMs,
    templatesGlobalPath: config.get<string>('templates.globalPath'),
    templatesPrecedence,
  };
}


function clampInt(value: number, min: number, max: number, fallback: number): number {
  if (!Number.isFinite(value)) return fallback;
  return Math.min(max, Math.max(min, Math.round(value)));
}

function loadTemplatePartials(templateRoot: string): Record<string, string> {
  const partials: Record<string, string> = {};
  const sharedDir = path.join(templateRoot, 'shared');
  if (!fs.existsSync(sharedDir)) return partials;

  const files = fs.readdirSync(sharedDir, { withFileTypes: true });
  for (const entry of files) {
    if (!entry.isFile() || !entry.name.endsWith('.md')) continue;
    const key = `shared/${entry.name.replace(/\.md$/i, '')}`;
    partials[key] = fs.readFileSync(path.join(sharedDir, entry.name), 'utf8');
  }
  return partials;
}

function readComposedTemplate(templatePath: string, partials: Record<string, string>, templateSlug: string): string {
  const raw = fs.readFileSync(templatePath, 'utf8');
  return raw.replace(/\{\{>\s*([^\s}]+)\s*\}\}/g, (_match, partialKey: string) => {
    const partial = partials[partialKey];
    if (partial === undefined) {
      log.warn(`Template '${templateSlug}' references unknown partial '${partialKey}'.`);
      return '';
    }
    return partial;
  });
}

function renderTemplate(content: string, name: string, slug: string): string {
  return content
    .replace(/\{\{\s*name\s*\}\}/g, name)
    .replace(/\{\{\s*slug\s*\}\}/g, slug);
}

function setDisconnectedPlaceholder(
  message: string,
  agentsProvider: AgentsPanelProvider,
  messagesProvider: MessagesPanelProvider,
  decisionsProvider: DecisionsPanelProvider,
): void {
  agentsProvider.setPlaceholderMessage(message);
  messagesProvider.setPlaceholderMessage(message);
  decisionsProvider.setPlaceholderMessage(message);
}

function getActiveApiContext(): { api: StudioApiClient; projectSlug: string } | undefined {
  if (!activeApiClient || !activeProjectSlug) {
    return undefined;
  }

  return {
    api: activeApiClient,
    projectSlug: activeProjectSlug,
  };
}

function detectGitHubRepo(workspaceFolderPath: string): GitHubRepoInfo | undefined {
  try {
    const raw = fs.readFileSync(path.join(workspaceFolderPath, '.git', 'config'), 'utf8');
    // Capture both the remote name and its URL
    const remotePattern = /\[remote\s+"([^"]+)"\][\s\S]*?url\s*=\s*([^\r\n]+)/g;
    const remotes = new Map<string, GitHubRepoInfo>();
    let match: RegExpExecArray | null;
    while ((match = remotePattern.exec(raw)) !== null) {
      const name = match[1].trim();
      const url = match[2].trim();
      const parsed = parseGitHubUrl(url);
      if (parsed) remotes.set(name, parsed);
    }
    // Prefer upstream (parent of a fork) so issues resolve to the canonical repo
    return remotes.get('upstream') ?? remotes.get('origin') ?? remotes.values().next().value;
  } catch {
    // no .git/config or not readable — not a git repo
  }
  return undefined;
}

function parseGitHubUrl(url: string): GitHubRepoInfo | undefined {
  // HTTPS: https://github.com/owner/repo.git  or  https://github.com/owner/repo
  // SSH:   git@github.com:owner/repo.git
  const m = /github\.com[/:]([^/\s]+)\/([^/\s.]+?)(?:\.git)?$/.exec(url);
  if (m) return { owner: m[1], repo: m[2] };
  return undefined;
}

async function connectBackend(
  context: vscode.ExtensionContext,
  config: ProjectConfig,
  workspaceFolderPath: string,
  agentsProvider: AgentsPanelProvider,
  messagesProvider: MessagesPanelProvider,
  decisionsProvider: DecisionsPanelProvider,
  kanbanProvider: KanbanPanelProvider,
): Promise<void> {
  const settings = getSettings();
  const binaryName = process.platform === 'win32' ? 'ai-dev-api.exe' : 'ai-dev-api';
  const binaryPath = path.join(context.extensionPath, 'bin', binaryName);
  const hasBundledBinary = fs.existsSync(binaryPath);
  const repoRoot = path.resolve(context.extensionPath, '..');
  const devApiProject = path.join(repoRoot, 'ai-dev.api', 'ai-dev.api.csproj');
  const useDevDotnetFallback = !hasBundledBinary && fs.existsSync(devApiProject);

  log.appendLine(`Binary: ${binaryPath} (exists: ${hasBundledBinary})`);
  if (useDevDotnetFallback) {
    log.appendLine(`Using dev backend fallback: dotnet run --project ${devApiProject} --no-launch-profile`);
  } else if (!hasBundledBinary) {
    log.warn('Bundled backend missing and no local ai-dev.api project found. Expecting external backend on configured port.');
  }

  backendManager = new BackendProcessManager(
    {
      binaryPath,
      port: config.apiPort,
      maxAttempts: settings.backendMaxAttempts,
      retryDelayMs: settings.backendRetryDelayMs,
      fallbackCommand: useDevDotnetFallback
        ? {
            binary: 'dotnet',
            args: ['run', '--project', devApiProject, '--no-launch-profile'],
            cwd: repoRoot,
          }
        : undefined,
      onOutput: (line, source) => {
        const msg = `[backend:${source}] ${line}`;
        if (source === 'stderr') log.warn(msg);
        else log.info(msg);
      },
    },
    (binary, args, options) => spawn(binary, args, {
      ...options,
      stdio: 'pipe',
      env: {
        ...process.env,
        WORKSPACE_ROOT: workspaceFolderPath,
        ASPNETCORE_URLS: `http://localhost:${config.apiPort}`,
      },
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
  activeApiClient = api;
  activeProjectSlug = config.projectSlug;

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
  const gitHubRepo = detectGitHubRepo(workspaceFolderPath);
  if (gitHubRepo) {
    log.appendLine(`GitHub repo detected: ${gitHubRepo.owner}/${gitHubRepo.repo}`);
  }

  agentsProvider.connect(config.projectSlug, api, signalRClient);
  messagesProvider.connect(config.projectSlug, api, signalRClient);
  decisionsProvider.connect(config.projectSlug, api, signalRClient);
  kanbanProvider.connect(config.projectSlug, api, signalRClient, workspaceFolderPath, gitHubRepo);
  log.appendLine('Panels connected.');
}

async function teardown(
  agentsProvider: AgentsPanelProvider,
  messagesProvider: MessagesPanelProvider,
  decisionsProvider: DecisionsPanelProvider,
  kanbanProvider: KanbanPanelProvider,
): Promise<void> {
  log?.appendLine('Tearing down.');
  await signalRClient?.stop();
  signalRClient = undefined;
  activeApiClient = undefined;
  activeProjectSlug = undefined;
  backendManager?.stop();
  backendManager = undefined;
  statusBar?.setDisconnected();
  agentsProvider.disconnect();
  messagesProvider.disconnect();
  decisionsProvider.disconnect();
  kanbanProvider.disconnect();
}

export function deactivate(): void {
  log?.appendLine('Deactivating.');
  backendManager?.stop();
  void signalRClient?.stop();
}
