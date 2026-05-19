import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { getVsCodeApi } from '../shared/vscodeApi';
import type { BoardColumnItem, BoardData, BoardTaskItem } from '../../types';
import type { ToKanbanWebview } from '../shared/protocol';

const vscode = getVsCodeApi();

// ─── Constants ────────────────────────────────────────────────────────────────

const DONE_COLUMN_ID = 'done';
const DONE_DAY_PRESETS = [7, 14, 30, 0]; // 0 = all

const PRIORITY_META: Record<string, { label: string; color: string }> = {
  critical: { label: 'Critical', color: '#e11d48' },
  high:     { label: 'High',     color: '#ea580c' },
  normal:   { label: 'Normal',   color: '#ca8a04' },
  low:      { label: 'Low',      color: '#16a34a' },
};

const COLUMN_ACCENTS = ['#6366f1', '#f59e0b', '#8b5cf6', '#10b981', '#ef4444', '#3b82f6'];

// ─── Helpers ──────────────────────────────────────────────────────────────────

function normalizeTags(input: string): string[] {
  const raw = input.split(',').map(s => s.trim()).filter(Boolean);
  return Array.from(new Set(raw.map(t => t.toLowerCase())));
}

function getInitials(name: string): string {
  return name.split(' ').slice(0, 2).map(w => w[0]?.toUpperCase() ?? '').join('');
}

function tasksForColumn(column: BoardColumnItem, board: BoardData): BoardTaskItem[] {
  return column.taskIds
    .map(id => board.tasks[id])
    .filter((t): t is BoardTaskItem => t !== undefined);
}

function taskDoneDate(task: BoardTaskItem): Date | null {
  const raw = task.completedAt ?? task.movedAt;
  if (!raw) return null;
  const d = new Date(raw);
  return isNaN(d.getTime()) ? null : d;
}

function formatRelative(iso: string | undefined): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (isNaN(d.getTime())) return null;
  const days = Math.floor((Date.now() - d.getTime()) / 86_400_000);
  if (days === 0) return 'today';
  if (days === 1) return 'yesterday';
  if (days < 7) return `${days}d ago`;
  if (days < 30) return `${Math.floor(days / 7)}w ago`;
  if (days < 365) return `${Math.floor(days / 30)}mo ago`;
  return `${Math.floor(days / 365)}y ago`;
}

// ─── SelectedTask ─────────────────────────────────────────────────────────────

interface SelectedTask extends BoardTaskItem {
  columnId: string;
}

// ─── PriorityChip ─────────────────────────────────────────────────────────────

function PriorityChip({ priority, size = 'sm' }: { priority: string; size?: 'sm' | 'xs' }) {
  const meta = PRIORITY_META[priority] ?? PRIORITY_META['normal'];
  return (
    <span
      className={`priority-chip priority-chip--${size}`}
      style={{ borderColor: `${meta.color}55`, color: meta.color, background: `${meta.color}1a` }}
    >
      {meta.label}
    </span>
  );
}

// ─── TaskCard ─────────────────────────────────────────────────────────────────

function TaskCard({
  task, isSelected, onSelect, isDragging, onDragStart, onDragEnd,
}: {
  task: BoardTaskItem;
  isSelected: boolean;
  onSelect: (t: BoardTaskItem) => void;
  isDragging: boolean;
  onDragStart: (id: string) => void;
  onDragEnd: () => void;
}): React.JSX.Element {
  const relDate = formatRelative(task.createdAt);
  return (
    <div
      className={`task-card${isDragging ? ' is-dragging' : ''}${isSelected ? ' is-selected' : ''}`}
      draggable
      onDragStart={e => {
        e.dataTransfer.setData('text/task-id', task.id);
        e.dataTransfer.effectAllowed = 'move';
        onDragStart(task.id);
      }}
      onDragEnd={onDragEnd}
      onClick={() => onSelect(task)}
    >
      <div className="card-top">
        <PriorityChip priority={task.priority ?? 'normal'} size="xs" />
        {task.assignee
          ? <span className="assignee-avatar" title={task.assignee}>{getInitials(task.assignee) || '?'}</span>
          : null}
      </div>

      <span className="task-title">{task.title}</span>

      {task.description
        ? <p className="task-description">{task.description}</p>
        : null}

      {(task.tags && task.tags.length > 0) || relDate ? (
        <div className="card-footer">
          {task.tags && task.tags.length > 0 ? (
            <div className="tag-list">
              {task.tags.slice(0, 2).map(tag => <span key={tag} className="tag-pill">{tag}</span>)}
              {task.tags.length > 2 ? <span className="tag-pill">+{task.tags.length - 2}</span> : null}
            </div>
          ) : null}
          {relDate ? <span className="card-date">{relDate}</span> : null}
        </div>
      ) : null}
    </div>
  );
}

// ─── TaskPanel ────────────────────────────────────────────────────────────────

function TaskPanel({
  task, columns, onSave, onDelete, onClose,
}: {
  task: SelectedTask;
  columns: BoardColumnItem[];
  onSave: (taskId: string, updates: Omit<SelectedTask, 'id' | 'columnId'> & { columnId: string }) => void;
  onDelete: (taskId: string) => void;
  onClose: () => void;
}): React.JSX.Element {
  const [title, setTitle] = useState(task.title);
  const [columnId, setColumnId] = useState(task.columnId);
  const [description, setDescription] = useState(task.description ?? '');
  const [assignee, setAssignee] = useState(task.assignee ?? '');
  const [priority, setPriority] = useState(task.priority ?? 'normal');
  const [tags, setTags] = useState((task.tags ?? []).join(', '));
  const [dirty, setDirty] = useState(false);
  const titleRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    setTitle(task.title);
    setColumnId(task.columnId);
    setDescription(task.description ?? '');
    setAssignee(task.assignee ?? '');
    setPriority(task.priority ?? 'normal');
    setTags((task.tags ?? []).join(', '));
    setDirty(false);
    setTimeout(() => titleRef.current?.focus(), 50);
  }, [task.id]);

  const mark = () => setDirty(true);

  const save = () => {
    if (!title.trim()) return;
    onSave(task.id, { title: title.trim(), columnId, description, assignee, priority, tags: normalizeTags(tags) });
    setDirty(false);
  };

  const priorityMeta = PRIORITY_META[priority] ?? PRIORITY_META['normal'];
  const relCreated = formatRelative(task.createdAt);
  const relMoved = formatRelative(task.movedAt);

  return (
    <aside className="task-panel">
      <div className="panel-header">
        <span className="panel-heading">Task Detail</span>
        <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
          {dirty ? <span className="panel-dirty">unsaved</span> : null}
          <button className="icon-btn" title="Close panel (Esc)" onClick={onClose}>✕</button>
        </div>
      </div>

      <div className="panel-body">
        <div className="panel-field">
          <textarea
            ref={titleRef}
            className="panel-title-input"
            value={title}
            rows={2}
            placeholder="Task title"
            onChange={e => { setTitle(e.target.value); mark(); }}
            onKeyDown={e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); save(); } }}
          />
        </div>

        <div className="panel-row">
          <div className="panel-field">
            <label>Priority</label>
            <select
              value={priority}
              style={{ borderLeftColor: priorityMeta.color, borderLeftWidth: '3px' }}
              onChange={e => { setPriority(e.target.value); mark(); }}
            >
              <option value="critical">Critical</option>
              <option value="high">High</option>
              <option value="normal">Normal</option>
              <option value="low">Low</option>
            </select>
          </div>

          <div className="panel-field">
            <label>Column</label>
            <select value={columnId} onChange={e => { setColumnId(e.target.value); mark(); }}>
              {columns.map(c => <option key={c.id} value={c.id}>{c.title}</option>)}
            </select>
          </div>
        </div>

        <div className="panel-field">
          <label>Assignee</label>
          <input
            value={assignee}
            placeholder="Name"
            onChange={e => { setAssignee(e.target.value); mark(); }}
          />
        </div>

        <div className="panel-field">
          <label>Tags</label>
          <input
            value={tags}
            placeholder="tag1, tag2"
            onChange={e => { setTags(e.target.value); mark(); }}
          />
          {tags.trim() ? (
            <div className="tag-preview">
              {normalizeTags(tags).map(t => <span key={t} className="tag-pill">{t}</span>)}
            </div>
          ) : null}
        </div>

        <div className="panel-field panel-field--grow" style={{ display: 'flex', flexDirection: 'column' }}>
          <label>Description</label>
          <textarea
            style={{ flex: 1, minHeight: '80px', resize: 'none' }}
            value={description}
            placeholder="Add a description…"
            onChange={e => { setDescription(e.target.value); mark(); }}
          />
        </div>

        <div className="panel-meta">
          {relCreated ? <span>Created {relCreated}</span> : null}
          {relMoved ? <span>Moved {relMoved}</span> : null}
          <span className="panel-meta-id">#{task.id}</span>
        </div>
      </div>

      <div className="panel-footer">
        <button onClick={save} disabled={!dirty || !title.trim()}>Save</button>
        <button className="secondary" onClick={onClose}>Close</button>
        <div className="spacer" />
        <button className="danger" onClick={() => onDelete(task.id)}>Delete</button>
      </div>
    </aside>
  );
}

// ─── App ──────────────────────────────────────────────────────────────────────

function App(): React.JSX.Element {
  const [state, setState] = useState<'loading' | 'ready' | 'error' | 'github-sign-in-required'>('loading');
  const [error, setError] = useState('');
  const [githubRepo, setGithubRepo] = useState('');
  const [board, setBoard] = useState<BoardData>({ columns: [], tasks: {} });
  const [creatingIn, setCreatingIn] = useState<string | null>(null);
  const [newTitle, setNewTitle] = useState('');
  const [dragOverColumn, setDragOverColumn] = useState<string | null>(null);
  const [draggingTaskId, setDraggingTaskId] = useState<string | null>(null);
  const [selected, setSelected] = useState<SelectedTask | null>(null);
  const [doneDays, setDoneDays] = useState(7);
  const [renamingColumn, setRenamingColumn] = useState<string | null>(null);
  const [renameValue, setRenameValue] = useState('');
  const [addingColumn, setAddingColumn] = useState(false);
  const [newColumnTitle, setNewColumnTitle] = useState('');
  const renameInputRef = useRef<HTMLInputElement>(null);
  const newColumnInputRef = useRef<HTMLInputElement>(null);

  const createInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const handler = (e: MessageEvent<ToKanbanWebview>) => {
      const msg = e.data;
      if (msg.type === 'loading') { setState('loading'); return; }
      if (msg.type === 'error') { setError(msg.message); setState('error'); return; }
      if (msg.type === 'github-sign-in-required') {
        setGithubRepo(`${msg.owner}/${msg.repo}`);
        setState('github-sign-in-required');
        return;
      }
      setBoard(msg.data);
      if (msg.githubRepo) setGithubRepo(msg.githubRepo);
      setState('ready');
    };
    window.addEventListener('message', handler);
    vscode.postMessage({ type: 'ready' });
    return () => window.removeEventListener('message', handler);
  }, []);

  useEffect(() => {
    if (creatingIn) setTimeout(() => createInputRef.current?.focus(), 40);
  }, [creatingIn]);

  useEffect(() => {
    if (renamingColumn) setTimeout(() => renameInputRef.current?.select(), 40);
  }, [renamingColumn]);

  useEffect(() => {
    if (addingColumn) setTimeout(() => newColumnInputRef.current?.focus(), 40);
  }, [addingColumn]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setSelected(null); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const selectTask = (task: BoardTaskItem) => {
    const col = board.columns.find(c => c.taskIds.includes(task.id));
    setSelected({ ...task, columnId: col?.id ?? '' });
  };

  const startRename = (column: BoardColumnItem) => {
    setRenamingColumn(column.id);
    setRenameValue(column.title);
  };

  const submitRename = () => {
    if (!renamingColumn || !renameValue.trim()) { setRenamingColumn(null); return; }
    vscode.postMessage({ type: 'renameColumn', columnId: renamingColumn, title: renameValue.trim() });
    setRenamingColumn(null);
  };

  const submitDeleteColumn = (columnId: string) => {
    vscode.postMessage({ type: 'deleteColumn', columnId });
  };

  const submitAddColumn = () => {
    const title = newColumnTitle.trim();
    if (!title) { setAddingColumn(false); return; }
    const id = title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'column';
    vscode.postMessage({ type: 'addColumn', id, title });
    setNewColumnTitle('');
    setAddingColumn(false);
  };

  const submitNewTask = () => {
    if (!newTitle.trim() || !creatingIn) return;
    vscode.postMessage({ type: 'createTask', columnId: creatingIn, title: newTitle.trim(), priority: 'normal', tags: [] });
    setNewTitle('');
    setCreatingIn(null);
  };

  const saveTask = (taskId: string, updates: { title: string; columnId: string; description: string; assignee: string; priority: string; tags: string[] }) => {
    vscode.postMessage({ type: 'updateTask', taskId, ...updates });
    // Optimistically update selected so dirty indicator clears
    if (selected?.id === taskId) {
      setSelected(prev => prev ? { ...prev, ...updates } : prev);
    }
  };

  const deleteTask = (taskId: string) => {
    vscode.postMessage({ type: 'deleteTask', taskId });
    setSelected(null);
  };

  const onDropTask = (e: React.DragEvent<HTMLElement>, toColumnId: string) => {
    e.preventDefault();
    const taskId = e.dataTransfer.getData('text/task-id');
    if (taskId) vscode.postMessage({ type: 'moveTask', taskId, toColumnId });
    setDragOverColumn(null);
  };

  if (state === 'loading') return <p style={{ padding: '12px', color: 'var(--vscode-descriptionForeground)' }}>Loading board…</p>;
  if (state === 'error') return <p style={{ padding: '12px', color: '#ef4444' }}>{error}</p>;
  if (state === 'github-sign-in-required') {
    return (
      <div style={{ padding: '12px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
        <p style={{ margin: 0, color: 'var(--vscode-descriptionForeground)', fontSize: '0.85rem' }}>
          This project uses GitHub Issues{githubRepo ? ` (${githubRepo})` : ''}.
        </p>
        <button onClick={() => vscode.postMessage({ type: 'githubSignIn' })}>Sign in to GitHub</button>
      </div>
    );
  }

  const totalTasks = Object.keys(board.tasks).length;

  return (
    <div className="kanban-shell">
      <style>{`
        *, *::before, *::after { box-sizing: border-box; }

        :root {
          --kb-bg:           var(--vscode-editor-background);
          --kb-surface:      var(--vscode-sideBar-background);
          --kb-border:       var(--vscode-widget-border, #3c3c3c);
          --kb-border-focus: var(--vscode-focusBorder);
          --kb-fg:           var(--vscode-foreground);
          --kb-muted:        var(--vscode-descriptionForeground);
          --kb-accent:       var(--vscode-textLink-foreground);
          --kb-badge-bg:     var(--vscode-badge-background);
          --kb-badge-fg:     var(--vscode-badge-foreground);
          --kb-input-bg:     var(--vscode-input-background);
          --kb-input-fg:     var(--vscode-input-foreground);
          --kb-input-border: var(--vscode-input-border, #3c3c3c);
          --kb-btn-bg:       var(--vscode-button-background);
          --kb-btn-fg:       var(--vscode-button-foreground);
          --kb-btn-hover:    var(--vscode-button-hoverBackground);
          --kb-btn2-bg:      var(--vscode-button-secondaryBackground, #3c3c3c);
          --kb-btn2-fg:      var(--vscode-button-secondaryForeground, var(--kb-fg));
          --kb-btn2-hover:   var(--vscode-button-secondaryHoverBackground, #4c4c4c);
        }

        .kanban-shell {
          font-family: var(--vscode-font-family);
          font-size: var(--vscode-font-size, 13px);
          color: var(--kb-fg);
          background: var(--kb-bg);
          display: flex;
          flex-direction: column;
          height: 100vh;
          overflow: hidden;
        }

        /* ─── Header ─── */
        .board-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          gap: 8px;
          padding: 8px 10px 7px;
          border-bottom: 1px solid color-mix(in srgb, var(--kb-border) 50%, transparent);
          flex-shrink: 0;
        }

        .board-title {
          margin: 0;
          font-size: 0.73rem;
          font-weight: 600;
          text-transform: uppercase;
          letter-spacing: 0.09em;
          opacity: 0.6;
        }

        .board-repo { font-size: 0.67rem; color: var(--kb-muted); margin-top: 1px; }

        .header-right { display: flex; align-items: center; gap: 5px; }

        .task-count {
          border-radius: 999px;
          padding: 1px 7px;
          font-size: 0.67rem;
          font-variant-numeric: tabular-nums;
          background: var(--kb-badge-bg);
          color: var(--kb-badge-fg);
        }

        .icon-btn {
          background: transparent;
          border: 1px solid transparent;
          color: var(--kb-muted);
          cursor: pointer;
          border-radius: 4px;
          padding: 2px 6px;
          font-size: 0.82rem;
          line-height: 1.4;
          transition: color 120ms, border-color 120ms, background 120ms;
        }
        .icon-btn:hover {
          color: var(--kb-fg);
          border-color: var(--kb-border);
          background: color-mix(in srgb, var(--kb-border) 25%, transparent);
        }

        /* ─── Body (board + panel) ─── */
        .kanban-body {
          display: flex;
          flex: 1;
          overflow: hidden;
          gap: 0;
        }

        /* ─── Columns ─── */
        .board-scroll {
          flex: 1;
          overflow-x: auto;
          overflow-y: auto;
          padding: 10px;
        }

        .board-columns {
          display: grid;
          grid-auto-flow: column;
          grid-auto-columns: minmax(195px, 260px);
          gap: 8px;
          align-items: start;
          min-height: 100%;
        }

        .board-column {
          border: 1px solid color-mix(in srgb, var(--kb-border) 60%, transparent);
          border-top: 2px solid var(--col-accent, var(--kb-border));
          border-radius: 6px;
          padding: 7px;
          background: color-mix(in srgb, var(--kb-surface) 50%, var(--kb-bg) 50%);
          display: flex;
          flex-direction: column;
          gap: 5px;
          transition: background 130ms, border-color 130ms;
        }

        .board-column.drag-over {
          background: color-mix(in srgb, var(--kb-border-focus) 9%, var(--kb-bg) 91%);
          border-color: var(--kb-border-focus);
        }

        .column-head {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding-bottom: 3px;
        }

        .column-title {
          margin: 0;
          font-size: 0.68rem;
          font-weight: 600;
          text-transform: uppercase;
          letter-spacing: 0.09em;
          color: var(--kb-muted);
        }

        .column-head-right { display: flex; align-items: center; gap: 4px; }

        /* ─── Column rename / delete ─── */
        .column-title--editable { cursor: text; }
        .column-title--editable:hover { color: var(--kb-fg); }

        .column-rename-input {
          font-size: 0.68rem;
          font-weight: 600;
          text-transform: uppercase;
          letter-spacing: 0.08em;
          background: var(--kb-input-bg);
          border: 1px solid var(--kb-border-focus);
          border-radius: 3px;
          color: var(--kb-fg);
          padding: 1px 5px;
          outline: none;
          flex: 1;
          min-width: 0;
        }

        .col-delete-btn {
          background: transparent;
          border: 1px solid transparent;
          color: color-mix(in srgb, var(--kb-muted) 40%, transparent);
          cursor: pointer;
          border-radius: 3px;
          padding: 1px 4px;
          font-size: 0.65rem;
          line-height: 1.4;
          transition: color 110ms, border-color 110ms, background 110ms;
        }
        .col-delete-btn:not(:disabled):hover {
          color: #f87171;
          border-color: color-mix(in srgb, #dc2626 35%, transparent);
          background: color-mix(in srgb, #dc2626 12%, transparent);
        }
        .col-delete-btn:disabled { opacity: 0.3; cursor: not-allowed; }

        /* ─── Add column card ─── */
        .add-column-card {
          border-style: dashed;
          border-color: color-mix(in srgb, var(--kb-border) 45%, transparent);
          background: transparent;
          gap: 6px;
        }

        .add-column-input {
          background: transparent;
          border: none;
          border-bottom: 1px solid var(--kb-border-focus);
          border-radius: 0;
          color: var(--kb-fg);
          font-size: 0.8rem;
          padding: 3px 0;
          outline: none;
          width: 100%;
        }
        .add-column-input::placeholder { color: var(--kb-muted); }

        .done-window-btn {
          background: color-mix(in srgb, var(--kb-badge-bg) 45%, transparent);
          color: var(--kb-muted);
          border: 1px solid color-mix(in srgb, var(--kb-border) 50%, transparent);
          border-radius: 999px;
          padding: 1px 6px;
          font-size: 0.58rem;
          font-variant-numeric: tabular-nums;
          cursor: pointer;
          transition: color 110ms, border-color 110ms, background 110ms;
        }
        .done-window-btn:hover {
          color: var(--kb-fg);
          border-color: var(--kb-border-focus);
          background: color-mix(in srgb, var(--kb-border) 28%, transparent);
        }

        .column-count {
          min-width: 17px;
          height: 16px;
          display: inline-flex;
          align-items: center;
          justify-content: center;
          padding: 0 4px;
          border-radius: 999px;
          font-size: 0.6rem;
          font-variant-numeric: tabular-nums;
          background: color-mix(in srgb, var(--kb-badge-bg) 60%, transparent);
          color: var(--kb-badge-fg);
        }

        .column-cards { display: flex; flex-direction: column; gap: 5px; min-height: 30px; }

        .done-hidden-btn {
          background: transparent;
          border: 1px dashed color-mix(in srgb, var(--kb-border) 40%, transparent);
          border-radius: 4px;
          color: color-mix(in srgb, var(--kb-muted) 65%, transparent);
          font-family: var(--vscode-font-family);
          font-size: 0.63rem;
          padding: 4px 8px;
          cursor: pointer;
          width: 100%;
          text-align: center;
          transition: color 110ms, border-color 110ms;
        }
        .done-hidden-btn:hover { color: var(--kb-muted); border-color: var(--kb-border); }

        /* ─── Cards ─── */
        .task-card {
          border: 1px solid color-mix(in srgb, var(--kb-border) 50%, transparent);
          border-radius: 5px;
          background: var(--kb-bg);
          padding: 7px 8px;
          display: flex;
          flex-direction: column;
          gap: 4px;
          cursor: pointer;
          user-select: none;
          transition: transform 110ms ease, border-color 110ms ease,
                      box-shadow 110ms ease, opacity 110ms ease;
        }

        .task-card:hover {
          transform: translateY(-1px);
          border-color: color-mix(in srgb, var(--kb-border-focus) 50%, transparent);
          box-shadow: 0 2px 6px color-mix(in srgb, black 18%, transparent);
        }

        .task-card.is-selected {
          border-color: var(--kb-border-focus);
          box-shadow: 0 0 0 1px color-mix(in srgb, var(--kb-border-focus) 40%, transparent);
        }

        .task-card.is-dragging { opacity: 0.3; transform: scale(0.97); }

        .card-top {
          display: flex;
          justify-content: space-between;
          align-items: center;
          gap: 5px;
        }

        .task-title {
          font-size: 0.8rem;
          font-weight: 500;
          line-height: 1.35;
          word-break: break-word;
        }

        .priority-chip {
          border: 1px solid;
          border-radius: 999px;
          font-weight: 700;
          text-transform: uppercase;
          letter-spacing: 0.05em;
          white-space: nowrap;
          flex-shrink: 0;
        }
        .priority-chip--xs { padding: 1px 5px; font-size: 0.56rem; }
        .priority-chip--sm { padding: 1px 6px; font-size: 0.6rem; }

        .task-description {
          margin: 0;
          color: var(--kb-muted);
          font-size: 0.72rem;
          line-height: 1.4;
          display: -webkit-box;
          -webkit-line-clamp: 2;
          -webkit-box-orient: vertical;
          overflow: hidden;
        }

        .card-footer {
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: 5px;
          margin-top: 1px;
        }

        .card-date {
          font-size: 0.6rem;
          color: color-mix(in srgb, var(--kb-muted) 70%, transparent);
          white-space: nowrap;
          margin-left: auto;
        }

        .assignee-avatar {
          display: inline-flex;
          align-items: center;
          justify-content: center;
          width: 18px;
          height: 18px;
          border-radius: 999px;
          flex-shrink: 0;
          font-size: 0.55rem;
          font-weight: 700;
          background: color-mix(in srgb, var(--kb-accent) 22%, transparent);
          color: var(--kb-accent);
          border: 1px solid color-mix(in srgb, var(--kb-accent) 32%, transparent);
        }

        .tag-list { display: flex; gap: 3px; overflow: hidden; flex-wrap: nowrap; }

        .tag-pill {
          border-radius: 3px;
          padding: 1px 5px;
          font-size: 0.57rem;
          background: color-mix(in srgb, var(--kb-border) 50%, transparent);
          color: var(--kb-muted);
          white-space: nowrap;
        }

        /* ─── Add task button ─── */
        .add-task-btn {
          display: flex;
          align-items: center;
          gap: 5px;
          width: 100%;
          background: transparent;
          border: 1px dashed color-mix(in srgb, var(--kb-border) 55%, transparent);
          border-radius: 4px;
          color: color-mix(in srgb, var(--kb-muted) 75%, transparent);
          font-family: var(--vscode-font-family);
          font-size: 0.72rem;
          padding: 5px 7px;
          cursor: pointer;
          text-align: left;
          transition: color 110ms, border-color 110ms, background 110ms;
        }
        .add-task-btn:hover {
          color: var(--kb-fg);
          border-color: var(--kb-border-focus);
          background: color-mix(in srgb, var(--kb-border) 10%, transparent);
        }

        .task-create {
          display: flex;
          flex-direction: column;
          gap: 5px;
          border: 1px solid var(--kb-border-focus);
          border-radius: 5px;
          padding: 6px 7px;
          background: var(--kb-bg);
        }

        .task-create input {
          background: transparent;
          border: none;
          outline: none;
          color: var(--kb-fg);
          font-family: var(--vscode-font-family);
          font-size: 0.8rem;
          padding: 1px 0;
          width: 100%;
        }
        .task-create input::placeholder { color: var(--kb-muted); }
        .task-create-actions { display: flex; gap: 4px; }

        /* ─── Empty state ─── */
        .empty-column {
          font-size: 0.68rem;
          color: color-mix(in srgb, var(--kb-muted) 45%, transparent);
          text-align: center;
          padding: 10px 4px;
          border: 1px dashed color-mix(in srgb, var(--kb-border) 30%, transparent);
          border-radius: 4px;
        }

        /* ─── Buttons ─── */
        button {
          font-family: var(--vscode-font-family);
          font-size: 0.76rem;
          border-radius: 3px;
          padding: 3px 10px;
          cursor: pointer;
          border: none;
          transition: background 110ms;
        }

        button:not(.secondary):not(.icon-btn):not(.add-task-btn):not(.danger):not(.done-window-btn):not(.done-hidden-btn):not(.col-delete-btn) {
          background: var(--kb-btn-bg);
          color: var(--kb-btn-fg);
        }
        button:not(.secondary):not(.icon-btn):not(.add-task-btn):not(.danger):not(.done-window-btn):not(.done-hidden-btn):not(.col-delete-btn):hover {
          background: var(--kb-btn-hover);
        }
        button:disabled { opacity: 0.45; cursor: default; }

        button.secondary { background: var(--kb-btn2-bg); color: var(--kb-btn2-fg); }
        button.secondary:hover { background: var(--kb-btn2-hover); }

        button.danger {
          background: color-mix(in srgb, #dc2626 16%, transparent);
          color: #f87171;
          border: 1px solid color-mix(in srgb, #dc2626 32%, transparent);
        }
        button.danger:hover { background: color-mix(in srgb, #dc2626 25%, transparent); }

        /* ─── Form controls ─── */
        input, select, textarea {
          font-family: var(--vscode-font-family);
          font-size: 0.8rem;
          background: var(--kb-input-bg);
          color: var(--kb-input-fg);
          border: 1px solid var(--kb-input-border);
          border-radius: 3px;
          padding: 4px 7px;
          outline: none;
          width: 100%;
        }
        input:focus, select:focus, textarea:focus { border-color: var(--kb-border-focus); }
        textarea { resize: vertical; }

        label {
          display: block;
          font-size: 0.65rem;
          color: var(--kb-muted);
          text-transform: uppercase;
          letter-spacing: 0.07em;
          margin-bottom: 3px;
        }

        /* ─── Task Panel ─── */
        .task-panel {
          width: 270px;
          flex-shrink: 0;
          border-left: 1px solid color-mix(in srgb, var(--kb-border) 60%, transparent);
          background: color-mix(in srgb, var(--kb-surface) 70%, var(--kb-bg) 30%);
          display: flex;
          flex-direction: column;
          overflow: hidden;
          animation: panel-in 160ms ease;
        }

        @keyframes panel-in {
          from { transform: translateX(20px); opacity: 0; }
          to   { transform: translateX(0);    opacity: 1; }
        }

        .panel-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 8px 10px 7px;
          border-bottom: 1px solid color-mix(in srgb, var(--kb-border) 50%, transparent);
          flex-shrink: 0;
        }

        .panel-heading {
          font-size: 0.68rem;
          font-weight: 600;
          text-transform: uppercase;
          letter-spacing: 0.09em;
          color: var(--kb-muted);
        }

        .panel-dirty {
          font-size: 0.63rem;
          color: var(--kb-accent);
          opacity: 0.8;
        }

        .panel-body {
          flex: 1;
          overflow-y: auto;
          padding: 10px;
          display: flex;
          flex-direction: column;
          gap: 10px;
        }

        .panel-field { display: flex; flex-direction: column; gap: 0; }
        .panel-field--grow { flex: 1; min-height: 0; }
        .panel-field--grow textarea { flex: 1; min-height: 80px; resize: none; }

        .panel-title-input {
          background: transparent;
          border: 1px solid transparent;
          border-radius: 4px;
          color: var(--kb-fg);
          font-size: 0.88rem;
          font-weight: 500;
          line-height: 1.4;
          padding: 4px 5px;
          resize: none;
          width: 100%;
          outline: none;
        }
        .panel-title-input:focus {
          border-color: var(--kb-border-focus);
          background: var(--kb-input-bg);
        }
        .panel-title-input::placeholder { color: var(--kb-muted); }

        .panel-row { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }

        .tag-preview { display: flex; gap: 3px; flex-wrap: wrap; margin-top: 5px; }

        .panel-meta {
          display: flex;
          flex-direction: column;
          gap: 3px;
          padding-top: 6px;
          border-top: 1px solid color-mix(in srgb, var(--kb-border) 40%, transparent);
        }

        .panel-meta span { font-size: 0.63rem; color: color-mix(in srgb, var(--kb-muted) 70%, transparent); }
        .panel-meta-id { font-variant-numeric: tabular-nums; }

        .panel-footer {
          display: flex;
          gap: 6px;
          align-items: center;
          padding: 8px 10px;
          border-top: 1px solid color-mix(in srgb, var(--kb-border) 50%, transparent);
          flex-shrink: 0;
        }

        .spacer { flex: 1; }
      `}</style>

      <div className="board-header">
        <div>
          <h3 className="board-title">Project Board</h3>
          {githubRepo ? <div className="board-repo">⎇ {githubRepo}</div> : null}
        </div>
        <div className="header-right">
          <span className="task-count">{totalTasks}</span>
          {!addingColumn ? (
            <button className="icon-btn" title="Add column" onClick={() => setAddingColumn(true)}>+</button>
          ) : null}
          <button
            className="icon-btn"
            title="Refresh board"
            onClick={() => { setState('loading'); vscode.postMessage({ type: 'refresh' }); }}
          >↻</button>
        </div>
      </div>

      <div className="kanban-body">
        <div className="board-scroll">
          <div className="board-columns">
            {board.columns.map((column, idx) => {
              const allTasks = tasksForColumn(column, board);
              const isDoneCol = column.id === DONE_COLUMN_ID;
              const isProtected = column.id === DONE_COLUMN_ID || column.id === 'backlog';
              const cutoff = isDoneCol && doneDays > 0
                ? new Date(Date.now() - doneDays * 86_400_000)
                : null;
              const tasks = cutoff
                ? allTasks.filter(t => { const d = taskDoneDate(t); return d === null || d >= cutoff; })
                : allTasks;
              const hiddenCount = allTasks.length - tasks.length;
              const accent = COLUMN_ACCENTS[idx % COLUMN_ACCENTS.length];

              return (
                <section
                  key={column.id}
                  className={`board-column${dragOverColumn === column.id ? ' drag-over' : ''}`}
                  style={{ '--col-accent': accent } as React.CSSProperties}
                  onDragOver={e => { e.preventDefault(); setDragOverColumn(column.id); }}
                  onDragLeave={e => { if (!e.currentTarget.contains(e.relatedTarget as Node)) setDragOverColumn(null); }}
                  onDrop={e => onDropTask(e, column.id)}
                >
                  <div className="column-head">
                    {renamingColumn === column.id ? (
                      <input
                        ref={renameInputRef}
                        className="column-rename-input"
                        value={renameValue}
                        onChange={e => setRenameValue(e.target.value)}
                        onKeyDown={e => {
                          if (e.key === 'Enter') submitRename();
                          if (e.key === 'Escape') setRenamingColumn(null);
                        }}
                        onBlur={submitRename}
                      />
                    ) : (
                      <h4
                        className={`column-title${isProtected ? '' : ' column-title--editable'}`}
                        title={isProtected ? undefined : 'Double-click to rename'}
                        onDoubleClick={() => { if (!isProtected) startRename(column); }}
                      >
                        {column.title}
                      </h4>
                    )}
                    <div className="column-head-right">
                      {isDoneCol ? (
                        <button
                          className="done-window-btn"
                          title="Cycle time window"
                          onClick={() => {
                            const i = DONE_DAY_PRESETS.indexOf(doneDays);
                            setDoneDays(DONE_DAY_PRESETS[(i + 1) % DONE_DAY_PRESETS.length]);
                          }}
                        >
                          {doneDays === 0 ? 'all' : `${doneDays}d`}
                        </button>
                      ) : null}
                      <span className="column-count">{tasks.length}</span>
                      {!isProtected ? (
                        <button
                          className="col-delete-btn"
                          title={tasks.length > 0 ? 'Move tasks before deleting' : 'Delete column'}
                          disabled={tasks.length > 0}
                          onClick={() => submitDeleteColumn(column.id)}
                        >✕</button>
                      ) : null}
                    </div>
                  </div>

                  <div className="column-cards">
                    {tasks.length === 0 && dragOverColumn !== column.id
                      ? <div className="empty-column">Drop tasks here</div>
                      : null}
                    {tasks.map(task => (
                      <TaskCard
                        key={task.id}
                        task={task}
                        isSelected={selected?.id === task.id}
                        onSelect={selectTask}
                        isDragging={draggingTaskId === task.id}
                        onDragStart={setDraggingTaskId}
                        onDragEnd={() => setDraggingTaskId(null)}
                      />
                    ))}
                  </div>

                  {hiddenCount > 0 ? (
                    <button
                      className="done-hidden-btn"
                      onClick={() => {
                        const i = DONE_DAY_PRESETS.indexOf(doneDays);
                        setDoneDays(DONE_DAY_PRESETS[(i + 1) % DONE_DAY_PRESETS.length]);
                      }}
                    >
                      {hiddenCount} older · show more
                    </button>
                  ) : null}

                  {creatingIn === column.id ? (
                    <div className="task-create">
                      <input
                        ref={createInputRef}
                        value={newTitle}
                        placeholder="Task title…"
                        onChange={e => setNewTitle(e.target.value)}
                        onKeyDown={e => {
                          if (e.key === 'Enter') submitNewTask();
                          if (e.key === 'Escape') setCreatingIn(null);
                        }}
                      />
                      <div className="task-create-actions">
                        <button onClick={submitNewTask}>Add</button>
                        <button className="secondary" onClick={() => setCreatingIn(null)}>Cancel</button>
                      </div>
                    </div>
                  ) : (
                    <button
                      className="add-task-btn"
                      onClick={() => { setCreatingIn(column.id); setNewTitle(''); }}
                    >
                      + Add task
                    </button>
                  )}
                </section>
              );
            })}
            {addingColumn ? (
              <div className="board-column add-column-card">
                <input
                  ref={newColumnInputRef}
                  className="add-column-input"
                  value={newColumnTitle}
                  placeholder="Column name…"
                  onChange={e => setNewColumnTitle(e.target.value)}
                  onKeyDown={e => {
                    if (e.key === 'Enter') submitAddColumn();
                    if (e.key === 'Escape') { setAddingColumn(false); setNewColumnTitle(''); }
                  }}
                />
                <div className="task-create-actions">
                  <button onClick={submitAddColumn}>Add</button>
                  <button className="secondary" onClick={() => { setAddingColumn(false); setNewColumnTitle(''); }}>Cancel</button>
                </div>
              </div>
            ) : null}
          </div>
        </div>

        {selected ? (
          <TaskPanel
            task={selected}
            columns={board.columns}
            onSave={saveTask}
            onDelete={deleteTask}
            onClose={() => setSelected(null)}
          />
        ) : null}
      </div>
    </div>
  );
}

const root = document.getElementById('root');
if (root) {
  createRoot(root).render(<App />);
}
