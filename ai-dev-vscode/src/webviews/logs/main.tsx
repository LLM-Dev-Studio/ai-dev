import React, { useState, useEffect, useRef, useCallback } from 'react';
import { createRoot } from 'react-dom/client';
import { getVsCodeApi } from '../shared/vscodeApi';
import type { ToLogsWebview, LogEntry } from '../shared/protocol';

const vscode = getVsCodeApi();

const LEVEL_COLORS: Record<string, string> = {
  info: 'var(--vscode-foreground)',
  warn: 'var(--vscode-editorWarning-foreground, #cca700)',
  error: 'var(--vscode-errorForeground, #f48771)',
};

function formatTime(iso: string): string {
  try {
    const d = new Date(iso);
    const hh = String(d.getHours()).padStart(2, '0');
    const mm = String(d.getMinutes()).padStart(2, '0');
    const ss = String(d.getSeconds()).padStart(2, '0');
    return `${hh}:${mm}:${ss}`;
  } catch {
    return iso;
  }
}

function LogRow({ entry }: { entry: LogEntry }) {
  const color = LEVEL_COLORS[entry.level] ?? LEVEL_COLORS.info;
  const levelLabel = entry.level.toUpperCase().padEnd(5);
  return (
    <div style={{
      display: 'flex',
      gap: '0.5em',
      padding: '1px 8px',
      lineHeight: '1.5',
      borderBottom: '1px solid var(--vscode-widget-border, transparent)',
      color,
      wordBreak: 'break-word',
    }}>
      <span style={{ flexShrink: 0, color: 'var(--vscode-descriptionForeground)', userSelect: 'none' }}>
        {formatTime(entry.timestamp)}
      </span>
      <span style={{ flexShrink: 0, userSelect: 'none', fontWeight: entry.level !== 'info' ? 'bold' : undefined }}>
        {levelLabel}
      </span>
      <span style={{ flex: 1, whiteSpace: 'pre-wrap' }}>{entry.message}</span>
    </div>
  );
}

function App() {
  const [entries, setEntries] = useState<LogEntry[]>([]);
  const bottomRef = useRef<HTMLDivElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  // Track whether the user has scrolled up — if so, don't auto-scroll.
  const isAtBottom = useRef(true);

  const scrollToBottom = useCallback(() => {
    if (isAtBottom.current) {
      bottomRef.current?.scrollIntoView({ behavior: 'auto' });
    }
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [entries, scrollToBottom]);

  useEffect(() => {
    const handler = (event: MessageEvent<ToLogsWebview>) => {
      const msg = event.data;
      if (msg.type === 'history') {
        setEntries(msg.entries);
        isAtBottom.current = true;
      } else if (msg.type === 'entry') {
        setEntries(prev => [...prev, msg.entry]);
      } else if (msg.type === 'cleared') {
        setEntries([]);
        isAtBottom.current = true;
      }
    };
    window.addEventListener('message', handler);
    return () => window.removeEventListener('message', handler);
  }, []);

  function handleScroll() {
    const el = containerRef.current;
    if (!el) return;
    const threshold = 32;
    isAtBottom.current = el.scrollHeight - el.scrollTop - el.clientHeight < threshold;
  }

  function handleClear() {
    vscode.postMessage({ type: 'clear' });
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100vh', overflow: 'hidden' }}>
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'flex-end',
        padding: '4px 8px',
        borderBottom: '1px solid var(--vscode-widget-border)',
        flexShrink: 0,
      }}>
        <button
          onClick={handleClear}
          title="Clear logs"
          style={{
            background: 'none',
            border: 'none',
            color: 'var(--vscode-descriptionForeground)',
            cursor: 'pointer',
            fontSize: '11px',
            padding: '2px 6px',
          }}
        >
          Clear
        </button>
      </div>
      <div
        ref={containerRef}
        onScroll={handleScroll}
        style={{ flex: 1, overflowY: 'auto', overflowX: 'hidden' }}
      >
        {entries.length === 0 ? (
          <p style={{ color: 'var(--vscode-descriptionForeground)', padding: '8px', margin: 0, fontSize: '0.9em' }}>
            No log entries yet.
          </p>
        ) : (
          entries.map((e, i) => <LogRow key={i} entry={e} />)
        )}
        <div ref={bottomRef} />
      </div>
    </div>
  );
}

const root = document.getElementById('root');
if (root) createRoot(root).render(<App />);
