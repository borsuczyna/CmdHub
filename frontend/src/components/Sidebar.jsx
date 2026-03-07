import { useState } from "react";

const navItems = [
  { id: "processes", label: "Processes", icon: "⊞" },
  { id: "logs", label: "Logs", icon: "☰" },
  { id: "performance", label: "Performance", icon: "⚡" },
];

export default function Sidebar({ activeTab, onTabChange, apiPort, panelPort, onSettings, onLogout }) {
  const [mobileOpen, setMobileOpen] = useState(false);

  function navigate(id) {
    onTabChange(id);
    setMobileOpen(false);
  }

  return (
    <>
      <button
        className="mobile-menu-btn"
        type="button"
        onClick={() => setMobileOpen(true)}
        aria-label="Open menu"
      >
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M3 12h18M3 6h18M3 18h18"/></svg>
      </button>

      {mobileOpen && <div className="sidebar-overlay" onClick={() => setMobileOpen(false)} />}

      <aside className={`sidebar ${mobileOpen ? "open" : ""}`}>
        <div className="sidebar-brand">
          <div className="brand-icon">⌘</div>
          <span className="brand-name">CmdHub</span>
          <button
            className="mobile-close-btn"
            type="button"
            onClick={() => setMobileOpen(false)}
            aria-label="Close menu"
          >
            ✕
          </button>
        </div>

        <nav className="sidebar-nav">
          <div className="sidebar-section">Navigation</div>
          {navItems.map((item) => (
            <button
              key={item.id}
              className={`nav-item ${activeTab === item.id ? "active" : ""}`}
              onClick={() => navigate(item.id)}
              type="button"
            >
              <span className="nav-icon">{item.icon}</span>
              {item.label}
            </button>
          ))}
        </nav>

        <div className="sidebar-footer">
          <div className="sidebar-info">
            API: {apiPort || "-"} · Panel: {panelPort || "-"}
          </div>
          <button className="nav-item" type="button" onClick={() => { onSettings(); setMobileOpen(false); }}>
            <span className="nav-icon">⚙</span>
            Settings
          </button>
          <button className="nav-item" type="button" onClick={() => { onLogout(); setMobileOpen(false); }}>
            <span className="nav-icon">↗</span>
            Sign Out
          </button>
        </div>
      </aside>
    </>
  );
}
