import React, { useState, useEffect } from 'react';
import { createRoot } from 'react-dom/client';
import { getVsCodeApi } from '../shared/vscodeApi';
import type { ToDecisionsWebview } from '../shared/protocol';
import type { DecisionItem } from '../../types';

const vscode = getVsCodeApi();

function DecisionRow({ decision }: { decision: DecisionItem }) {
  const [resolution, setResolution] = useState('');
  const [busy, setBusy] = useState(false);

  function resolve() {
    if (!resolution.trim()) return;
    setBusy(true);
    vscode.postMessage({ type: 'resolve', decisionId: decision.id, resolution: resolution.trim() });
  }

  return (
    <div style={{ padding: '6px 0', borderBottom: '1px solid var(--vscode-widget-border)' }}>
      <div style={{ fontWeight: 'bold', marginBottom: 2 }}>{decision.subject}</div>
      <div className="muted" style={{ marginBottom: 2 }}>
        from: {decision.from} · {decision.priority}
        {decision.blocks && <> · blocks: {decision.blocks}</>}
      </div>
      {decision.body && (
        <div style={{ fontSize: '0.85em', marginBottom: 6, whiteSpace: 'pre-wrap',
          color: 'var(--vscode-descriptionForeground)' }}>
          {decision.body}
        </div>
      )}
      <div style={{ display: 'flex', gap: 4 }}>
        <input
          value={resolution}
          onChange={e => setResolution(e.target.value)}
          placeholder="Enter resolution…"
          style={{ flex: 1 }}
          onKeyDown={e => { if (e.key === 'Enter') resolve(); }}
          disabled={busy}
        />
        <button onClick={resolve} disabled={busy || !resolution.trim()}>Resolve</button>
      </div>
    </div>
  );
}

function App() {
  const [state, setState] = useState<'loading' | 'error' | 'ready'>('loading');
  const [decisions, setDecisions] = useState<DecisionItem[]>([]);
  const [errorMsg, setErrorMsg] = useState('');

  useEffect(() => {
    const handler = (event: MessageEvent<ToDecisionsWebview>) => {
      const msg = event.data;
      if (msg.type === 'loading') { setState('loading'); }
      else if (msg.type === 'error') { setState('error'); setErrorMsg(msg.message); }
      else if (msg.type === 'decisions') { setDecisions(msg.data); setState('ready'); }
    };
    window.addEventListener('message', handler);
    return () => window.removeEventListener('message', handler);
  }, []);

  if (state === 'loading') return <p className="muted">Loading decisions…</p>;
  if (state === 'error') return <p className="error">{errorMsg}</p>;
  if (decisions.length === 0) return <p className="muted">No pending decisions.</p>;
  return <div>{decisions.map(d => <DecisionRow key={d.id} decision={d} />)}</div>;
}

const root = document.getElementById('root');
if (root) createRoot(root).render(<App />);
