import * as vscode from 'vscode';
import * as crypto from 'crypto';
import { StudioApiClient } from '../StudioApiClient';
import { StudioSignalRClient } from '../StudioSignalRClient';

export abstract class BasePanelProvider implements vscode.WebviewViewProvider {
  protected view?: vscode.WebviewView;
  protected projectSlug?: string;
  protected api?: StudioApiClient;
  protected signalR?: StudioSignalRClient;
  private connectionDisposables: vscode.Disposable[] = [];

  constructor(
    protected readonly viewId: string,
    protected readonly extensionUri: vscode.Uri,
    protected readonly webviewScript: string,
  ) {}

  resolveWebviewView(webviewView: vscode.WebviewView): void {
    this.view = webviewView;
    if (this.api) {
      this.enableScripts(webviewView);
      this.wireUp(webviewView);
    } else {
      webviewView.webview.options = { enableScripts: false };
      webviewView.webview.html = this.buildPlaceholderHtml();
    }
  }

  connect(projectSlug: string, api: StudioApiClient, signalR: StudioSignalRClient): void {
    this.projectSlug = projectSlug;
    this.api = api;
    this.signalR = signalR;
    if (this.view) {
      this.enableScripts(this.view);
      this.wireUp(this.view);
    }
  }

  private enableScripts(webviewView: vscode.WebviewView): void {
    webviewView.webview.options = {
      enableScripts: true,
      localResourceRoots: [vscode.Uri.joinPath(this.extensionUri, 'dist', 'webviews')],
    };
    webviewView.webview.html = this.buildHtml(webviewView.webview);
  }

  disconnect(): void {
    for (const d of this.connectionDisposables) d.dispose();
    this.connectionDisposables = [];
    this.projectSlug = undefined;
    this.api = undefined;
    this.signalR = undefined;
    this.send({ type: 'loading' });
  }

  protected send(message: unknown): void {
    this.view?.webview.postMessage(message);
  }

  private wireUp(view: vscode.WebviewView): void {
    for (const d of this.connectionDisposables) d.dispose();
    this.connectionDisposables = [];
    this.onConnected(view, this.connectionDisposables);
  }

  protected abstract onConnected(view: vscode.WebviewView, disposables: vscode.Disposable[]): void;

  private buildPlaceholderHtml(): string {
    return `<!DOCTYPE html><html lang="en"><head><meta charset="UTF-8">
<style>body{font-family:var(--vscode-font-family);font-size:var(--vscode-font-size);color:var(--vscode-descriptionForeground);background:transparent;margin:0;padding:8px;}</style>
</head><body><p>Waiting for AI Dev Studio backend…</p></body></html>`;
  }

  private buildHtml(webview: vscode.Webview): string {
    const nonce = crypto.randomBytes(16).toString('hex');
    const scriptUri = webview.asWebviewUri(
      vscode.Uri.joinPath(this.extensionUri, 'dist', 'webviews', this.webviewScript),
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
    input, textarea { background: var(--vscode-input-background); color: var(--vscode-input-foreground); border: 1px solid var(--vscode-input-border, transparent); padding: 4px 6px; font-size: inherit; font-family: inherit; box-sizing: border-box; }
    input:focus, textarea:focus { outline: 1px solid var(--vscode-focusBorder); border-color: var(--vscode-focusBorder); }
    .error { color: var(--vscode-errorForeground); padding: 8px 0; }
    .muted { color: var(--vscode-descriptionForeground); font-size: 0.9em; }
    .badge { background: var(--vscode-badge-background); color: var(--vscode-badge-foreground); border-radius: 10px; padding: 1px 6px; font-size: 0.75em; }
  </style>
</head>
<body>
  <div id="root"></div>
  <script nonce="${nonce}" src="${scriptUri}"></script>
</body>
</html>`;
  }
}
