import * as vscode from 'vscode';

export type LogLevel = 'info' | 'warn' | 'error';

export interface LogEntry {
  timestamp: string;
  level: LogLevel;
  message: string;
}

type LogSubscriber = (entry: LogEntry) => void;
type ClearSubscriber = () => void;

const MAX_BUFFER = 1000;

export class Logger implements vscode.Disposable {
  private readonly channel: vscode.OutputChannel;
  private readonly buffer: LogEntry[] = [];
  private readonly subscribers: LogSubscriber[] = [];
  private readonly clearSubscribers: ClearSubscriber[] = [];

  constructor(channelName: string) {
    this.channel = vscode.window.createOutputChannel(channelName);
  }

  /** Backward-compatible alias for info(). */
  appendLine(message: string): void {
    this.info(message);
  }

  info(message: string): void {
    this.emit('info', message);
  }

  warn(message: string): void {
    this.emit('warn', message);
  }

  error(message: string): void {
    this.emit('error', message);
  }

  private emit(level: LogLevel, message: string): void {
    const entry: LogEntry = { timestamp: new Date().toISOString(), level, message };
    const formatted = `[${entry.timestamp}] [${level.toUpperCase().padEnd(5)}] ${message}`;
    this.channel.appendLine(formatted);
    this.buffer.push(entry);
    if (this.buffer.length > MAX_BUFFER) this.buffer.shift();
    for (const sub of this.subscribers) sub(entry);
  }

  subscribe(fn: LogSubscriber): vscode.Disposable {
    this.subscribers.push(fn);
    return new vscode.Disposable(() => {
      const idx = this.subscribers.indexOf(fn);
      if (idx >= 0) this.subscribers.splice(idx, 1);
    });
  }

  onCleared(fn: ClearSubscriber): vscode.Disposable {
    this.clearSubscribers.push(fn);
    return new vscode.Disposable(() => {
      const idx = this.clearSubscribers.indexOf(fn);
      if (idx >= 0) this.clearSubscribers.splice(idx, 1);
    });
  }

  getHistory(): LogEntry[] {
    return [...this.buffer];
  }

  clearBuffer(): void {
    this.buffer.length = 0;
    for (const sub of this.clearSubscribers) sub();
  }

  dispose(): void {
    this.channel.dispose();
  }
}
