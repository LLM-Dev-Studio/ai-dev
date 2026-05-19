import React, { useEffect, useMemo, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { getVsCodeApi } from '../shared/vscodeApi';
import type { BoardColumnItem, BoardData, BoardTaskItem } from '../../types';
import type { EditableBoardTask, ToKanbanWebview } from '../shared/protocol';

const vscode = getVsCodeApi();

const PRIORITY_META: Record<string, { label: string; color: string }> = {
  critical: { label: 'Critical', color: '#e11d48' },
  high:     { label: 'High',     color: '#ea580c' },
  normal:   { label: 'Normal',   color: '#ca8a04' },
  low:      { label: 'Low',      color: '#16a34a' },
};

const COLUMN_ACCENTS = ['#6366f1', '#f59e0b', '#8b5cf6', '#10b981', '#ef4444', '#3b82f6'];

const DONE_COLUMN_ID = 'done';
const DONE_DAY_PRESETS = [7, 14, 30, 0]; // 0 = all

function taskDate(task: BoardTaskItem): Date | null {
  const raw = task.completedAt ?? task.movedAt;
  if (!raw) return null;
  const d = new Date(raw);
  return isNaN(d.getTime()) ? null : d;
}

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

function PriorityChip({ priority }: { priority: string }) {
  const meta = PRIORITY_META[priority] ?? PRIORITY_META['normal'];
  return (
    <span
      className="priority-chip"
      style={{ borderColor: `${meta.color}55`, color: meta.color, background: `${meta.color}1a` }}
    >
      {meta.label}
    </span>
  );
}

function TaskCard({
  task, onEdit, isDragging, onDragStart, onDragEnd,
}: {
  task: BoardTaskItem;
  onEdit: (t: BoardTaskItem) => void;
  isDragging: boolean;
  onDragStart: (id: string) => void;
  onDragEnd: () => void;
}): React.JSX.Element {
  return (
    <div
      className={`task-card${isDragging ? ' is-dragging' : ''}`}
      draggable
      onDragStart={event => {
        event.dataTransfer.setData('text/task-id', task.id);
        event.dataTransfer.effectAllowed = 'move';
        onDragStart(task.id);
      }}
      onDragEnd={onDragEnd}
      onClick={() => onEdit(task)}
    >
      <div className="task-title-row">
        <span className="task-title">{task.title}</span>
        <PriorityChip priority={task.priority ?? 'normal'} />
      </div>
      {task.description
        ? <p className="task-description">{task.description}</p>
        : null}
      <div className="task-footer">
        {task.assignee
          ? <span className="assignee-avatar" title={task.assignee}>{getInitials(task.assignee) || '?'}</span>
          : <span className="no-assignee">—</span>}
        {task.tags && task.tags.length > 0
          ? (
            <div className="tag-list">
              {task.tags.slice(0, 3).map(tag => <span key={tag} className="tag-pill">{tag}</span>)}
              {task.tags.length > 3
                ? <span className="tag-pill">+{task.tags.length - 3}</span>
                : null}
            </div>
          )
          : null}
      </div>
    </div>
  );
}

function App(): React.JSX.Element {
  const [state, setState] = useState<'loading' | 'ready' | 'error' | 'github-sign-in-required'>('loading');
  const [error, setError] = useState('');
  const [githubRepo, setGithubRepo] = useState('');
  const [board, setBoard] = useState<BoardData>({ columns: [], tasks: {} });
  const [creatingIn, setCreatingIn] = useState<string | null>(null);
  const [newTitle, setNewTitle] = useState('');
  const [dragOverColumn, setDragOverColumn] = useState<string | null>(null);
  const [draggingTaskId, setDraggingTaskId] = useState<string | null>(null);
  const [doneDays, setDoneDays] = useState(7);

  const [editing, setEditing] = useState<EditableBoardTask | null>(null);
  const [editColumnId, setEditColumnId] = useState('');
  const [editTitle, setEditTitle] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editAssignee, setEditAssignee] = useState('');
  const [editPriority, setEditPriority] = useState('normal');
  const [editTags, setEditTags] = useState('');

  const createInputRef = useRef<HTMLInputElement>(null);
  const totalTasks = useMemo(() => Object.keys(board.tasks).length, [board.tasks]);

  useEffect(() => {
    const handler = (event: MessageEvent<ToKanbanWebview>) => {
      const msg = event.data;
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

  const openEditor = (task: BoardTaskItem): void => {
    const col = board.columns.find(c => c.taskIds.includes(task.id));
    setEditing(task);
    setEditColumnId(col?.id ?? '');
    setEditTitle(task.title);
    setEditDescription(task.description ?? '');
    setEditAssignee(task.assignee ?? '');
    setEditPriority(task.priority ?? 'normal');
    setEditTags((task.tags ?? []).join(', '));
  };

  const submitNewTask = (): void => {
    if (!newTitle.trim() || !creatingIn) return;
    vscode.postMessage({ type: 'createTask', columnId: creatingIn, title: newTitle.trim(), priority: 'normal', tags: [] });
    setNewTitle('');
    setCreatingIn(null);
  };

  const submitTaskUpdate = (): void => {
    if (!editing || !editTitle.trim() || !editColumnId) return;
    vscode.postMessage({
      type: 'updateTask',
      taskId: editing.id,
      columnId: editColumnId,
      title: editTitle.trim(),
      description: editDescription,
      assignee: editAssignee,
      priority: editPriority,
      tags: normalizeTags(editTags),
    });
    setEditing(null);
  };

  const deleteTask = (): void => {
    if (!editing) return;
    vscode.postMessage({ type: 'deleteTask', taskId: editing.id });
    setEditing(null);
  };

  const onDropTask = (event: React.DragEvent<HTMLElement>, toColumnId: string): void => {
    event.preventDefault();
    const taskId = event.dataTransfer.getData('text/task-id');
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

  return (
    <div className="kanban-shell">
      <style>{`
        *, *::before, *::after { box-sizing: border-box; }

        :root {
          --kb-bg:           var(--vscode-editor-background);
          --kb-surface:      var(--vscode-sideBar-background);
          --kb-panel:        var(--vscode-editorWidget-background);
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
          gap: 10px;
          padding: 10px;
          min-height: 100vh;
        }

        /* ─── Header ─── */
        .board-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          gap: 8px;
          padding-bottom: 8px;
          border-bottom: 1px solid color-mix(in srgb, var(--kb-border) 50%, transparent);
        }

        .board-title {
          margin: 0;
          font-size: 0.75rem;
          font-weight: 600;
          text-transform: uppercase;
          letter-spacing: 0.09em;
          opacity: 0.65;
        }

        .board-repo {
          font-size: 0.68rem;
          color: var(--kb-muted);
          margin-top: 2px;
        }

        .header-right { display: flex; align-items: center; gap: 5px; }

        .task-count {
          border-radius: 999px;
          padding: 1px 8px;
          font-size: 0.68rem;
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

        /* ─── Columns ─── */
        .board-columns {
          display: grid;
          grid-auto-flow: column;
          grid-auto-columns: minmax(200px, 1fr);
          gap: 8px;
          overflow-x: auto;
          padding-bottom: 4px;
          align-items: start;
        }

        .board-column {
          border: 1px solid color-mix(in srgb, var(--kb-border) 65%, transparent);
          border-top: 2px solid var(--col-accent, var(--kb-border));
          border-radius: 7px;
          padding: 8px;
          background: color-mix(in srgb, var(--kb-surface) 55%, var(--kb-bg) 45%);
          display: flex;
          flex-direction: column;
          gap: 6px;
          transition: background 140ms, border-color 140ms;
        }

        .board-column.drag-over {
          background: color-mix(in srgb, var(--kb-border-focus) 8%, var(--kb-bg) 92%);
          border-color: var(--kb-border-focus);
        }

        .column-head {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding-bottom: 4px;
        }

        .column-title {
          margin: 0;
          font-size: 0.7rem;
          font-weight: 600;
          text-transform: uppercase;
          letter-spacing: 0.08em;
          color: var(--kb-muted);
        }

        .column-count {
          min-width: 18px;
          height: 17px;
          display: inline-flex;
          align-items: center;
          justify-content: center;
          padding: 0 5px;
          border-radius: 999px;
          font-size: 0.62rem;
          font-variant-numeric: tabular-nums;
          background: color-mix(in srgb, var(--kb-badge-bg) 65%, transparent);
          color: var(--kb-badge-fg);
        }

        .column-cards {
          display: flex;
          flex-direction: column;
          gap: 5px;
          min-height: 32px;
        }

        /* ─── Cards ─── */
        .task-card {
          border: 1px solid color-mix(in srgb, var(--kb-border) 55%, transparent);
          border-radius: 5px;
          background: var(--kb-bg);
          padding: 8px 9px;
          display: flex;
          flex-direction: column;
          gap: 5px;
          cursor: pointer;
          user-select: none;
          transition: transform 120ms ease, border-color 120ms ease,
                      box-shadow 120ms ease, opacity 120ms ease;
        }

        .task-card:hover {
          transform: translateY(-1px);
          border-color: color-mix(in srgb, var(--kb-border-focus) 55%, transparent);
          box-shadow: 0 2px 6px color-mix(in srgb, black 20%, transparent);
        }

        .task-card.is-dragging { opacity: 0.35; transform: scale(0.97); }

        .task-title-row {
          display: flex;
          justify-content: space-between;
          align-items: flex-start;
          gap: 6px;
        }

        .task-title {
          font-size: 0.8rem;
          font-weight: 500;
          line-height: 1.35;
          flex: 1;
          min-width: 0;
          word-break: break-word;
        }

        .priority-chip {
          flex-shrink: 0;
          border: 1px solid;
          border-radius: 999px;
          padding: 1px 6px;
          font-size: 0.58rem;
          font-weight: 700;
          text-transform: uppercase;
          letter-spacing: 0.06em;
          white-space: nowrap;
        }

        .task-description {
          margin: 0;
          color: var(--kb-muted);
          font-size: 0.73rem;
          line-height: 1.4;
          display: -webkit-box;
          -webkit-line-clamp: 2;
          -webkit-box-orient: vertical;
          overflow: hidden;
        }

        .task-footer {
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: 5px;
        }

        .assignee-avatar {
          display: inline-flex;
          align-items: center;
          justify-content: center;
          width: 19px;
          height: 19px;
          border-radius: 999px;
          flex-shrink: 0;
          font-size: 0.57rem;
          font-weight: 700;
          background: color-mix(in srgb, var(--kb-accent) 22%, transparent);
          color: var(--kb-accent);
          border: 1px solid color-mix(in srgb, var(--kb-accent) 35%, transparent);
        }

        .no-assignee { font-size: 0.65rem; color: color-mix(in srgb, var(--kb-muted) 50%, transparent); }

        .tag-list { display: flex; gap: 3px; overflow: hidden; flex-wrap: nowrap; }

        .tag-pill {
          border-radius: 3px;
          padding: 1px 5px;
          font-size: 0.58rem;
          background: color-mix(in srgb, var(--kb-border) 55%, transparent);
          color: var(--kb-muted);
          white-space: nowrap;
        }

        /* ─── Add task ─── */
        .add-task-btn {
          display: flex;
          align-items: center;
          gap: 5px;
          width: 100%;
          background: transparent;
          border: 1px dashed color-mix(in srgb, var(--kb-border) 60%, transparent);
          border-radius: 5px;
          color: color-mix(in srgb, var(--kb-muted) 80%, transparent);
          font-family: var(--vscode-font-family);
          font-size: 0.73rem;
          padding: 5px 8px;
          cursor: pointer;
          text-align: left;
          transition: color 120ms, border-color 120ms, background 120ms;
        }
        .add-task-btn:hover {
          color: var(--kb-fg);
          border-color: var(--kb-border-focus);
          background: color-mix(in srgb, var(--kb-border) 12%, transparent);
        }

        .task-create {
          display: flex;
          flex-direction: column;
          gap: 5px;
          border: 1px solid var(--kb-border-focus);
          border-radius: 5px;
          padding: 7px 8px;
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

        button.primary, button:not(.secondary):not(.icon-btn):not(.add-task-btn):not(.danger) {
          background: var(--kb-btn-bg);
          color: var(--kb-btn-fg);
        }
        button.primary:hover,
        button:not(.secondary):not(.icon-btn):not(.add-task-btn):not(.danger):hover {
          background: var(--kb-btn-hover);
        }

        button.secondary {
          background: var(--kb-btn2-bg);
          color: var(--kb-btn2-fg);
        }
        button.secondary:hover { background: var(--kb-btn2-hover); }

        button.danger {
          background: color-mix(in srgb, #dc2626 18%, transparent);
          color: #f87171;
          border: 1px solid color-mix(in srgb, #dc2626 35%, transparent);
        }
        button.danger:hover { background: color-mix(in srgb, #dc2626 28%, transparent); }

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
          font-size: 0.67rem;
          color: var(--kb-muted);
          text-transform: uppercase;
          letter-spacing: 0.06em;
          margin-bottom: 4px;
        }

        .field { display: flex; flex-direction: column; }

        /* ─── Edit modal ─── */
        .modal-backdrop {
          position: fixed;
          inset: 0;
          background: color-mix(in srgb, black 55%, transparent);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 100;
          padding: 16px;
        }

        .modal {
          background: var(--kb-surface, var(--kb-bg));
          border: 1px solid var(--kb-border);
          border-radius: 9px;
          padding: 16px;
          width: 100%;
          max-width: min(460px, calc(100vw - 32px));
          display: flex;
          flex-direction: column;
          gap: 12px;
          box-shadow: 0 12px 40px color-mix(in srgb, black 45%, transparent);
        }

        .modal-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
        }

        .modal-title {
          margin: 0;
          font-size: 0.72rem;
          font-weight: 600;
          text-transform: uppercase;
          letter-spacing: 0.08em;
          color: var(--kb-muted);
        }

        .modal-grid {
          display: grid;
          grid-template-columns: 1fr 1fr;
          gap: 10px;
        }

        .modal-grid .full { grid-column: 1 / -1; }

        .modal-actions {
          display: flex;
          gap: 6px;
          align-items: center;
          border-top: 1px solid color-mix(in srgb, var(--kb-border) 55%, transparent);
          padding-top: 10px;
        }

        .spacer { flex: 1; }

        /* ─── Done column window control ─── */
        .done-window-btn {
          background: color-mix(in srgb, var(--kb-badge-bg) 50%, transparent);
          color: var(--kb-muted);
          border: 1px solid color-mix(in srgb, var(--kb-border) 55%, transparent);
          border-radius: 999px;
          padding: 1px 6px;
          font-size: 0.6rem;
          font-variant-numeric: tabular-nums;
          cursor: pointer;
          transition: color 120ms, border-color 120ms, background 120ms;
        }
        .done-window-btn:hover {
          color: var(--kb-fg);
          border-color: var(--kb-border-focus);
          background: color-mix(in srgb, var(--kb-border) 30%, transparent);
        }

        .done-hidden-btn {
          background: transparent;
          border: 1px dashed color-mix(in srgb, var(--kb-border) 45%, transparent);
          border-radius: 4px;
          color: color-mix(in srgb, var(--kb-muted) 70%, transparent);
          font-family: var(--vscode-font-family);
          font-size: 0.65rem;
          padding: 4px 8px;
          cursor: pointer;
          width: 100%;
          text-align: center;
          transition: color 120ms, border-color 120ms;
        }
        .done-hidden-btn:hover {
          color: var(--kb-muted);
          border-color: var(--kb-border);
        }

        /* ─── Empty state ─── */
        .empty-column {
          font-size: 0.7rem;
          color: color-mix(in srgb, var(--kb-muted) 55%, transparent);
          text-align: center;
          padding: 10px 4px;
          border: 1px dashed color-mix(in srgb, var(--kb-border) 35%, transparent);
          border-radius: 4px;
        }

        @media (max-width: 560px) {
          .modal-grid { grid-template-columns: 1fr; }
        }
      `}</style>

      <div className="board-header">
        <div>
          <h3 className="board-title">Project Board</h3>
          {githubRepo ? <div className="board-repo">⎇ {githubRepo}</div> : null}
        </div>
        <div className="header-right">
          <span className="task-count">{totalTasks}</span>
          <button
            className="icon-btn"
            title="Refresh board"
            onClick={() => { setState('loading'); vscode.postMessage({ type: 'refresh' }); }}
          >↻</button>
        </div>
      </div>

      <div className="board-columns">
        {board.columns.map((column, idx) => {
          const allTasks = tasksForColumn(column, board);
          const isDoneColumn = column.id === DONE_COLUMN_ID;
          const cutoff = isDoneColumn && doneDays > 0
            ? new Date(Date.now() - doneDays * 86_400_000)
            : null;
          const tasks = cutoff
            ? allTasks.filter(t => { const d = taskDate(t); return d === null || d >= cutoff; })
            : allTasks;
          const hiddenCount = allTasks.length - tasks.length;
          const accent = COLUMN_ACCENTS[idx % COLUMN_ACCENTS.length];
          return (
            <section
              key={column.id}
              className={`board-column${dragOverColumn === column.id ? ' drag-over' : ''}`}
              style={{ '--col-accent': accent } as React.CSSProperties}
              onDragOver={event => { event.preventDefault(); setDragOverColumn(column.id); }}
              onDragLeave={event => {
                if (!event.currentTarget.contains(event.relatedTarget as Node)) {
                  setDragOverColumn(null);
                }
              }}
              onDrop={event => onDropTask(event, column.id)}
            >
              <div className="column-head">
                <h4 className="column-title">{column.title}</h4>
                <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                  {isDoneColumn ? (
                    <button
                      className="done-window-btn"
                      title="Cycle time window"
                      onClick={() => {
                        const next = DONE_DAY_PRESETS[(DONE_DAY_PRESETS.indexOf(doneDays) + 1) % DONE_DAY_PRESETS.length];
                        setDoneDays(Math.max(next, next === 0 ? 0 : 7));
                      }}
                    >
                      {doneDays === 0 ? 'all' : `${doneDays}d`}
                    </button>
                  ) : null}
                  <span className="column-count">{tasks.length}</span>
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
                    onEdit={openEditor}
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
                    const idx2 = DONE_DAY_PRESETS.indexOf(doneDays);
                    setDoneDays(DONE_DAY_PRESETS[(idx2 + 1) % DONE_DAY_PRESETS.length]);
                  }}
                >
                  {hiddenCount} older task{hiddenCount !== 1 ? 's' : ''} hidden · show more
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
      </div>

      {editing ? (
        <div className="modal-backdrop" onClick={e => { if (e.target === e.currentTarget) setEditing(null); }}>
          <div className="modal">
            <div className="modal-header">
              <h4 className="modal-title">Edit Task</h4>
              <button className="icon-btn" onClick={() => setEditing(null)}>✕</button>
            </div>

            <div className="modal-grid">
              <div className="field full">
                <label>Title</label>
                <input
                  value={editTitle}
                  onChange={e => setEditTitle(e.target.value)}
                  placeholder="Task title"
                  autoFocus
                  onKeyDown={e => { if (e.key === 'Enter') submitTaskUpdate(); }}
                />
              </div>

              <div className="field">
                <label>Column</label>
                <select value={editColumnId} onChange={e => setEditColumnId(e.target.value)}>
                  <option value="" disabled>Select column</option>
                  {board.columns.map(col => (
                    <option key={col.id} value={col.id}>{col.title}</option>
                  ))}
                </select>
              </div>

              <div className="field">
                <label>Priority</label>
                <select value={editPriority} onChange={e => setEditPriority(e.target.value)}>
                  <option value="critical">Critical</option>
                  <option value="high">High</option>
                  <option value="normal">Normal</option>
                  <option value="low">Low</option>
                </select>
              </div>

              <div className="field">
                <label>Assignee</label>
                <input value={editAssignee} onChange={e => setEditAssignee(e.target.value)} placeholder="Name" />
              </div>

              <div className="field">
                <label>Tags</label>
                <input value={editTags} onChange={e => setEditTags(e.target.value)} placeholder="tag1, tag2" />
              </div>

              <div className="field full">
                <label>Description</label>
                <textarea
                  rows={4}
                  value={editDescription}
                  onChange={e => setEditDescription(e.target.value)}
                  placeholder="Description…"
                />
              </div>
            </div>

            <div className="modal-actions">
              <button onClick={submitTaskUpdate}>Save</button>
              <button className="secondary" onClick={() => setEditing(null)}>Cancel</button>
              <div className="spacer" />
              <button className="danger" onClick={deleteTask}>Delete</button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

const root = document.getElementById('root');
if (root) {
  createRoot(root).render(<App />);
}
