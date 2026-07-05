import * as vscode from 'vscode';
import { BasePanelProvider } from './BasePanelProvider';
import type { FromDecisionsWebview } from '../webviews/shared/protocol';

export class DecisionsPanelProvider extends BasePanelProvider {
  constructor(extensionUri: vscode.Uri) {
    super('aidev.decisions', extensionUri, 'decisions/main.js');
  }

  protected onConnected(view: vscode.WebviewView, disposables: vscode.Disposable[]): void {
    let webviewReady = false;
    disposables.push(this.signalR!.onDecisionsChanged(() => {
      if (webviewReady) void this.refresh(view);
    }));

    disposables.push(view.webview.onDidReceiveMessage(async (msg: FromDecisionsWebview) => {
      try {
        if (msg.type === 'ready') {
          webviewReady = true;
          await this.refresh(view);
        } else if (msg.type === 'resolve') {
          await this.api!.resolveDecision(this.projectSlug!, msg.decisionId, msg.resolution);
          await this.refresh(view);
        }
      } catch (e) {
        this.send({ type: 'error', message: String(e) });
      }
    }));
  }

  private async refresh(view: vscode.WebviewView): Promise<void> {
    if (!this.api) return;
    this.send({ type: 'loading' });
    try {
      const data = await this.api.listDecisions(this.projectSlug!, 'pending');
      this.send({ type: 'decisions', data });
      view.badge = data.length > 0
        ? { value: data.length, tooltip: `${data.length} pending decision(s)` }
        : undefined;
    } catch (e) {
      this.send({ type: 'error', message: `Failed to load decisions: ${e}` });
    }
  }
}
