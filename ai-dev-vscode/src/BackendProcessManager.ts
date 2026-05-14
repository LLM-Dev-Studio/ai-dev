export type FileExistsFn = (path: string) => boolean;

export type ProcessLike = {
  kill(): void;
  on(event: 'exit', handler: (code: number | null) => void): void;
  on(event: 'error', handler: (err: Error) => void): void;
};

export type Spawner = (
  binary: string,
  args: string[],
  options: { env?: NodeJS.ProcessEnv; cwd?: string },
) => ProcessLike;

export type Fetcher = (url: string) => Promise<{ ok: boolean }>;

export type BackendState = 'not-started' | 'starting' | 'ready' | 'stopped';

export interface BackendProcessManagerOptions {
  binaryPath: string;
  port: number;
  maxAttempts: number;
  retryDelayMs: number;
}

export class BackendProcessManager {
  private _state: BackendState = 'not-started';
  private _process?: ProcessLike;

  constructor(
    private readonly options: BackendProcessManagerOptions,
    private readonly spawner: Spawner,
    private readonly fetcher: Fetcher,
    private readonly delay: (ms: number) => Promise<void> = ms =>
      new Promise(resolve => setTimeout(resolve, ms)),
    private readonly fileExists: FileExistsFn = p => {
      try { require('fs').accessSync(p); return true; } catch { return false; }
    },
  ) {}

  get state(): BackendState {
    return this._state;
  }

  async start(): Promise<void> {
    if (this._state !== 'not-started') return;
    this._state = 'starting';

    // Skip spawning when binary is absent — dev mode where backend runs externally.
    if (this.fileExists(this.options.binaryPath)) {
      this._process = this.spawner(this.options.binaryPath, [], {});
      this._process.on('exit', () => {
        if (this._state !== 'stopped') this._state = 'stopped';
      });
      this._process.on('error', () => {
        if (this._state !== 'stopped') this._state = 'stopped';
      });
    }

    const healthUrl = `http://localhost:${this.options.port}/api/health`;
    for (let attempt = 0; attempt < this.options.maxAttempts; attempt++) {
      try {
        const res = await this.fetcher(healthUrl);
        if (res.ok) {
          this._state = 'ready';
          return;
        }
      } catch {
        // backend not ready yet
      }
      await this.delay(this.options.retryDelayMs);
    }

    if (this._process) {
      try { this._process.kill(); } catch { }
    }
    this._state = 'stopped';
    throw new Error(
      `AI Dev backend did not become healthy after ${this.options.maxAttempts} attempts`,
    );
  }

  stop(): void {
    if (this._state === 'stopped' || this._state === 'not-started') return;
    if (this._process) {
      try { this._process.kill(); } catch { }
    }
    this._state = 'stopped';
  }
}
