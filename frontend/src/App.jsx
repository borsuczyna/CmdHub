import { useEffect, useMemo, useState } from "react";
import { apiRequest, loadPanelConfig } from "./api";
import LoginScreen from "./components/LoginScreen";
import ShellHeader from "./components/ShellHeader";
import Tabs from "./components/Tabs";
import ProcessesView from "./components/ProcessesView";
import LogsView from "./components/LogsView";
import PerformanceView from "./components/PerformanceView";
import CommandDialog from "./components/CommandDialog";

const PASSWORD_KEY = "cmdhub-panel-password";

function formatBytes(bytes) {
  if (typeof bytes !== "number" || !Number.isFinite(bytes)) return "-";
  const kb = 1024;
  const mb = 1024 * kb;
  const gb = 1024 * mb;
  if (bytes >= gb) return `${(bytes / gb).toFixed(2)} GB`;
  if (bytes >= mb) return `${(bytes / mb).toFixed(2)} MB`;
  if (bytes >= kb) return `${(bytes / kb).toFixed(2)} KB`;
  return `${bytes} B`;
}

export default function App() {
  const [apiPort, setApiPort] = useState(null);
  const [password, setPassword] = useState("");
  const [authError, setAuthError] = useState("");
  const [authBusy, setAuthBusy] = useState(false);
  const [authed, setAuthed] = useState(false);
  const [tab, setTab] = useState("processes");
  const [processes, setProcesses] = useState([]);
  const [toast, setToast] = useState("");
  const [logsText, setLogsText] = useState("");
  const [logsTail, setLogsTail] = useState(16000);
  const [logsAutoRefresh, setLogsAutoRefresh] = useState(true);
  const [selectedLogsId, setSelectedLogsId] = useState(null);
  const [selectedPerfId, setSelectedPerfId] = useState(null);
  const [cpuPoints, setCpuPoints] = useState([]);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogProcess, setDialogProcess] = useState(null);

  const apiBase = useMemo(() => {
    if (!apiPort) return "";
    return `${window.location.protocol}//${window.location.hostname}:${apiPort}/api`;
  }, [apiPort]);

  const selectedPerf = useMemo(() => {
    const process = processes.find((item) => item.id === selectedPerfId);
    if (!process) return null;
    return {
      ...process,
      workingSetDisplay: formatBytes(process.workingSetBytes),
      privateMemoryDisplay: formatBytes(process.privateMemoryBytes)
    };
  }, [processes, selectedPerfId]);

  useEffect(() => {
    loadPanelConfig()
      .then((config) => {
        setApiPort(config.apiPort);
      })
      .catch((error) => {
        setAuthError(error.message);
      });
  }, []);

  useEffect(() => {
    if (!apiBase) return;
    const remembered = localStorage.getItem(PASSWORD_KEY);
    if (remembered) {
      void login(remembered, true);
    }
  }, [apiBase]);

  useEffect(() => {
    if (!authed || !apiBase) return;
    void refreshProcesses();
    const timer = window.setInterval(() => {
      void refreshProcesses();
    }, 2000);
    return () => window.clearInterval(timer);
  }, [authed, apiBase]);

  useEffect(() => {
    if (!authed || !apiBase || tab !== "logs" || !selectedLogsId || !logsAutoRefresh) return;
    void refreshLogs();
    const timer = window.setInterval(() => {
      void refreshLogs();
    }, 1600);
    return () => window.clearInterval(timer);
  }, [authed, apiBase, tab, selectedLogsId, logsTail, logsAutoRefresh]);

  useEffect(() => {
    if (!selectedPerf) return;
    setCpuPoints((prev) => {
      const next = [...prev, selectedPerf.cpuPercent ?? 0];
      return next.slice(-60);
    });
  }, [selectedPerf?.cpuPercent]);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(""), 2200);
    return () => window.clearTimeout(timer);
  }, [toast]);

  async function login(value, remember) {
    if (!apiBase) return;
    setAuthBusy(true);
    setAuthError("");

    try {
      await apiRequest(apiBase, value, "/processes");
      setPassword(value);
      setAuthed(true);
      if (remember) {
        localStorage.setItem(PASSWORD_KEY, value);
      } else {
        localStorage.removeItem(PASSWORD_KEY);
      }
    } catch (error) {
      setAuthed(false);
      setAuthError(error.message || "Authentication failed.");
    } finally {
      setAuthBusy(false);
    }
  }

  function logout() {
    setAuthed(false);
    setPassword("");
    localStorage.removeItem(PASSWORD_KEY);
  }

  async function refreshProcesses() {
    if (!authed || !apiBase) return;
    const response = await apiRequest(apiBase, password, "/processes");
    const list = response?.processes || [];
    setProcesses(list);

    if (!selectedLogsId && list.length) {
      setSelectedLogsId(list[0].id);
    }

    if (!selectedPerfId && list.length) {
      setSelectedPerfId(list[0].id);
    }
  }

  async function refreshLogs() {
    if (!selectedLogsId || !authed || !apiBase) return;
    const response = await apiRequest(apiBase, password, `/processes/${encodeURIComponent(selectedLogsId)}/logs?tail=${logsTail}`);
    const text = (response?.logs || []).join("\n");
    setLogsText(text);
  }

  async function commandAction(id, action) {
    await apiRequest(apiBase, password, `/processes/${encodeURIComponent(id)}/actions/${action}`, { method: "POST" });
    setToast(`Action ${action} requested.`);
    await refreshProcesses();
  }

  async function clearLogs() {
    if (!selectedLogsId) return;
    await commandAction(selectedLogsId, "clear-logs");
    setLogsText("");
  }

  async function removeProcess(id) {
    if (!window.confirm("Delete this command?")) {
      return;
    }

    await apiRequest(apiBase, password, `/processes/${encodeURIComponent(id)}`, { method: "DELETE" });
    setToast("Command deleted.");
    await refreshProcesses();
  }

  async function saveCommand(model) {
    const payload = {
      ...model,
      runEveryUnit: "minutes",
      restartEveryUnit: "minutes"
    };

    if (dialogProcess) {
      await apiRequest(apiBase, password, `/processes/${encodeURIComponent(dialogProcess.id)}`, {
        method: "PUT",
        body: JSON.stringify(payload)
      });
      setToast("Command updated.");
    } else {
      await apiRequest(apiBase, password, "/processes", {
        method: "POST",
        body: JSON.stringify(payload)
      });
      setToast("Command created.");
    }

    setDialogOpen(false);
    setDialogProcess(null);
    await refreshProcesses();
  }

  if (!authed) {
    return <LoginScreen onLogin={login} loading={authBusy} error={authError} />;
  }

  return (
    <main className="app-shell">
      <ShellHeader
        runningCount={processes.filter((item) => item.isRunning).length}
        apiBase={apiBase}
        onLogout={logout}
      />

      <Tabs value={tab} onChange={setTab} />

      {tab === "processes" ? (
        <ProcessesView
          processes={processes}
          onRefresh={refreshProcesses}
          onCreate={() => {
            setDialogProcess(null);
            setDialogOpen(true);
          }}
          onEdit={(process) => {
            setDialogProcess(process);
            setDialogOpen(true);
          }}
          onAction={commandAction}
          onDelete={removeProcess}
          onOpenLogs={(id) => {
            setSelectedLogsId(id);
            setTab("logs");
          }}
          onOpenPerformance={(id) => {
            setSelectedPerfId(id);
            setTab("performance");
          }}
        />
      ) : null}

      {tab === "logs" ? (
        <LogsView
          processes={processes}
          selectedId={selectedLogsId}
          logs={logsText}
          tail={logsTail}
          autoRefresh={logsAutoRefresh}
          onSelect={(id) => {
            setSelectedLogsId(id);
            setLogsText("");
          }}
          onTailChange={setLogsTail}
          onAutoRefreshChange={setLogsAutoRefresh}
          onRefresh={refreshLogs}
          onClear={clearLogs}
        />
      ) : null}

      {tab === "performance" ? (
        <PerformanceView
          selected={selectedPerf}
          cpuHistory={{ points: cpuPoints, available: processes }}
          onSelect={setSelectedPerfId}
        />
      ) : null}

      <CommandDialog
        process={dialogProcess}
        open={dialogOpen}
        onClose={() => {
          setDialogOpen(false);
          setDialogProcess(null);
        }}
        onSave={saveCommand}
      />

      {toast ? <div className="toast">{toast}</div> : null}
    </main>
  );
}
