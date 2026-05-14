import * as vscode from 'vscode';
import * as crypto from 'crypto';
import type { Logger } from '../Logger';

export class LogsPanelProvider implements vscode.WebviewViewProvider, vscode.Disposable {
  private view?: vscode.WebviewView;
  private readonly disposables: vscode.Disposable[] = [];

  constructor(
    private readonly extensionUri: vscode.Uri,
    private readonly logger: Logger,
  ) {}

  resolveWebviewView(webviewView: vscode.WebviewView): void {
    this.view = webviewView;

    webviewView.webview.options = {
      enableScripts: true,
      localResourceRoots: [vscode.Uri.joinPath(this.extensionUri, 'dist', 'webviews')],
    };
    webviewView.webview.html = this.buildHtml(webviewView.webview);

    // Send buffered history when the view first opens or becomes visible again.
    webviewView.onDidChangeVisibility(() => {
      if (webviewView.visible) this.sendHistory();
    }, undefined, this.disposables);

    this.sendHistory();

    // Stream new log entries to the webview.
    this.disposables.push(
      this.logger.subscribe(entry => {
        webviewView.webview.postMessage({ type: 'entry', entry });
      }),
    );

    // When logger buffer is cleared, notify the webview.
    this.disposables.push(
      this.logger.onCleared(() => {
        webviewView.webview.postMessage({ type: 'cleared' });
      }),
    );

    // Handle clear command from the webview.
    this.disposables.push(
      webviewView.webview.onDidReceiveMessage(msg => {
        if (msg.type === 'clear') this.logger.clearBuffer();
      }),
    );
  }

  private sendHistory(): void {
    this.view?.webview.postMessage({ type: 'history', entries: this.logger.getHistory() });
  }

  dispose(): void {
    for (const d of this.disposables) d.dispose();
  }

  private buildHtml(webview: vscode.Webview): string {
    const nonce = crypto.randomBytes(16).toString('hex');
    const scriptUri = webview.asWebviewUri(
      vscode.Uri.joinPath(this.extensionUri, 'dist', 'webviews', 'logs/main.js'),
    );
    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="Content-Security-Policy"
    content="default-src 'none'; script-src 'nonce-${nonce}'; style-src 'unsafe-inline';">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <style>
    html, body { height: 100%; margin: 0; padding: 0; }
    body { font-family: var(--vscode-editor-font-family, monospace); font-size: var(--vscode-editor-font-size, 12px); color: var(--vscode-foreground); background: transparent; }
  </style>
</head>
<body>
  <div id="root"></div>
  <script nonce="${nonce}" src="${scriptUri}"></script>
</body>
</html>`;
  }
}
