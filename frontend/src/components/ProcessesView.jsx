function StatusBadge({ status }) {
  const normalized = String(status || "Stopped").toLowerCase();
  return <span className={`status-badge status-${normalized}`}>{status}</span>;
}

export default function ProcessesView({
  processes,
  onRefresh,
  onCreate,
  onEdit,
  onAction,
  onDelete,
  onOpenLogs,
  onOpenPerformance
}) {
  return (
    <section className="view">
      <div className="view-header">
        <h3>Processes</h3>
        <div className="row-actions">
          <button className="ghost" onClick={onRefresh}>Refresh</button>
          <button className="primary" onClick={() => onCreate()}>New Command</button>
        </div>
      </div>

      <div className="card-grid">
        {processes.map((process) => (
          <article key={process.id} className="process-card">
            <div className="process-head">
              <h4>{process.name}</h4>
              <StatusBadge status={process.status} />
            </div>

            <p className="mono command-line">{process.command}</p>
            <p className="muted">{process.workingDirectory || "No working directory"}</p>

            <div className="mini-grid">
              <div><span>CPU</span><strong>{process.cpuPercent ?? "-"}%</strong></div>
              <div><span>PID</span><strong>{process.pid ?? "-"}</strong></div>
              <div><span>Threads</span><strong>{process.threadCount ?? "-"}</strong></div>
              <div><span>Handles</span><strong>{process.handleCount ?? "-"}</strong></div>
            </div>

            <div className="row-actions wrap">
              <button className="good" onClick={() => onAction(process.id, "start")}>Start</button>
              <button className="bad" onClick={() => onAction(process.id, "stop")}>Stop</button>
              <button className="ghost" onClick={() => onAction(process.id, "restart")}>Restart</button>
              <button className="ghost" onClick={() => onAction(process.id, "ctrlc")}>Ctrl+C</button>
              <button className="ghost" onClick={() => onOpenLogs(process.id)}>Logs</button>
              <button className="ghost" onClick={() => onOpenPerformance(process.id)}>Perf</button>
              <button className="ghost" onClick={() => onEdit(process)}>Edit</button>
              <button className="bad" onClick={() => onDelete(process.id)}>Delete</button>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
