import * as vscode from 'vscode';
import { BasePanelProvider } from './BasePanelProvider';
import type { FromMessagesWebview } from '../webviews/shared/protocol';

export class MessagesPanelProvider extends BasePanelProvider {
  constructor(extensionUri: vscode.Uri) {
    super('aidev.messages', extensionUri, 'messages/main.js');
  }

  protected onConnected(view: vscode.WebviewView, disposables: vscode.Disposable[]): void {
    disposables.push(this.signalR!.onMessagesChanged(() => void this.refresh(view)));

    disposables.push(view.webview.onDidReceiveMessage(async (msg: FromMessagesWebview) => {
      try {
        if (msg.type === 'process') {
          await this.api!.processMessage(this.projectSlug!, msg.agentSlug, msg.fileName);
          await this.refresh(view);
        }
      } catch (e) {
        this.send({ type: 'error', message: String(e) });
      }
    }));

    void this.refresh(view);
  }

  private async refresh(view: vscode.WebviewView): Promise<void> {
    if (!this.api) return;
    this.send({ type: 'loading' });
    try {
      const data = await this.api.listAllMessages(this.projectSlug!);
      this.send({ type: 'messages', data });
      view.badge = data.length > 0
        ? { value: data.length, tooltip: `${data.length} unprocessed message(s)` }
        : undefined;
    } catch (e) {
      this.send({ type: 'error', message: `Failed to load messages: ${e}` });
    }
  }
}
