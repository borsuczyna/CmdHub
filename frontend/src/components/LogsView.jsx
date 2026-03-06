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
  const selected = processes.find((process) => process.id === selectedId) || null;

  return (
    <section className="view">
      <div className="view-header">
        <h3>Logs</h3>
        <div className="row-actions">
          <input
            className="tail-input"
            type="number"
            min="100"
            max="200000"
            value={tail}
            onChange={(event) => onTailChange(Number(event.target.value || 16000))}
          />
          <label className="checkbox-line compact">
            <input
              type="checkbox"
              checked={autoRefresh}
              onChange={(event) => onAutoRefreshChange(event.target.checked)}
            />
            Auto refresh
          </label>
          <button className="ghost" onClick={onRefresh}>Refresh</button>
          <button className="bad" onClick={onClear} disabled={!selectedId}>Clear Logs</button>
        </div>
      </div>

      <div className="logs-layout">
        <aside className="process-list">
          {processes.map((process) => (
            <button
              key={process.id}
              className={selectedId === process.id ? "process-list-item active" : "process-list-item"}
              onClick={() => onSelect(process.id)}
            >
              <strong>{process.name}</strong>
              <span>{process.status}</span>
            </button>
          ))}
        </aside>

        <article className="log-panel">
          <div className="log-head">
            <div>
              <h4>{selected ? selected.name : "No process selected"}</h4>
              <p className="muted">{selected ? selected.command : "Choose a process from the left."}</p>
            </div>
          </div>
          <pre className="mono log-body">{logs || "No logs yet."}</pre>
        </article>
      </div>
    </section>
  );
}
