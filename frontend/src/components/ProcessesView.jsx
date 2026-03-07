import { useState } from "react";

function StatusBadge({ status }) {
  const s = (status || "Stopped").toLowerCase();
  const cls = s === "running" ? "badge-success" : s === "crashed" ? "badge-danger" : "badge-neutral";
  return (
    <span className={`status-badge ${cls}`}>
      <span className="badge-dot" />
      {status || "Stopped"}
    </span>
  );
}

export default function ProcessesView({
  processes,
  onCreate,
  onRefresh,
  onEdit,
  onAction,
  onDelete,
  onOpenLogs,
  onOpenPerformance
}) {
  const [search, setSearch] = useState("");

  const running = processes.filter((p) => p.status === "Running").length;
  const stopped = processes.filter((p) => !p.status || p.status === "Stopped").length;
  const crashed = processes.filter((p) => p.status === "Crashed").length;

  const filtered = search
    ? processes.filter(
        (p) =>
          p.name?.toLowerCase().includes(search.toLowerCase()) ||
          p.command?.toLowerCase().includes(search.toLowerCase())
      )
    : processes;

  return (
    <div className="content-view">
      <div className="content-header">
        <div>
          <h1 className="page-title">Processes</h1>
          <p className="page-subtitle">Manage your running commands and services</p>
        </div>
        <div className="header-actions">
          <button className="btn btn-primary" onClick={onCreate}>+ New Command</button>
        </div>
      </div>

      <div className="stats-row">
        <div className="stat-card">
          <span className="stat-label">Running</span>
          <span className="stat-value stat-success">{running}</span>
        </div>
        <div className="stat-card">
          <span className="stat-label">Stopped</span>
          <span className="stat-value">{stopped}</span>
        </div>
        <div className="stat-card">
          <span className="stat-label">Crashed</span>
          <span className="stat-value stat-danger">{crashed}</span>
        </div>
        <div className="stat-card">
          <span className="stat-label">Total</span>
          <span className="stat-value">{processes.length}</span>
        </div>
      </div>

      <div className="toolbar">
        <div className="search-box">
          <svg className="search-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
          <input
            type="text"
            className="search-input"
            placeholder="Search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <button className="btn btn-ghost" onClick={onRefresh}>↻ Refresh</button>
      </div>

      <div className="table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Command</th>
              <th>Status</th>
              <th>Auto-Restart</th>
              <th style={{ textAlign: "right" }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 ? (
              <tr><td colSpan={5} className="empty-row">No commands found.</td></tr>
            ) : null}
            {filtered.map((p) => (
              <tr key={p.id}>
                <td className="cell-name">{p.name}</td>
                <td className="cell-command mono" title={p.command}>{p.command}</td>
                <td><StatusBadge status={p.status} /></td>
                <td>
                  {p.autoRestart
                    ? <span className="text-success" style={{ fontWeight: 600 }}>Yes</span>
                    : <span className="text-muted">No</span>}
                </td>
                <td className="cell-actions">
                  <div className="action-group">
                    <button className="btn-icon success" title="Start" onClick={() => onAction(p.id, "start")}>▶</button>
                    <button className="btn-icon danger" title="Stop" onClick={() => onAction(p.id, "stop")}>■</button>
                    <button className="btn-icon" title="Restart" onClick={() => onAction(p.id, "restart")}>↻</button>
                    <button className="btn-icon" title="Logs" onClick={() => onOpenLogs(p.id)}>☰</button>
                    <button className="btn-icon" title="Performance" onClick={() => onOpenPerformance(p.id)}>⚡</button>
                    <button className="btn-icon" title="Edit" onClick={() => onEdit(p)}>✎</button>
                    <button className="btn-icon danger" title="Delete" onClick={() => onDelete(p.id)}>✕</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
