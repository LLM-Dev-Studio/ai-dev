import { BackendProcessManager, ProcessLike, Spawner, Fetcher } from '../BackendProcessManager';

function makeProcess(): ProcessLike & { triggerExit(code: number | null): void } {
  const handlers: Record<string, ((...args: unknown[]) => void)[]> = {};
  return {
    kill: jest.fn(),
    on: (event, handler) => {
      (handlers[event] ??= []).push(handler as (...args: unknown[]) => void);
    },
    triggerExit: code => handlers['exit']?.forEach(h => h(code)),
  };
}

const OPTIONS = { binaryPath: '/bin/ai-dev-api', port: 5100, maxAttempts: 3, retryDelayMs: 0 };
const noDelay = () => Promise.resolve();
const binaryExists = () => true;
const binaryAbsent = () => false;

describe('BackendProcessManager', () => {
  it('starts in not-started state', () => {
    const mgr = new BackendProcessManager(OPTIONS, jest.fn(), jest.fn(), noDelay, binaryExists);
    expect(mgr.state).toBe('not-started');
  });

  it('transitions not-started → starting → ready on successful health check', async () => {
    const proc = makeProcess();
    const spawner: Spawner = jest.fn(() => proc);
    const fetcher: Fetcher = jest.fn().mockResolvedValue({ ok: true });

    const mgr = new BackendProcessManager(OPTIONS, spawner, fetcher, noDelay, binaryExists);
    const states: string[] = [mgr.state];
    (spawner as jest.Mock).mockImplementation(() => {
      states.push('spawned');
      return proc;
    });

    await mgr.start();
    states.push(mgr.state);

    expect(states).toContain('spawned');
    expect(mgr.state).toBe('ready');
    expect(fetcher).toHaveBeenCalledWith('http://localhost:5100/api/health');
  });

  it('retries health check until success', async () => {
    const proc = makeProcess();
    const fetcher: Fetcher = jest.fn()
      .mockResolvedValueOnce({ ok: false })
      .mockResolvedValueOnce({ ok: false })
      .mockResolvedValueOnce({ ok: true });

    const mgr = new BackendProcessManager(OPTIONS, () => proc, fetcher, noDelay, binaryExists);
    await mgr.start();

    expect(mgr.state).toBe('ready');
    expect(fetcher).toHaveBeenCalledTimes(3);
  });

  it('transitions to stopped and throws after maxAttempts exhausted', async () => {
    const proc = makeProcess();
    const fetcher: Fetcher = jest.fn().mockResolvedValue({ ok: false });

    const mgr = new BackendProcessManager(OPTIONS, () => proc, fetcher, noDelay, binaryExists);
    await expect(mgr.start()).rejects.toThrow(/healthy after 3 attempts/);

    expect(mgr.state).toBe('stopped');
    expect(proc.kill).toHaveBeenCalled();
  });

  it('counts fetch errors as failed attempts', async () => {
    const proc = makeProcess();
    const fetcher: Fetcher = jest.fn().mockRejectedValue(new Error('ECONNREFUSED'));

    const mgr = new BackendProcessManager(OPTIONS, () => proc, fetcher, noDelay, binaryExists);
    await expect(mgr.start()).rejects.toThrow(/healthy after 3 attempts/);
    expect(fetcher).toHaveBeenCalledTimes(3);
  });

  it('stop() kills the process and transitions to stopped', async () => {
    const proc = makeProcess();
    const fetcher: Fetcher = jest.fn().mockResolvedValue({ ok: true });

    const mgr = new BackendProcessManager(OPTIONS, () => proc, fetcher, noDelay, binaryExists);
    await mgr.start();
    expect(mgr.state).toBe('ready');

    mgr.stop();
    expect(mgr.state).toBe('stopped');
    expect(proc.kill).toHaveBeenCalled();
  });

  it('stop() is a no-op when not-started', () => {
    const proc = makeProcess();
    const mgr = new BackendProcessManager(OPTIONS, () => proc, jest.fn(), noDelay, binaryExists);
    mgr.stop();
    expect(mgr.state).toBe('not-started');
    expect(proc.kill).not.toHaveBeenCalled();
  });

  it('start() is idempotent — second call is ignored', async () => {
    const proc = makeProcess();
    const fetcher: Fetcher = jest.fn().mockResolvedValue({ ok: true });
    const spawner: Spawner = jest.fn(() => proc);

    const mgr = new BackendProcessManager(OPTIONS, spawner, fetcher, noDelay, binaryExists);
    await mgr.start();
    await mgr.start();

    expect(spawner).toHaveBeenCalledTimes(1);
  });

  it('transitions to stopped when spawned process exits unexpectedly', async () => {
    const proc = makeProcess();
    const fetcher: Fetcher = jest.fn().mockResolvedValue({ ok: true });

    const mgr = new BackendProcessManager(OPTIONS, () => proc, fetcher, noDelay, binaryExists);
    await mgr.start();
    expect(mgr.state).toBe('ready');

    proc.triggerExit(1);
    expect(mgr.state).toBe('stopped');
  });

  it('skips spawning when binary is absent (dev mode), still reaches ready via health check', async () => {
    const spawner: Spawner = jest.fn();
    const fetcher: Fetcher = jest.fn().mockResolvedValue({ ok: true });

    const mgr = new BackendProcessManager(OPTIONS, spawner, fetcher, noDelay, binaryAbsent);
    await mgr.start();

    expect(spawner).not.toHaveBeenCalled();
    expect(mgr.state).toBe('ready');
  });
});
