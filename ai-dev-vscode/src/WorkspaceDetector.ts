import { EventEmitter } from 'events';
import * as path from 'path';
import { DetectedProject, ProjectConfig } from './types';

export interface FileWatcherLike {
  onCreated(handler: (filePath: string) => void): void;
  onDeleted(handler: (filePath: string) => void): void;
  dispose(): void;
}

export type WatcherFactory = (glob: string) => FileWatcherLike;
export type FileReader = (filePath: string) => string | undefined;

export declare interface WorkspaceDetector {
  on(event: 'projectDetected', listener: (project: DetectedProject) => void): this;
  on(event: 'projectRemoved', listener: (workspaceFolderPath: string) => void): this;
  emit(event: 'projectDetected', project: DetectedProject): boolean;
  emit(event: 'projectRemoved', workspaceFolderPath: string): boolean;
}

export class WorkspaceDetector extends EventEmitter {
  private watcher?: FileWatcherLike;

  constructor(
    private readonly createWatcher: WatcherFactory,
    private readonly readFile: FileReader,
  ) {
    super();
  }

  start(workspacePaths: string[]): void {
    for (const wsPath of workspacePaths) {
      const configPath = path.join(wsPath, '.ai-dev', 'project.json');
      this.tryEmitDetected(configPath, wsPath);
    }

    this.watcher = this.createWatcher('**/.ai-dev/project.json');
    this.watcher.onCreated(filePath => {
      const wsPath = this.resolveWorkspacePath(filePath, workspacePaths);
      if (wsPath) this.tryEmitDetected(filePath, wsPath);
    });
    this.watcher.onDeleted(filePath => {
      const wsPath = this.resolveWorkspacePath(filePath, workspacePaths);
      if (wsPath) this.emit('projectRemoved', wsPath);
    });
  }

  private tryEmitDetected(filePath: string, workspaceFolderPath: string): void {
    const raw = this.readFile(filePath);
    if (!raw) return;
    try {
      const config = parseProjectConfig(JSON.parse(raw) as Record<string, unknown>);
      if (config) {
        this.emit('projectDetected', { config, workspaceFolderPath });
      }
    } catch {
      // malformed JSON — skip silently
    }
  }

  private resolveWorkspacePath(filePath: string, workspacePaths: string[]): string | undefined {
    const normalised = filePath.replace(/\\/g, '/');
    return workspacePaths.find(ws => {
      const normWs = ws.replace(/\\/g, '/');
      return normalised.startsWith(normWs + '/');
    });
  }

  dispose(): void {
    this.watcher?.dispose();
  }
}

function parseProjectConfig(parsed: Record<string, unknown>): ProjectConfig | undefined {
  const projectSlug = firstNonEmptyString(parsed.projectSlug, parsed.slug, parsed.projectId);
  const apiPort = firstPositiveNumber(parsed.apiPort, parsed.port);

  if (!projectSlug || !apiPort) {
    return undefined;
  }

  return { projectSlug, apiPort };
}

function firstNonEmptyString(...values: unknown[]): string | undefined {
  for (const value of values) {
    if (typeof value !== 'string') continue;
    const trimmed = value.trim();
    if (trimmed) return trimmed;
  }
  return undefined;
}

function firstPositiveNumber(...values: unknown[]): number | undefined {
  for (const value of values) {
    if (typeof value === 'number' && Number.isFinite(value) && value > 0) {
      return Math.trunc(value);
    }
    if (typeof value === 'string') {
      const candidate = Number(value.trim());
      if (Number.isFinite(candidate) && candidate > 0) {
        return Math.trunc(candidate);
      }
    }
  }
  return undefined;
}
