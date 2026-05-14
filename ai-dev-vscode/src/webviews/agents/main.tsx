import React, { useState, useEffect } from 'react';
import { createRoot } from 'react-dom/client';
import { getVsCodeApi } from '../shared/vscodeApi';
import type { ToAgentsWebview } from '../shared/protocol';
import type { AgentSummary } from '../../types';

const vscode = getVsCodeApi();

function AgentRow({ agent }: { agent: AgentSummary }) {
  const [busy, setBusy] = useState(false);

  function send(type: 'run' | 'stop') {
    setBusy(true);
    vscode.postMessage({ type, agentSlug: agent.slug });
  }

  return (
    <div style={{ padding: '6px 0', borderBottom: '1px solid var(--vscode-widget-border)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span style={{ fontWeight: 'bold' }}>{agent.slug}</span>
        <span className="badge" style={{
          background: agent.isRunning
            ? 'var(--vscode-testing-runAction)'
            : 'var(--vscode-badge-background)',
        }}>
          {agent.isRateLimited ? 'rate-limited' : agent.isRunning ? 'running' : 'idle'}
        </span>
      </div>
      <div style={{ marginTop: 4, display: 'flex', gap: 4 }}>
        {agent.isRunning ? (
          <button className="secondary" disabled={busy} onClick={() => send('stop')}>Stop</button>
        ) : (
          <button disabled={busy || agent.isRateLimited} onClick={() => send('run')}>Run</button>
        )}
      </div>
    </div>
  );
}

function App() {
  const [state, setState] = useState<'loading' | 'error' | 'ready'>('loading');
  const [agents, setAgents] = useState<AgentSummary[]>([]);
  const [errorMsg, setErrorMsg] = useState('');

  useEffect(() => {
    const handler = (event: MessageEvent<ToAgentsWebview>) => {
      const msg = event.data;
      if (msg.type === 'loading') { setState('loading'); }
      else if (msg.type === 'error') { setState('error'); setErrorMsg(msg.message); }
      else if (msg.type === 'agents') { setAgents(msg.data); setState('ready'); }
    };
    window.addEventListener('message', handler);
    return () => window.removeEventListener('message', handler);
  }, []);

  if (state === 'loading') return <p className="muted">Loading agents…</p>;
  if (state === 'error') return <p className="error">{errorMsg}</p>;
  if (agents.length === 0) return <p className="muted">No agents found.</p>;
  return <div>{agents.map(a => <AgentRow key={a.slug} agent={a} />)}</div>;
}

const root = document.getElementById('root');
if (root) createRoot(root).render(<App />);
