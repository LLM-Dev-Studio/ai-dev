import React, { useEffect, useMemo, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { getVsCodeApi } from '../shared/vscodeApi';
import type { BoardColumnItem, BoardData, BoardTaskItem } from '../../types';
import type { EditableBoardTask, ToKanbanWebview } from '../shared/protocol';

const vscode = getVsCodeApi();
const BACKLOG_COLUMN_ID = 'backlog';

function normalizeTags(input: string): string[] {
  const raw = input.split(',').map(item => item.trim()).filter(Boolean);
  return Array.from(new Set(raw.map(tag => tag.toLowerCase())));
}

function tasksForColumn(column: BoardColumnItem, board: BoardData): BoardTaskItem[] {
  return column.taskIds
    .map(id => board.tasks[id])
    .filter((task): task is BoardTaskItem => task !== undefined);
}

function TaskCard(
  { task, onEdit }: { task: BoardTaskItem; onEdit: (task: BoardTaskItem) => void },
): React.JSX.Element {
  return (
    <div
      className="task-card"
      draggable
      onDragStart={event => {
        event.dataTransfer.setData('text/task-id', task.id);
        event.dataTransfer.effectAllowed = 'move';
      }}
      onClick={() => onEdit(task)}
    >
      <div className="task-title-row">
        <strong>{task.title}</strong>
        <span className="priority-chip">{task.priority ?? 'normal'}</span>
      </div>
      {task.description ? <p className="task-description">{task.description}</p> : null}
      <div className="task-footer">
        <span>{task.assignee || 'Unassigned'}</span>
        <span>{task.tags && task.tags.length > 0 ? task.tags.join(', ') : 'No tags'}</span>
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

  const [editing, setEditing] = useState<EditableBoardTask | null>(null);
  const [editColumnId, setEditColumnId] = useState('');
  const [editTitle, setEditTitle] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editAssignee, setEditAssignee] = useState('');
  const [editPriority, setEditPriority] = useState('normal');
  const [editTags, setEditTags] = useState('');

  const totalTasks = useMemo(() => Object.keys(board.tasks).length, [board.tasks]);

  useEffect(() => {
    const handler = (event: MessageEvent<ToKanbanWebview>) => {
      const message = event.data;
      if (message.type === 'loading') {
        setState('loading');
        return;
      }

      if (message.type === 'error') {
        setError(message.message);
        setState('error');
        return;
      }

      if (message.type === 'github-sign-in-required') {
        setGithubRepo(`${message.owner}/${message.repo}`);
        setState('github-sign-in-required');
        return;
      }

      setBoard(message.data);
      if (message.githubRepo) {
        setGithubRepo(message.githubRepo);
      }
      setState('ready');
    };

    window.addEventListener('message', handler);
    vscode.postMessage({ type: 'ready' });
    return () => window.removeEventListener('message', handler);
  }, []);

  const openEditor = (task: BoardTaskItem): void => {
    const containingColumn = board.columns.find(col => col.taskIds.includes(task.id));
    setEditing(task);
    setEditColumnId(containingColumn?.id ?? '');
    setEditTitle(task.title);
    setEditDescription(task.description ?? '');
    setEditAssignee(task.assignee ?? '');
    setEditPriority(task.priority ?? 'normal');
    setEditTags((task.tags ?? []).join(', '));
  };

  const submitNewTask = (): void => {
    if (!newTitle.trim()) {
      return;
    }

    vscode.postMessage({
      type: 'createTask',
      columnId: BACKLOG_COLUMN_ID,
      title: newTitle.trim(),
      priority: 'normal',
      tags: [],
    });

    setNewTitle('');
    setCreatingIn(null);
  };

  const submitTaskUpdate = (): void => {
    if (!editing || !editTitle.trim() || !editColumnId) {
      return;
    }

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
    if (!editing) {
      return;
    }

    vscode.postMessage({ type: 'deleteTask', taskId: editing.id });
    setEditing(null);
  };

  const onDropTask = (event: React.DragEvent<HTMLDivElement>, toColumnId: string): void => {
    event.preventDefault();
    const taskId = event.dataTransfer.getData('text/task-id');
    if (!taskId) {
      return;
    }
    vscode.postMessage({ type: 'moveTask', taskId, toColumnId });
  };

  if (state === 'loading') {
    return <p className="muted">Loading board...</p>;
  }

  if (state === 'error') {
    return <p className="error">{error}</p>;
  }

  if (state === 'github-sign-in-required') {
    return (
      <div style={{ padding: '12px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
        <p className="muted">
          This project uses GitHub Issues for task management
          {githubRepo ? ` (${githubRepo})` : ''}.
        </p>
        <button onClick={() => vscode.postMessage({ type: 'githubSignIn' })}>
          Sign in to GitHub
        </button>
      </div>
    );
  }

  return (
    <div className="kanban-shell">
      <style>{`
        :root {
          --kb-bg: var(--vscode-editor-background);
          --kb-surface: var(--vscode-sideBar-background);
          --kb-panel: var(--vscode-editorWidget-background);
          --kb-card: var(--vscode-editorWidget-background);
          --kb-border: var(--vscode-widget-border);
          --kb-border-strong: var(--vscode-focusBorder);
          --kb-foreground: var(--vscode-foreground);
          --kb-muted: var(--vscode-descriptionForeground);
          --kb-accent: var(--vscode-textLink-foreground);
          --kb-badge-bg: var(--vscode-badge-background);
          --kb-badge-fg: var(--vscode-badge-foreground);
        }

        .kanban-shell {
          font-family: var(--vscode-font-family);
          display: flex;
          flex-direction: column;
          gap: 12px;
          background: linear-gradient(
            180deg,
            color-mix(in srgb, var(--kb-bg) 96%, var(--kb-surface) 4%),
            color-mix(in srgb, var(--kb-bg) 92%, var(--kb-surface) 8%)
          );
          border: 1px solid var(--kb-border);
          border-radius: 12px;
          padding: 10px;
          min-height: calc(100vh - 24px);
          box-sizing: border-box;
        }

        .board-header {
          display: flex;
          justify-content: space-between;
          align-items: baseline;
          gap: 12px;
        }

        .board-title {
          margin: 0;
          letter-spacing: 0.06em;
          text-transform: uppercase;
          font-size: 0.95rem;
          color: var(--kb-foreground);
        }

        .task-count {
          border: 1px solid color-mix(in srgb, var(--kb-border) 75%, transparent);
          border-radius: 999px;
          padding: 2px 8px;
          font-size: 0.75rem;
          background: var(--kb-badge-bg);
          color: var(--kb-badge-fg);
        }

        .board-columns {
          display: grid;
          grid-auto-flow: column;
          grid-auto-columns: minmax(240px, 1fr);
          gap: 10px;
          overflow-x: auto;
          padding-bottom: 8px;
        }

        .board-column {
          border: 1px solid var(--kb-border);
          border-radius: 10px;
          padding: 8px;
          background: color-mix(in srgb, var(--kb-panel) 92%, var(--kb-bg) 8%);
          min-height: 220px;
          display: flex;
          flex-direction: column;
          gap: 8px;
          backdrop-filter: blur(4px);
        }

        .column-head {
          display: flex;
          justify-content: space-between;
          align-items: center;
          gap: 8px;
        }

        .column-title {
          margin: 0;
          font-size: 0.85rem;
          text-transform: uppercase;
          letter-spacing: 0.05em;
        }

        .column-count {
          color: var(--kb-muted);
          font-size: 0.75rem;
        }

        .task-card {
          border: 1px solid color-mix(in srgb, var(--kb-border) 80%, transparent);
          border-radius: 8px;
          background: color-mix(in srgb, var(--kb-card) 92%, var(--kb-bg) 8%);
          padding: 8px;
          display: flex;
          flex-direction: column;
          gap: 7px;
          cursor: pointer;
          transition: transform 140ms ease, border-color 140ms ease;
        }

        .task-card:hover {
          transform: translateY(-1px);
          border-color: var(--kb-border-strong);
        }

        .task-title-row {
          display: flex;
          justify-content: space-between;
          align-items: flex-start;
          gap: 8px;
        }

        .priority-chip {
          border: 1px solid color-mix(in srgb, var(--kb-accent) 70%, transparent);
          color: var(--kb-accent);
          background: color-mix(in srgb, var(--kb-accent) 14%, transparent);
          border-radius: 999px;
          padding: 1px 8px;
          font-size: 0.7rem;
          text-transform: uppercase;
          letter-spacing: 0.04em;
          white-space: nowrap;
        }

        .task-description {
          margin: 0;
          color: var(--kb-muted);
          font-size: 0.8rem;
          line-height: 1.35;
        }

        .task-footer {
          display: flex;
          justify-content: space-between;
          gap: 8px;
          color: var(--kb-muted);
          font-size: 0.7rem;
          white-space: nowrap;
          overflow: hidden;
        }

        .task-footer > span {
          text-overflow: ellipsis;
          overflow: hidden;
        }

        .task-create {
          display: flex;
          gap: 6px;
        }

        .task-create input {
          width: 100%;
        }

        .editor {
          border-top: 1px solid color-mix(in srgb, var(--kb-border) 70%, transparent);
          margin-top: 4px;
          padding-top: 8px;
          display: grid;
          grid-template-columns: 1fr 1fr;
          gap: 8px;
        }

        .editor h4 {
          margin: 0;
          grid-column: 1 / -1;
          font-size: 0.85rem;
          letter-spacing: 0.05em;
          text-transform: uppercase;
        }

        .editor textarea,
        .editor input,
        .editor select {
          width: 100%;
          box-sizing: border-box;
        }

        .editor .full {
          grid-column: 1 / -1;
        }

        .editor-actions {
          grid-column: 1 / -1;
          display: flex;
          gap: 6px;
        }

        @media (max-width: 720px) {
          .editor {
            grid-template-columns: 1fr;
          }
        }
      `}</style>

      <div className="board-header">
        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
          <h3 className="board-title">Project Board</h3>
          {githubRepo ? (
            <span style={{ fontSize: '0.72rem', color: 'var(--kb-muted)' }}>
              ⎇ {githubRepo}
            </span>
          ) : null}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <span className="task-count">{totalTasks} tasks</span>
          <button
            className="secondary"
            title="Refresh board"
            style={{ padding: '2px 7px', fontSize: '0.85em', lineHeight: 1 }}
            onClick={() => {
              setState('loading');
              vscode.postMessage({ type: 'refresh' });
            }}
          >↻</button>
        </div>
      </div>

      <div className="board-columns">
        {board.columns.map(column => {
          const tasks = tasksForColumn(column, board);
          return (
            <section
              key={column.id}
              className="board-column"
              onDragOver={event => event.preventDefault()}
              onDrop={event => onDropTask(event, column.id)}
            >
              <div className="column-head">
                <h4 className="column-title">{column.title}</h4>
                <span className="column-count">{tasks.length}</span>
              </div>

              {tasks.map(task => <TaskCard key={task.id} task={task} onEdit={openEditor} />)}

              {creatingIn === column.id ? (
                <div className="task-create">
                  <input
                    value={newTitle}
                    placeholder="Task title"
                    onChange={event => setNewTitle(event.target.value)}
                    onKeyDown={event => {
                      if (event.key === 'Enter') {
                        submitNewTask();
                      }
                    }}
                  />
                  <button onClick={() => submitNewTask()}>Add</button>
                </div>
              ) : column.id === BACKLOG_COLUMN_ID ? (
                <button className="secondary" onClick={() => { setCreatingIn(column.id); setNewTitle(''); }}>
                  + Task
                </button>
              ) : null}
            </section>
          );
        })}
      </div>

      {editing ? (
        <section className="editor">
          <h4>Edit Task</h4>

          <input value={editTitle} onChange={event => setEditTitle(event.target.value)} placeholder="Title" />
          <select value={editColumnId} onChange={event => setEditColumnId(event.target.value)}>
            <option value="" disabled>Select column</option>
            {board.columns.map(column => (
              <option key={column.id} value={column.id}>{column.title}</option>
            ))}
          </select>

          <input value={editAssignee} onChange={event => setEditAssignee(event.target.value)} placeholder="Assignee" />
          <input value={editPriority} onChange={event => setEditPriority(event.target.value)} placeholder="Priority" />

          <input
            className="full"
            value={editTags}
            onChange={event => setEditTags(event.target.value)}
            placeholder="Tags (comma separated)"
          />

          <textarea
            className="full"
            rows={4}
            value={editDescription}
            onChange={event => setEditDescription(event.target.value)}
            placeholder="Description"
          />

          <div className="editor-actions">
            <button onClick={submitTaskUpdate}>Save</button>
            <button className="secondary" onClick={() => setEditing(null)}>Cancel</button>
            <button className="secondary" onClick={deleteTask}>Delete</button>
          </div>
        </section>
      ) : null}
    </div>
  );
}

const root = document.getElementById('root');
if (root) {
  createRoot(root).render(<App />);
}
