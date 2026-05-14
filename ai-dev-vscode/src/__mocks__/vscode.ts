export class EventEmitter<T = void> {
  private listeners: Array<(e: T) => void> = [];

  readonly event = (listener: (e: T) => void) => {
    this.listeners.push(listener);
    return { dispose: () => { this.listeners = this.listeners.filter(l => l !== listener); } };
  };

  fire(value: T): void {
    for (const l of this.listeners) l(value);
  }

  dispose(): void {
    this.listeners = [];
  }
}

export const window = {
  createStatusBarItem: jest.fn(() => ({
    show: jest.fn(),
    dispose: jest.fn(),
    text: '',
    tooltip: '',
    command: '',
    backgroundColor: undefined,
  })),
};

export const workspace = {
  createFileSystemWatcher: jest.fn(),
  workspaceFolders: [],
  findFiles: jest.fn().mockResolvedValue([]),
};

export const commands = {
  registerCommand: jest.fn(),
};

export enum StatusBarAlignment { Left = 1, Right = 2 }

export class ThemeColor {
  constructor(public readonly id: string) {}
}
