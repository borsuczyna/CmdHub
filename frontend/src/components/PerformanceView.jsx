function Sparkline({ points }) {
  if (!points.length) {
    return <div className="sparkline-empty">No CPU samples yet.</div>;
  }

  const max = Math.max(1, ...points);
  const w = 600;
  const h = 160;

  const segments = points
    .map((v, i) => {
      const x = (i / Math.max(points.length - 1, 1)) * w;
      const y = h - (v / max) * h;
      return `${x},${y}`;
    })
    .join(" ");

  return (
    <svg className="sparkline" viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none">
      <polyline fill="none" stroke="currentColor" strokeWidth="2.5" points={segments} />
    </svg>
  );
}

export default function PerformanceView({ processes, selected, selectedId, cpuHistory, onSelect }) {
  return (
    <div className="content-view">
      <div className="content-header">
        <div>
          <h1 className="page-title">Performance</h1>
          <p className="page-subtitle">Monitor resource usage</p>
        </div>
      </div>

      <div className="perf-layout">
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

        <div className="perf-main">
          <div className="perf-grid">
            <div className="metric-card"><span className="metric-label">Status</span><span className="metric-value">{selected?.status || "-"}</span></div>
            <div className="metric-card"><span className="metric-label">CPU</span><span className="metric-value">{selected?.cpuPercent ?? "-"}%</span></div>
            <div className="metric-card"><span className="metric-label">PID</span><span className="metric-value">{selected?.pid ?? "-"}</span></div>
            <div className="metric-card"><span className="metric-label">Working Set</span><span className="metric-value">{selected?.workingSetDisplay || "-"}</span></div>
            <div className="metric-card"><span className="metric-label">Private Memory</span><span className="metric-value">{selected?.privateMemoryDisplay || "-"}</span></div>
            <div className="metric-card"><span className="metric-label">Threads / Handles</span><span className="metric-value">{selected ? `${selected.threadCount ?? "-"} / ${selected.handleCount ?? "-"}` : "-"}</span></div>
          </div>

          <div className="chart-container">
            <p className="chart-title">CPU trend (latest 60 samples)</p>
            <Sparkline points={cpuHistory.points} />
          </div>
        </div>
      </div>
    </div>
  );
}
