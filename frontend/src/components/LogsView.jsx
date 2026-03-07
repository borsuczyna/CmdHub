export default function LogsView({
  processes,
  selectedId,
  logs,
  tail,
  autoRefresh,
  onSelect,
  onTailChange,
  onAutoRefreshChange,
  onRefresh,
  onClear
}) {
  const selected = processes.find((p) => p.id === selectedId) || null;

  return (
    <div className="content-view">
      <div className="content-header">
        <div>
          <h1 className="page-title">Logs</h1>
          <p className="page-subtitle">View console output from your processes</p>
        </div>
        <div className="header-actions">
          <input
            className="form-input"
            style={{ width: 100 }}
            type="number"
            min="100"
            max="200000"
            value={tail}
            onChange={(e) => onTailChange(Number(e.target.value || 16000))}
          />
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={autoRefresh}
              onChange={(e) => onAutoRefreshChange(e.target.checked)}
            />
            Auto refresh
          </label>
          <button className="btn btn-ghost" onClick={onRefresh}>Refresh</button>
          <button className="btn btn-danger" onClick={onClear} disabled={!selectedId}>Clear</button>
        </div>
      </div>

      <div className="logs-layout">
        <aside className="process-sidebar">
          {processes.map((p) => (
            <button
              key={p.id}
              className={`process-sidebar-item ${selectedId === p.id ? "active" : ""}`}
              onClick={() => onSelect(p.id)}
            >
              <strong>{p.name}</strong>
              <span>{p.status}</span>
            </button>
          ))}
        </aside>

        <div className="log-panel">
          <div className="log-panel-header">
            <h4>{selected ? selected.name : "No process selected"}</h4>
            <p>{selected ? selected.command : "Choose a process from the sidebar."}</p>
          </div>
          <pre className="log-body">{logs || "No logs yet."}</pre>
        </div>
      </div>
    </div>
  );
}
