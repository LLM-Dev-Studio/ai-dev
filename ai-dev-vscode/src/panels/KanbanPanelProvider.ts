import * as vscode from 'vscode';
import * as crypto from 'crypto';
import { BasePanelProvider } from './BasePanelProvider';
import { StudioApiClient } from '../StudioApiClient';
import { StudioSignalRClient } from '../StudioSignalRClient';
import { GitHubBoardClient } from '../GitHubBoardClient';
import type { BoardData, GitHubRepoInfo } from '../types';
import type { FromKanbanWebview } from '../webviews/shared/protocol';

const SCOPES_ISSUES_ONLY = ['repo'];
const SCOPES_WITH_PROJECTS = ['repo', 'project'];

export class KanbanPanelProvider extends BasePanelProvider {
  private static readonly backlogColumnId = 'backlog';
  private editorPanel?: vscode.WebviewPanel;
  private editorDisposables: vscode.Disposable[] = [];
  private currentBoard?: BoardData;
  private gitHubRepo?: GitHubRepoInfo;
  private cachedGitHubClient?: GitHubBoardClient;

  constructor(extensionUri: vscode.Uri) {
    super('ai-dev-studio.kanban', extensionUri, 'kanban/main.js');
  }

  openEditor(): void {
    if (this.editorPanel) {
      this.editorPanel.reveal(vscode.ViewColumn.Active, true);
      return;
    }

    const panel = vscode.window.createWebviewPanel(
      'aidev.boardEditor',
      'AI Dev Board',
      { viewColumn: vscode.ViewColumn.Active, preserveFocus: false },
      {
        enableScripts: true,
        localResourceRoots: [vscode.Uri.joinPath(this.extensionUri, 'dist', 'webviews')],
        retainContextWhenHidden: true,
      },
    );

    this.editorPanel = panel;
    panel.webview.html = this.buildPanelHtml(panel.webview);

    const onDispose = panel.onDidDispose(() => {
      onDispose.dispose();
      this.disposeEditorConnections();
      this.editorPanel = undefined;
    });

    if (this.api) {
      this.wireEditorPanel(panel);
    } else {
      panel.webview.html = this.buildPlaceholderHtml();
    }
  }

  override connect(
    projectSlug: string,
    api: StudioApiClient,
    signalR: StudioSignalRClient,
    workspaceFolderPath?: string,
    gitHubRepo?: GitHubRepoInfo,
  ): void {
    this.gitHubRepo = gitHubRepo;
    this.cachedGitHubClient = undefined; // reset on reconnect
    super.connect(projectSlug, api, signalR, workspaceFolderPath);
    if (this.editorPanel) {
      this.editorPanel.webview.html = this.buildPanelHtml(this.editorPanel.webview);
      this.wireEditorPanel(this.editorPanel);
    }
  }

  override disconnect(): void {
    this.gitHubRepo = undefined;
    this.cachedGitHubClient = undefined;
    this.disposeEditorConnections();
    if (this.editorPanel) {
      this.editorPanel.webview.html = this.buildPlaceholderHtml();
    }
    super.disconnect();
  }

  protected onConnected(view: vscode.WebviewView, disposables: vscode.Disposable[]): void {
    let webviewReady = false;

    disposables.push(this.signalR!.onBoardChanged(() => {
      if (webviewReady) {
        void this.refresh(view);
      }
    }));

    disposables.push(view.webview.onDidReceiveMessage(async (msg: FromKanbanWebview) => {
      try {
        if (msg.type === 'ready') {
          webviewReady = true;
          await this.refresh(view);
          return;
        }

        if (msg.type === 'refresh') {
          await this.refresh(view);
          return;
        }

        if (msg.type === 'githubSignIn') {
          this.cachedGitHubClient = undefined;
          await this.refreshWithGitHubAuth(view, true);
          return;
        }

        await this.handleAction(msg);
        await this.refresh(view);
      } catch (e) {
        this.send({ type: 'error', message: String(e) });
      }
    }));
  }

  protected override send(message: unknown): void {
    super.send(message);
    this.editorPanel?.webview.postMessage(message);
  }

  private wireEditorPanel(panel: vscode.WebviewPanel): void {
    this.disposeEditorConnections();

    let webviewReady = false;

    this.editorDisposables.push(this.signalR!.onBoardChanged(() => {
      if (webviewReady) {
        void this.refreshPanel(panel);
      }
    }));

    this.editorDisposables.push(panel.webview.onDidReceiveMessage(async (msg: FromKanbanWebview) => {
      try {
        if (msg.type === 'ready') {
          webviewReady = true;
          await this.refreshPanel(panel);
          return;
        }

        if (msg.type === 'refresh') {
          await this.refreshPanel(panel);
          return;
        }

        if (msg.type === 'githubSignIn') {
          this.cachedGitHubClient = undefined;
          await this.refreshPanelWithGitHubAuth(panel, true);
          return;
        }

        await this.handleAction(msg);
        await this.refreshPanel(panel);
      } catch (e) {
        this.send({ type: 'error', message: String(e) });
      }
    }));
  }

  private async refresh(view: vscode.WebviewView): Promise<void> {
    if (this.gitHubRepo) {
      await this.refreshWithGitHubAuth(view, false);
      return;
    }

    this.send({ type: 'loading' });
    try {
      const board = await this.loadLocalBoard();
      this.send({ type: 'board', data: board });
      view.badge = board.columns.length > 0
        ? { value: Object.keys(board.tasks).length, tooltip: 'Board task count' }
        : undefined;
    } catch (e) {
      this.send({ type: 'error', message: `Failed to load board: ${e}` });
    }
  }

  private async refreshWithGitHubAuth(view: vscode.WebviewView, forcePrompt: boolean): Promise<void> {
    if (!this.gitHubRepo) return;

    this.send({ type: 'loading' });
    try {
      const client = await this.getGitHubClient(forcePrompt);
      if (!client) {
        this.send({
          type: 'github-sign-in-required',
          owner: this.gitHubRepo.owner,
          repo: this.gitHubRepo.repo,
        });
        return;
      }

      const board = await client.getBoard();
      this.currentBoard = board;
      this.send({ type: 'board', data: board, githubRepo: `${this.gitHubRepo.owner}/${this.gitHubRepo.repo}` });
      view.badge = { value: Object.keys(board.tasks).length, tooltip: 'GitHub Issues' };
    } catch (e) {
      this.send({ type: 'error', message: `Failed to load GitHub Issues: ${e}` });
    }
  }

  private async refreshPanel(panel: vscode.WebviewPanel): Promise<void> {
    if (this.gitHubRepo) {
      await this.refreshPanelWithGitHubAuth(panel, false);
      return;
    }

    this.send({ type: 'loading' });
    try {
      const board = await this.loadLocalBoard();
      this.send({ type: 'board', data: board });
      panel.title = `AI Dev Board (${Object.keys(board.tasks).length})`;
    } catch (e) {
      this.send({ type: 'error', message: `Failed to load board: ${e}` });
    }
  }

  private async refreshPanelWithGitHubAuth(panel: vscode.WebviewPanel, forcePrompt: boolean): Promise<void> {
    if (!this.gitHubRepo) return;

    this.send({ type: 'loading' });
    try {
      const client = await this.getGitHubClient(forcePrompt);
      if (!client) {
        this.send({
          type: 'github-sign-in-required',
          owner: this.gitHubRepo.owner,
          repo: this.gitHubRepo.repo,
        });
        return;
      }

      const board = await client.getBoard();
      this.currentBoard = board;
      this.send({ type: 'board', data: board, githubRepo: `${this.gitHubRepo.owner}/${this.gitHubRepo.repo}` });
      panel.title = `AI Dev Board — ${this.gitHubRepo.owner}/${this.gitHubRepo.repo} (${Object.keys(board.tasks).length})`;
    } catch (e) {
      this.send({ type: 'error', message: `Failed to load GitHub Issues: ${e}` });
    }
  }

  private async getGitHubClient(createIfNone: boolean): Promise<GitHubBoardClient | undefined> {
    if (!this.gitHubRepo) return undefined;
    if (this.cachedGitHubClient) return this.cachedGitHubClient;

    let session: vscode.AuthenticationSession | undefined;

    try {
      // Try with project scopes silently first — enables linked-project auto-discovery
      session = await vscode.authentication.getSession('github', SCOPES_WITH_PROJECTS, { silent: true }) ?? undefined;
      if (!session) {
        // Fall back to repo-only — issues still load, project status sync skipped
        session = await vscode.authentication.getSession('github', SCOPES_ISSUES_ONLY, { silent: true }) ?? undefined;
      }
      if (!session && createIfNone) {
        session = await vscode.authentication.getSession('github', SCOPES_WITH_PROJECTS, { createIfNone: true }) ?? undefined;
      }
      if (!session) return undefined;

      this.cachedGitHubClient = new GitHubBoardClient(
        this.gitHubRepo.owner,
        this.gitHubRepo.repo,
        session.accessToken,
      );
      return this.cachedGitHubClient;
    } catch {
      return undefined;
    }
  }

  private disposeEditorConnections(): void {
    for (const disposable of this.editorDisposables) {
      disposable.dispose();
    }
    this.editorDisposables = [];
  }

  private buildPlaceholderHtml(): string {
    return `<!DOCTYPE html><html lang="en"><head><meta charset="UTF-8">
<style>body{font-family:var(--vscode-font-family);font-size:var(--vscode-font-size);color:var(--vscode-descriptionForeground);background:transparent;margin:0;padding:8px;}</style>
</head><body><p>Waiting for AI Dev Studio backend...</p></body></html>`;
  }

  private buildPanelHtml(webview: vscode.Webview): string {
    const nonce = crypto.randomBytes(16).toString('hex');
    const scriptUri = webview.asWebviewUri(
      vscode.Uri.joinPath(this.extensionUri, 'dist', 'webviews', 'kanban/main.js'),
    );

    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="Content-Security-Policy"
    content="default-src 'none'; script-src 'nonce-${nonce}'; style-src 'unsafe-inline';">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <style>
    body { font-family: var(--vscode-font-family); font-size: var(--vscode-font-size); color: var(--vscode-foreground); background: transparent; margin: 0; padding: 8px; }
    button { background: var(--vscode-button-background); color: var(--vscode-button-foreground); border: none; padding: 4px 10px; cursor: pointer; border-radius: 2px; font-size: inherit; }
    button:hover { background: var(--vscode-button-hoverBackground); }
    button.secondary { background: var(--vscode-button-secondaryBackground); color: var(--vscode-button-secondaryForeground); }
    button.secondary:hover { background: var(--vscode-button-secondaryHoverBackground); }
    input, textarea, select { background: var(--vscode-input-background); color: var(--vscode-input-foreground); border: 1px solid var(--vscode-input-border, transparent); padding: 4px 6px; font-size: inherit; font-family: inherit; box-sizing: border-box; }
    input:focus, textarea:focus, select:focus { outline: 1px solid var(--vscode-focusBorder); border-color: var(--vscode-focusBorder); }
    .error { color: var(--vscode-errorForeground); padding: 8px 0; }
    .muted { color: var(--vscode-descriptionForeground); font-size: 0.9em; }
  </style>
</head>
<body>
  <div id="root"></div>
  <script nonce="${nonce}" src="${scriptUri}"></script>
</body>
</html>`;
  }

  private async loadLocalBoard(): Promise<BoardData> {
    if (!this.api || !this.projectSlug) {
      throw new Error('Board API is not connected.');
    }

    const board = await this.api.getBoard(this.projectSlug);
    this.currentBoard = board;
    return board;
  }

  private async handleAction(msg: Exclude<FromKanbanWebview, { type: 'ready' } | { type: 'githubSignIn' }>): Promise<void> {
    if (this.gitHubRepo) {
      await this.handleGitHubAction(msg);
      return;
    }

    await this.handleLocalAction(msg);
  }

  private async handleGitHubAction(
    msg: Exclude<FromKanbanWebview, { type: 'ready' } | { type: 'githubSignIn' }>,
  ): Promise<void> {
    const client = await this.getGitHubClient(false);
    if (!client) {
      throw new Error('GitHub is not authenticated.');
    }

    if (msg.type === 'createTask') {
      const task = await client.createIssue(
        msg.columnId ?? KanbanPanelProvider.backlogColumnId,
        msg.title.trim(),
        msg.description,
      );
      this.currentBoard = undefined;
      return;
    }

    if (msg.type === 'updateTask') {
      await client.updateIssue(msg.taskId, msg.columnId, msg.title.trim(), msg.description);
      this.currentBoard = undefined;
      return;
    }

    if (msg.type === 'moveTask') {
      const board = this.currentBoard ?? await (async () => {
        const b = await client.getBoard();
        this.currentBoard = b;
        return b;
      })();
      const task = board.tasks[msg.taskId];
      if (!task) throw new Error(`Task '${msg.taskId}' not found.`);
      await client.updateIssue(msg.taskId, msg.toColumnId, task.title, task.description);
      this.currentBoard = undefined;
      return;
    }

    if (msg.type === 'deleteTask') {
      await client.closeIssue(msg.taskId);
      this.currentBoard = undefined;
    }
  }

  private async handleLocalAction(
    msg: Exclude<FromKanbanWebview, { type: 'ready' } | { type: 'githubSignIn' }>,
  ): Promise<void> {
    if (!this.api || !this.projectSlug) {
      throw new Error('Board API is not connected.');
    }

    if (msg.type === 'createTask') {
      const board = this.currentBoard ?? await this.loadLocalBoard();
      const targetColumnId = this.resolveBacklogColumnId(board) ?? msg.columnId;
      await this.api.createBoardTask(this.projectSlug, {
        columnId: targetColumnId,
        title: msg.title.trim(),
        description: this.normalizeOptional(msg.description),
        assignee: this.normalizeOptional(msg.assignee),
        priority: this.normalizePriority(msg.priority),
        tags: this.normalizeTags(msg.tags),
      });
      return;
    }

    if (msg.type === 'updateTask') {
      await this.api.updateBoardTask(this.projectSlug, msg.taskId, {
        columnId: msg.columnId,
        title: msg.title.trim(),
        description: this.normalizeOptional(msg.description),
        assignee: this.normalizeOptional(msg.assignee),
        priority: this.normalizePriority(msg.priority),
        tags: this.normalizeTags(msg.tags),
      });
      return;
    }

    if (msg.type === 'moveTask') {
      const board = this.currentBoard ?? await this.loadLocalBoard();
      const task = board.tasks[msg.taskId];
      if (!task) {
        throw new Error(`Task '${msg.taskId}' was not found.`);
      }

      await this.api.updateBoardTask(this.projectSlug, msg.taskId, {
        columnId: msg.toColumnId,
        title: task.title,
        description: task.description,
        assignee: task.assignee,
        priority: this.normalizePriority(task.priority),
        tags: this.normalizeTags(task.tags),
      });
      return;
    }

    if (msg.type === 'deleteTask') {
      await this.api.deleteBoardTask(this.projectSlug, msg.taskId);
    }
  }

  private normalizePriority(priority?: string): string {
    const trimmed = priority?.trim().toLowerCase();
    if (!trimmed) return 'normal';
    return trimmed;
  }

  private normalizeOptional(value?: string): string | undefined {
    const trimmed = value?.trim();
    return trimmed ? trimmed : undefined;
  }

  private normalizeTags(tags?: string[]): string[] {
    if (!Array.isArray(tags)) {
      return [];
    }

    const seen = new Set<string>();
    const normalized: string[] = [];
    for (const tag of tags) {
      const clean = tag.trim();
      const key = clean.toLowerCase();
      if (clean && !seen.has(key)) {
        seen.add(key);
        normalized.push(clean);
      }
    }
    return normalized;
  }

  private resolveBacklogColumnId(board: BoardData): string | undefined {
    return board.columns.find(column => column.id === KanbanPanelProvider.backlogColumnId)?.id;
  }
}
