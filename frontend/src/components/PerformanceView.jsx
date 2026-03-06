function Sparkline({ points }) {
  if (!points.length) {
    return <div className="sparkline-empty">No CPU samples yet.</div>;
  }

  const max = Math.max(1, ...points);
  const width = 600;
  const height = 140;

  const segments = points
    .map((value, index) => {
      const x = (index / Math.max(points.length - 1, 1)) * width;
      const y = height - (value / max) * height;
      return `${x},${y}`;
    })
    .join(" ");

  return (
    <svg className="sparkline" viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none">
      <polyline fill="none" stroke="currentColor" strokeWidth="3" points={segments} />
    </svg>
  );
}

export default function PerformanceView({ selected, cpuHistory, onSelect }) {
  return (
    <section className="view">
      <div className="view-header">
        <h3>Performance</h3>
        <select
          value={selected?.id || ""}
          onChange={(event) => onSelect(event.target.value)}
          className="process-select"
        >
          <option value="">Select process</option>
          {cpuHistory.available.map((process) => (
            <option key={process.id} value={process.id}>{process.name}</option>
          ))}
        </select>
      </div>

      <div className="mini-grid metrics">
        <div><span>Status</span><strong>{selected?.status || "-"}</strong></div>
        <div><span>CPU</span><strong>{selected?.cpuPercent ?? "-"}%</strong></div>
        <div><span>PID</span><strong>{selected?.pid ?? "-"}</strong></div>
        <div><span>Working Set</span><strong>{selected?.workingSetDisplay || "-"}</strong></div>
        <div><span>Private Memory</span><strong>{selected?.privateMemoryDisplay || "-"}</strong></div>
        <div><span>Threads / Handles</span><strong>{selected ? `${selected.threadCount ?? "-"} / ${selected.handleCount ?? "-"}` : "-"}</strong></div>
      </div>

      <div className="chart-panel">
        <p className="muted">CPU trend (latest 60 samples)</p>
        <Sparkline points={cpuHistory.points} />
      </div>
    </section>
  );
}
