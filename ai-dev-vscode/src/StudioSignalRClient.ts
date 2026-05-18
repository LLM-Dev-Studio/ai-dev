import * as vscode from 'vscode';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected';

export interface StateChangedMessage {
  projectSlug: string;
  kinds: string[];
}

export interface HubConnectionFactory {
  build(url: string): HubConnection;
}

const defaultFactory: HubConnectionFactory = {
  build: url =>
    new HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: ctx => {
          const base = Math.min(1000 * 2 ** ctx.previousRetryCount, 30_000);
          return base + Math.random() * 1000;
        },
      })
      .configureLogging(LogLevel.Warning)
      .build(),
};

export class StudioSignalRClient {
  private connection?: HubConnection;
  private projectSlug?: string;

  private readonly _onConnectionStateChanged = new vscode.EventEmitter<ConnectionState>();
  private readonly _onAgentsChanged = new vscode.EventEmitter<void>();
  private readonly _onMessagesChanged = new vscode.EventEmitter<void>();
  private readonly _onDecisionsChanged = new vscode.EventEmitter<void>();
  private readonly _onBoardChanged = new vscode.EventEmitter<void>();

  readonly onConnectionStateChanged = this._onConnectionStateChanged.event;
  readonly onAgentsChanged = this._onAgentsChanged.event;
  readonly onMessagesChanged = this._onMessagesChanged.event;
  readonly onDecisionsChanged = this._onDecisionsChanged.event;
  readonly onBoardChanged = this._onBoardChanged.event;

  private _connectionState: ConnectionState = 'disconnected';

  constructor(
    private readonly hubUrl: string,
    private readonly factory: HubConnectionFactory = defaultFactory,
  ) {}

  get connectionState(): ConnectionState {
    return this._connectionState;
  }

  async start(projectSlug: string): Promise<void> {
    this.projectSlug = projectSlug;
    this.connection = this.factory.build(this.hubUrl);

    this.connection.onreconnecting(() => this.setState('connecting'));
    this.connection.onreconnected(() => this.setState('connected'));
    this.connection.onclose(() => this.setState('disconnected'));

    this.connection.on('StateChanged', (msg: StateChangedMessage) => {
      if (msg.projectSlug !== this.projectSlug) return;
      const kinds = msg.kinds.map(k => k.toLowerCase());
      if (kinds.includes('agents')) this._onAgentsChanged.fire();
      if (kinds.includes('messages')) this._onMessagesChanged.fire();
      if (kinds.includes('decisions')) this._onDecisionsChanged.fire();
      if (kinds.includes('board')) {
        this._onBoardChanged.fire();
        this._onAgentsChanged.fire(); // board changes affect agent views
      }
    });

    this.setState('connecting');
    await this.connection.start();
    await this.connection.invoke('JoinProject', projectSlug);
    this.setState('connected');
  }

  async stop(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      try { await this.connection.invoke('LeaveProject', this.projectSlug); } catch { }
    }
    await this.connection?.stop();
    this.setState('disconnected');
  }

  dispose(): void {
    this._onConnectionStateChanged.dispose();
    this._onAgentsChanged.dispose();
    this._onMessagesChanged.dispose();
    this._onDecisionsChanged.dispose();
    this._onBoardChanged.dispose();
    void this.connection?.stop();
  }

  private setState(state: ConnectionState): void {
    if (this._connectionState === state) return;
    this._connectionState = state;
    this._onConnectionStateChanged.fire(state);
  }
}
