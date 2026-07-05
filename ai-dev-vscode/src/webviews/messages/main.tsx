import React, { useState, useEffect } from 'react';
import { createRoot } from 'react-dom/client';
import { getVsCodeApi } from '../shared/vscodeApi';
import type { ToMessagesWebview } from '../shared/protocol';
import type { MessageItem } from '../../types';

const vscode = getVsCodeApi();

function priorityColor(priority: string): string {
  switch (priority.toLowerCase()) {
    case 'urgent':   return 'var(--vscode-errorForeground)';
    case 'high':     return 'var(--vscode-notificationsWarningIcon-foreground)';
    default:         return 'var(--vscode-foreground)';
  }
}

function MessageRow({ message }: { message: MessageItem }) {
  function process() {
    vscode.postMessage({ type: 'process', fileName: message.fileName, agentSlug: message.agentSlug ?? message.from });
  }

  return (
    <div style={{ padding: '6px 0', borderBottom: '1px solid var(--vscode-widget-border)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between' }}>
        <span style={{ fontWeight: 'bold', fontSize: '0.9em' }}>{message.re}</span>
        <span style={{ color: priorityColor(message.priority), fontSize: '0.75em' }}>
          {message.priority}
        </span>
      </div>
      <div className="muted" style={{ margin: '2px 0' }}>from: {message.from}</div>
      <button className="secondary" onClick={process} style={{ marginTop: 4 }}>
        Mark processed
      </button>
    </div>
  );
}

function App() {
  const [state, setState] = useState<'loading' | 'error' | 'ready'>('loading');
  const [messages, setMessages] = useState<MessageItem[]>([]);
  const [errorMsg, setErrorMsg] = useState('');

  useEffect(() => {
    const handler = (event: MessageEvent<ToMessagesWebview>) => {
      const msg = event.data;
      if (msg.type === 'loading') { setState('loading'); }
      else if (msg.type === 'error') { setState('error'); setErrorMsg(msg.message); }
      else if (msg.type === 'messages') { setMessages(msg.data); setState('ready'); }
    };
    window.addEventListener('message', handler);
    vscode.postMessage({ type: 'ready' });
    return () => window.removeEventListener('message', handler);
  }, []);

  if (state === 'loading') return <p className="muted">Loading messages…</p>;
  if (state === 'error') return <p className="error">{errorMsg}</p>;
  if (messages.length === 0) return <p className="muted">No unprocessed messages.</p>;
  return <div>{messages.map(m => <MessageRow key={m.fileName} message={m} />)}</div>;
}

const root = document.getElementById('root');
if (root) createRoot(root).render(<App />);
