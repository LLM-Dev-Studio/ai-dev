import * as vscode from 'vscode';
import { BasePanelProvider } from './BasePanelProvider';
import type { FromAgentsWebview } from '../webviews/shared/protocol';

export class AgentsPanelProvider extends BasePanelProvider {
  constructor(extensionUri: vscode.Uri) {
    super('aidev.agents', extensionUri, 'agents/main.js');
  }

  protected onConnected(view: vscode.WebviewView, disposables: vscode.Disposable[]): void {
    disposables.push(this.signalR!.onAgentsChanged(() => void this.refresh()));

    disposables.push(view.webview.onDidReceiveMessage(async (msg: FromAgentsWebview) => {
      try {
        if (msg.type === 'run') {
          await this.api!.runAgent(this.projectSlug!, msg.agentSlug);
        } else if (msg.type === 'stop') {
          await this.api!.stopAgent(this.projectSlug!, msg.agentSlug);
        }
        await this.refresh();
      } catch (e) {
        this.send({ type: 'error', message: String(e) });
      }
    }));

    void this.refresh();
  }

  private async refresh(): Promise<void> {
    if (!this.api) return;
    this.send({ type: 'loading' });
    try {
      const data = await this.api.listAgents(this.projectSlug!);
      this.send({ type: 'agents', data });
    } catch (e) {
      this.send({ type: 'error', message: `Failed to load agents: ${e}` });
    }
  }
}
