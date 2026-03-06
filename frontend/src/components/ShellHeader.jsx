export default function ShellHeader({ runningCount, apiBase, onLogout }) {
  return (
    <header className="shell-header">
      <div>
        <p className="eyebrow">CmdHub</p>
        <h2>Remote Operations Console</h2>
      </div>

      <div className="header-meta">
        <span className="pill">API {apiBase}</span>
        <span className="pill">{runningCount} running</span>
        <button className="ghost" onClick={onLogout}>
          Sign Out
        </button>
      </div>
    </header>
  );
}
