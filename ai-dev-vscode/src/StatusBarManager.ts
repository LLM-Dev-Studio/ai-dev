import * as vscode from 'vscode';

export class StatusBarManager {
  private readonly item: vscode.StatusBarItem;

  constructor() {
    this.item = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    this.item.command = 'aidev.restartBackend';
    this.setDisconnected();
    this.item.show();
  }

  setRunning(): void {
    this.item.text = '$(check) AI Dev: Running';
    this.item.tooltip = 'AI Dev Studio backend is running. Click to restart.';
    this.item.backgroundColor = undefined;
  }

  setStarting(): void {
    this.item.text = '$(loading~spin) AI Dev: Starting';
    this.item.tooltip = 'AI Dev Studio backend is starting...';
    this.item.backgroundColor = undefined;
  }

  setDisconnected(): void {
    this.item.text = '$(x) AI Dev: Disconnected';
    this.item.tooltip = 'AI Dev Studio backend is not running. Click to retry.';
    this.item.backgroundColor = new vscode.ThemeColor('statusBarItem.errorBackground');
  }

  dispose(): void {
    this.item.dispose();
  }
}
