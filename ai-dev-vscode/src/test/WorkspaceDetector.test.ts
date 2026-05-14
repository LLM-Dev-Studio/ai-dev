import * as path from 'path';
import { WorkspaceDetector, FileWatcherLike } from '../WorkspaceDetector';
import { DetectedProject } from '../types';

function makeWatcher(): FileWatcherLike & {
  fireCreated(p: string): void;
  fireDeleted(p: string): void;
} {
  let createdHandler: ((p: string) => void) | undefined;
  let deletedHandler: ((p: string) => void) | undefined;
  return {
    onCreated: h => { createdHandler = h; },
    onDeleted: h => { deletedHandler = h; },
    dispose: jest.fn(),
    fireCreated: (p) => createdHandler?.(p),
    fireDeleted: (p) => deletedHandler?.(p),
  };
}

const VALID_CONFIG = JSON.stringify({ projectSlug: 'my-project', apiPort: 5100 });
const WS = '/workspace/my-repo';

describe('WorkspaceDetector', () => {
  it('emits projectDetected on start when config file exists', () => {
    const watcher = makeWatcher();
    const detector = new WorkspaceDetector(
      () => watcher,
      filePath => (filePath.endsWith('project.json') ? VALID_CONFIG : undefined),
    );

    const events: DetectedProject[] = [];
    detector.on('projectDetected', e => events.push(e));
    detector.start([WS]);

    expect(events).toHaveLength(1);
    expect(events[0].config.projectSlug).toBe('my-project');
    expect(events[0].config.apiPort).toBe(5100);
    expect(events[0].workspaceFolderPath).toBe(WS);
  });

  it('does not emit projectDetected on start when config file absent', () => {
    const watcher = makeWatcher();
    const detector = new WorkspaceDetector(() => watcher, () => undefined);

    const events: DetectedProject[] = [];
    detector.on('projectDetected', e => events.push(e));
    detector.start([WS]);

    expect(events).toHaveLength(0);
  });

  it('emits projectDetected when watcher fires created for a workspace path', () => {
    const watcher = makeWatcher();
    const configPath = path.join(WS, '.ai-dev', 'project.json').replace(/\\/g, '/');
    // readFile returns undefined initially (file absent), then valid JSON after creation
    let filePresent = false;
    const detector = new WorkspaceDetector(
      () => watcher,
      filePath => (filePresent && filePath.replace(/\\/g, '/') === configPath ? VALID_CONFIG : undefined),
    );

    const events: DetectedProject[] = [];
    detector.on('projectDetected', e => events.push(e));
    detector.start([WS]);
    expect(events).toHaveLength(0); // file absent at start

    filePresent = true;
    watcher.fireCreated(configPath);

    expect(events).toHaveLength(1);
    expect(events[0].config.projectSlug).toBe('my-project');
  });

  it('emits projectRemoved when watcher fires deleted for a workspace path', () => {
    const watcher = makeWatcher();
    const configPath = path.join(WS, '.ai-dev', 'project.json').replace(/\\/g, '/');
    const detector = new WorkspaceDetector(() => watcher, () => undefined);

    const removed: string[] = [];
    detector.on('projectRemoved', p => removed.push(p));
    detector.start([WS]);
    watcher.fireDeleted(configPath);

    expect(removed).toHaveLength(1);
    expect(removed[0]).toBe(WS);
  });

  it('ignores created events for paths outside workspace folders', () => {
    const watcher = makeWatcher();
    const detector = new WorkspaceDetector(() => watcher, () => VALID_CONFIG);

    const events: DetectedProject[] = [];
    detector.on('projectDetected', e => events.push(e));
    detector.start([WS]);
    events.length = 0; // reset after start scan

    watcher.fireCreated('/some/other/path/.ai-dev/project.json');

    expect(events).toHaveLength(0);
  });

  it('does not emit projectDetected for malformed JSON', () => {
    const watcher = makeWatcher();
    const detector = new WorkspaceDetector(() => watcher, () => '{ not valid json }');

    const events: DetectedProject[] = [];
    detector.on('projectDetected', e => events.push(e));
    detector.start([WS]);

    expect(events).toHaveLength(0);
  });

  it('disposes the watcher on dispose()', () => {
    const watcher = makeWatcher();
    const detector = new WorkspaceDetector(() => watcher, () => undefined);
    detector.start([WS]);
    detector.dispose();
    expect(watcher.dispose).toHaveBeenCalled();
  });
});
