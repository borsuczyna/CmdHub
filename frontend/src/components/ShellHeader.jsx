export default function ShellHeader({
  runningCount,
  apiPort,
  panelPort,
  onCreate,
  onSettings,
  onLogout
}) {
  return (
    <header className="shell-header">
      <div className="brand-box">
        <div className="brand-mark">⚙</div>
        <h1>CmdHub</h1>
      </div>

      <div className="header-actions">
        <button className="primary" onClick={onCreate}>+ New Command</button>
        <button className="remote-pill" onClick={onSettings}>
          Remote: On (API {apiPort ?? "-"} | Panel {panelPort ?? "-"})
        </button>
        <button className="ghost" onClick={onSettings}>Settings</button>
      </div>

      <div className="header-meta">
        <span>{runningCount} processes running</span>
        <button className="logout-link" onClick={onLogout}>
          Sign Out
        </button>
      </div>
    </header>
  );
}
