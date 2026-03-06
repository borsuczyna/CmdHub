using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CmdHub.Services;

public sealed class ControlPanelHostService : IDisposable
{
    private readonly Func<int> _getApiPort;

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public int Port { get; }
    public bool IsRunning => _listener?.IsListening == true;
    public bool IsLanAccessible { get; private set; }

    public ControlPanelHostService(int port, Func<int> getApiPort)
    {
        Port = port;
        _getApiPort = getApiPort;
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

      try
      {
        StartWithPrefixes(HttpListenerPrefixHelper.BuildPrefixes(Port));
        IsLanAccessible = true;
      }
      catch (HttpListenerException ex) when (ex.ErrorCode == 5)
      {
        Stop();
        StartWithPrefixes(HttpListenerPrefixHelper.BuildLocalPrefixes(Port));
        IsLanAccessible = false;
      }
    }

    private void StartWithPrefixes(IReadOnlyList<string> prefixes)
    {
      _listener = new HttpListener();
      foreach (var prefix in prefixes)
      {
        _listener.Prefixes.Add(prefix);
      }

      _listener.Start();

      _cts = new CancellationTokenSource();
      _serverTask = Task.Run(() => RunServerAsync(_cts.Token));
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // Best effort.
        }

        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch
        {
            // Best effort.
        }

        _listener = null;
        _cts?.Dispose();
        _cts = null;
        _serverTask = null;
        IsLanAccessible = false;
    }

    private async Task RunServerAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext? context = null;
            try
            {
                if (_listener == null || !_listener.IsListening)
                {
                    break;
                }

                context = await _listener.GetContextAsync();
                await HandleRequestAsync(context);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                if (context != null)
                {
                    TryWritePlainText(context.Response, 500, "Internal server error.");
                }
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        string path = (context.Request.Url?.AbsolutePath ?? "/").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        if (!string.Equals(path, "/", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase))
        {
            TryWritePlainText(context.Response, 404, "Not found.");
            return;
        }

        string html = BuildControlPanelHtml(_getApiPort());
        byte[] buffer = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;

        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.OutputStream.Close();
    }

    private static string BuildControlPanelHtml(int apiPort)
    {
        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>CmdHub Control Panel</title>
  <link rel="preconnect" href="https://fonts.googleapis.com" />
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
  <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;600&display=swap" rel="stylesheet" />
  <style>
    :root {
      --bg0: #071016;
      --bg1: #0d1c2a;
      --surface: #102230;
      --surface-2: #13293a;
      --border: #28506e;
      --text: #eff8ff;
      --muted: #9eb5c8;
      --brand: #00b7d6;
      --brand-2: #1edca2;
      --ok: #2ac769;
      --warn: #ffc14d;
      --bad: #ff5c66;
      --shadow: 0 14px 36px rgba(0, 0, 0, 0.35);
    }

    * { box-sizing: border-box; }
    html, body { height: 100%; }

    body {
      margin: 0;
      color: var(--text);
      font-family: "Plus Jakarta Sans", "Bahnschrift", "Segoe UI", sans-serif;
      background:
        radial-gradient(circle at 18% 12%, rgba(30, 220, 162, 0.14), transparent 40%),
        radial-gradient(circle at 85% 20%, rgba(0, 183, 214, 0.18), transparent 42%),
        linear-gradient(160deg, var(--bg1) 0%, var(--bg0) 60%, #04090d 100%);
      min-height: 100%;
    }

    .app {
      max-width: 1260px;
      margin: 0 auto;
      padding: 18px;
      animation: fade-in .35s ease;
    }

    .shell {
      border: 1px solid var(--border);
      border-radius: 18px;
      background: linear-gradient(180deg, rgba(19, 41, 58, 0.95), rgba(11, 26, 37, 0.95));
      backdrop-filter: blur(2px);
      box-shadow: var(--shadow);
      overflow: hidden;
    }

    .topbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 12px 14px;
      border-bottom: 1px solid var(--border);
      background: linear-gradient(180deg, rgba(21, 50, 70, 0.8), rgba(18, 42, 59, 0.8));
    }

    .brand {
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .brand-mark {
      width: 34px;
      height: 34px;
      border-radius: 10px;
      background: linear-gradient(150deg, var(--brand), var(--brand-2));
      color: #07313a;
      display: grid;
      place-items: center;
      font-weight: 700;
    }

    .brand-title {
      font-size: 17px;
      font-weight: 700;
      line-height: 1.1;
      letter-spacing: .2px;
    }

    .brand-sub {
      color: var(--muted);
      font-size: 12px;
      margin-top: 2px;
    }

    .right-info {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      align-items: center;
      justify-content: flex-end;
    }

    .chip {
      background: rgba(14, 32, 45, 0.95);
      border: 1px solid var(--border);
      border-radius: 999px;
      color: var(--muted);
      font-size: 12px;
      padding: 6px 11px;
    }

    .tabs {
      display: flex;
      gap: 8px;
      padding: 10px 14px 0;
      border-bottom: 1px solid rgba(40, 80, 110, 0.6);
      overflow-x: auto;
    }

    .tab {
      border: 1px solid transparent;
      border-bottom: none;
      border-radius: 10px 10px 0 0;
      color: var(--muted);
      background: transparent;
      padding: 9px 12px;
      font-size: 13px;
      font-weight: 600;
      cursor: pointer;
      white-space: nowrap;
    }

    .tab.active {
      border-color: var(--border);
      background: rgba(17, 39, 55, 0.95);
      color: var(--text);
      box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.06);
    }

    .view {
      display: none;
      padding: 14px;
      animation: slide-up .22s ease;
    }

    .view.active { display: block; }

    .toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 10px;
      margin-bottom: 12px;
    }

    .toolbar h2 {
      margin: 0;
      font-size: 16px;
      font-weight: 700;
    }

    .row {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      align-items: center;
    }

    button,
    input,
    select,
    textarea {
      font: inherit;
      color: var(--text);
    }

    button {
      border: 1px solid transparent;
      background: linear-gradient(180deg, #1c6f9a, #0f4f72);
      border-radius: 9px;
      padding: 8px 11px;
      cursor: pointer;
      font-size: 13px;
      font-weight: 600;
      transition: transform .1s ease, filter .16s ease;
    }

    button:hover { filter: brightness(1.1); }
    button:active { transform: translateY(1px); }
    button.alt {
      background: linear-gradient(180deg, #345268, #273f52);
      border-color: rgba(101, 138, 161, 0.35);
    }
    button.bad {
      background: linear-gradient(180deg, #9a3440, #6e222b);
      border-color: rgba(255, 102, 112, 0.35);
    }
    button.good {
      background: linear-gradient(180deg, #1f8f58, #14603b);
      border-color: rgba(80, 212, 135, 0.35);
    }

    input,
    select,
    textarea {
      background: rgba(8, 22, 32, 0.88);
      border: 1px solid rgba(102, 145, 171, 0.4);
      border-radius: 9px;
      padding: 8px 10px;
      min-height: 36px;
      outline: none;
    }

    input:focus,
    select:focus,
    textarea:focus {
      border-color: var(--brand);
      box-shadow: 0 0 0 2px rgba(0, 183, 214, 0.2);
    }

    .mono { font-family: "JetBrains Mono", "Consolas", monospace; }
    .muted { color: var(--muted); }

    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(290px, 1fr));
      gap: 10px;
    }

    .proc-card {
      border: 1px solid var(--border);
      border-radius: 12px;
      background: linear-gradient(180deg, rgba(15, 34, 48, 0.9), rgba(11, 25, 36, 0.9));
      padding: 11px;
      box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.04);
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .proc-head {
      display: flex;
      justify-content: space-between;
      align-items: start;
      gap: 8px;
    }

    .proc-name {
      margin: 0;
      font-size: 15px;
      font-weight: 700;
      line-height: 1.2;
    }

    .proc-cmd {
      color: #d8e7f5;
      font-size: 12px;
      margin: 2px 0 0;
      word-break: break-word;
      opacity: 0.95;
    }

    .badge {
      border-radius: 999px;
      padding: 4px 8px;
      font-size: 11px;
      font-weight: 700;
      border: 1px solid transparent;
      white-space: nowrap;
    }

    .status-running {
      color: #bdf5cf;
      background: rgba(28, 101, 56, 0.35);
      border-color: rgba(58, 182, 105, 0.6);
    }

    .status-stopped {
      color: #d2deea;
      background: rgba(54, 75, 94, 0.38);
      border-color: rgba(120, 150, 172, 0.5);
    }

    .status-crashed {
      color: #ffd6d9;
      background: rgba(116, 34, 42, 0.4);
      border-color: rgba(248, 101, 112, 0.6);
    }

    .kv {
      display: grid;
      grid-template-columns: repeat(2, minmax(120px, 1fr));
      gap: 6px;
      font-size: 12px;
    }

    .kv div {
      background: rgba(5, 14, 21, 0.46);
      border: 1px solid rgba(98, 136, 160, 0.22);
      border-radius: 8px;
      padding: 6px;
    }

    .tag-row {
      display: flex;
      flex-wrap: wrap;
      gap: 5px;
    }

    .tag {
      font-size: 11px;
      color: #c7dfef;
      border-radius: 999px;
      border: 1px solid rgba(118, 162, 189, 0.34);
      padding: 2px 7px;
      background: rgba(16, 41, 57, 0.75);
    }

    .action-row {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      margin-top: 2px;
    }

    .action-row button {
      padding: 6px 8px;
      font-size: 12px;
    }

    .logs-layout {
      display: grid;
      grid-template-columns: minmax(230px, 290px) 1fr;
      gap: 10px;
    }

    .panel {
      border: 1px solid var(--border);
      border-radius: 12px;
      background: rgba(13, 30, 42, 0.85);
      padding: 10px;
    }

    .process-list {
      max-height: 480px;
      overflow: auto;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .process-item {
      width: 100%;
      text-align: left;
      background: rgba(8, 20, 29, 0.85);
      border: 1px solid rgba(95, 134, 160, 0.32);
      border-radius: 9px;
      padding: 8px;
      cursor: pointer;
    }

    .process-item.active {
      border-color: var(--brand);
      background: rgba(11, 34, 47, 0.9);
      box-shadow: 0 0 0 1px rgba(0, 183, 214, 0.42);
    }

    .logs {
      min-height: 480px;
      max-height: 60vh;
      overflow: auto;
      border-radius: 10px;
      border: 1px solid rgba(91, 133, 163, 0.38);
      padding: 10px;
      white-space: pre-wrap;
      background: #050d14;
      font-size: 12px;
      line-height: 1.42;
      color: #d4e7f8;
    }

    .metric-grid {
      display: grid;
      grid-template-columns: repeat(3, minmax(170px, 1fr));
      gap: 8px;
      margin-bottom: 10px;
    }

    .metric {
      border: 1px solid rgba(103, 146, 172, 0.4);
      border-radius: 11px;
      background: rgba(8, 21, 31, 0.82);
      padding: 10px;
    }

    .metric .k {
      color: var(--muted);
      font-size: 12px;
      margin: 0;
    }

    .metric .v {
      margin: 4px 0 0;
      font-size: 21px;
      font-weight: 700;
      line-height: 1.1;
    }

    .canvas-wrap {
      border: 1px solid rgba(96, 139, 164, 0.42);
      border-radius: 11px;
      background: rgba(6, 18, 27, 0.85);
      padding: 8px;
    }

    #cpuCanvas {
      width: 100%;
      height: 170px;
      display: block;
      border-radius: 7px;
    }

    .overlay {
      position: fixed;
      inset: 0;
      background: rgba(2, 8, 12, 0.72);
      display: none;
      align-items: center;
      justify-content: center;
      z-index: 40;
      padding: 12px;
      backdrop-filter: blur(2px);
    }

    .overlay.open { display: flex; }

    .dialog {
      width: min(720px, 96vw);
      max-height: 92vh;
      overflow: auto;
      border: 1px solid var(--border);
      border-radius: 14px;
      background: linear-gradient(180deg, rgba(16, 37, 52, 0.98), rgba(9, 22, 32, 0.98));
      box-shadow: var(--shadow);
      padding: 13px;
    }

    .dialog h3 {
      margin: 0 0 10px;
      font-size: 17px;
    }

    .form-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(180px, 1fr));
      gap: 8px;
    }

    .field {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .field label {
      font-size: 12px;
      color: var(--muted);
    }

    .checks {
      display: flex;
      gap: 10px;
      flex-wrap: wrap;
      margin-top: 8px;
      font-size: 13px;
      color: #c9deef;
    }

    .checks label {
      display: inline-flex;
      align-items: center;
      gap: 5px;
    }

    .foot {
      margin-top: 10px;
      display: flex;
      gap: 8px;
      justify-content: flex-end;
      flex-wrap: wrap;
    }

    .login-wrap {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 14px;
    }

    .login-card {
      width: min(460px, 96vw);
      border-radius: 18px;
      border: 1px solid var(--border);
      background: linear-gradient(160deg, rgba(16, 37, 52, 0.95), rgba(8, 20, 30, 0.95));
      box-shadow: var(--shadow);
      padding: 22px;
    }

    .login-title {
      font-size: 24px;
      margin: 0;
      font-weight: 700;
    }

    .login-sub {
      margin: 5px 0 15px;
      color: var(--muted);
      font-size: 13px;
      line-height: 1.45;
    }

    .error {
      color: #ffd6db;
      border: 1px solid rgba(243, 105, 118, 0.56);
      border-radius: 9px;
      background: rgba(112, 29, 38, 0.36);
      padding: 7px 9px;
      font-size: 12px;
      margin-bottom: 10px;
      display: none;
    }

    .hidden { display: none !important; }

    .toast {
      position: fixed;
      right: 12px;
      bottom: 12px;
      z-index: 60;
      background: rgba(5, 20, 30, 0.95);
      border: 1px solid rgba(95, 141, 167, 0.5);
      border-radius: 9px;
      color: #e6f4ff;
      padding: 8px 10px;
      font-size: 12px;
      min-width: 210px;
      max-width: 360px;
      box-shadow: var(--shadow);
      opacity: 0;
      transform: translateY(6px);
      transition: opacity .15s ease, transform .15s ease;
      pointer-events: none;
    }

    .toast.show {
      opacity: 1;
      transform: translateY(0);
    }

    @keyframes fade-in {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @keyframes slide-up {
      from { opacity: 0; transform: translateY(6px); }
      to { opacity: 1; transform: translateY(0); }
    }

    @media (max-width: 980px) {
      .logs-layout { grid-template-columns: 1fr; }
      .metric-grid { grid-template-columns: repeat(2, minmax(150px, 1fr)); }
      .form-grid { grid-template-columns: 1fr; }
    }

    @media (max-width: 640px) {
      .app { padding: 8px; }
      .tabs { padding: 8px 8px 0; }
      .view { padding: 10px 8px; }
      .metric-grid { grid-template-columns: 1fr; }
      .kv { grid-template-columns: 1fr; }
      .logs { min-height: 360px; }
      .topbar { padding: 10px; }
      .brand-title { font-size: 15px; }
      .brand-sub { font-size: 11px; }
    }
  </style>
</head>
<body>
  <section id="loginScreen" class="login-wrap">
    <div class="login-card">
      <h1 class="login-title">CmdHub Remote Login</h1>
      <p class="login-sub">Authenticate with the control panel password from CmdHub settings. This panel supports process controls, logs, and live performance from desktop and mobile.</p>
      <div id="loginErr" class="error"></div>
      <div class="field">
        <label for="loginPassword">Password</label>
        <input id="loginPassword" type="password" autocomplete="current-password" placeholder="Control panel password" />
      </div>
      <div class="checks">
        <label><input type="checkbox" id="rememberPwd" /> Remember on this browser</label>
      </div>
      <div class="foot">
        <button id="loginBtn" class="good">Sign In</button>
      </div>
    </div>
  </section>

  <main id="appRoot" class="app hidden">
    <div class="shell">
      <header class="topbar">
        <div class="brand">
          <div class="brand-mark">CH</div>
          <div>
            <div class="brand-title">CmdHub Control Panel</div>
            <div class="brand-sub">Remote process control, console logs, and performance</div>
          </div>
        </div>
        <div class="right-info">
          <span class="chip">API <span id="apiBase" class="mono"></span></span>
          <span class="chip" id="runningChip">0 running</span>
          <span class="chip" id="refreshChip">refresh idle</span>
          <button id="logoutBtn" class="alt">Sign Out</button>
        </div>
      </header>

      <nav class="tabs">
        <button class="tab active" data-view="processesView">Processes</button>
        <button class="tab" data-view="logsView">Logs</button>
        <button class="tab" data-view="performanceView">Performance</button>
      </nav>

      <section id="processesView" class="view active">
        <div class="toolbar">
          <h2>Processes</h2>
          <div class="row">
            <button id="refreshProcessesBtn" class="alt">Refresh</button>
            <button id="newCommandBtn" class="good">New Command</button>
          </div>
        </div>
        <div id="processGrid" class="grid"></div>
      </section>

      <section id="logsView" class="view">
        <div class="toolbar">
          <h2>Logs</h2>
          <div class="row">
            <label class="row muted" for="logsTailInput">Tail</label>
            <input id="logsTailInput" type="number" min="100" max="200000" step="100" value="16000" style="width: 110px;" />
            <label class="row"><input type="checkbox" id="logsAutoScroll" checked /> Auto-scroll</label>
            <label class="row"><input type="checkbox" id="logsAutoRefresh" checked /> Auto-refresh</label>
            <button id="clearLogsBtn" class="alt">Clear Logs</button>
            <button id="copyLogsBtn" class="alt">Copy</button>
            <button id="refreshLogsBtn" class="alt">Refresh</button>
          </div>
        </div>
        <div class="logs-layout">
          <aside class="panel">
            <div class="muted" style="margin-bottom: 8px; font-size: 12px;">Select process</div>
            <div id="logsProcessList" class="process-list"></div>
          </aside>
          <section class="panel">
            <div class="row" style="justify-content: space-between; margin-bottom: 7px;">
              <div>
                <strong id="logsTitle">No process selected</strong>
                <div id="logsMeta" class="muted" style="font-size: 12px;"></div>
              </div>
            </div>
            <div id="logsOutput" class="logs mono">Select a process to view logs.</div>
          </section>
        </div>
      </section>

      <section id="performanceView" class="view">
        <div class="toolbar">
          <h2>Performance</h2>
          <div class="row">
            <select id="perfProcessSelect" style="min-width: 240px;"></select>
            <button id="refreshPerfBtn" class="alt">Refresh</button>
          </div>
        </div>
        <div class="metric-grid">
          <div class="metric">
            <p class="k">Status</p>
            <p id="mStatus" class="v">-</p>
          </div>
          <div class="metric">
            <p class="k">CPU</p>
            <p id="mCpu" class="v">-</p>
          </div>
          <div class="metric">
            <p class="k">PID</p>
            <p id="mPid" class="v">-</p>
          </div>
          <div class="metric">
            <p class="k">Working Set</p>
            <p id="mWorking" class="v" style="font-size: 17px;">-</p>
          </div>
          <div class="metric">
            <p class="k">Private Memory</p>
            <p id="mPrivate" class="v" style="font-size: 17px;">-</p>
          </div>
          <div class="metric">
            <p class="k">Threads / Handles</p>
            <p id="mThreads" class="v" style="font-size: 17px;">-</p>
          </div>
        </div>

        <div class="canvas-wrap">
          <div class="muted" style="font-size: 12px; margin: 0 0 6px;">CPU trend (last 60 samples)</div>
          <canvas id="cpuCanvas" width="960" height="170"></canvas>
        </div>
      </section>
    </div>
  </main>

  <div id="commandDialogOverlay" class="overlay">
    <div class="dialog">
      <h3 id="commandDialogTitle">New Command</h3>
      <div class="form-grid">
        <div class="field" style="grid-column: 1 / -1;">
          <label for="fName">Name</label>
          <input id="fName" type="text" />
        </div>
        <div class="field" style="grid-column: 1 / -1;">
          <label for="fCommand">Command</label>
          <input id="fCommand" type="text" class="mono" />
        </div>
        <div class="field" style="grid-column: 1 / -1;">
          <label for="fWorkingDirectory">Working Directory</label>
          <input id="fWorkingDirectory" type="text" class="mono" />
        </div>
        <div class="field">
          <label for="fRunEveryInterval">Run Every Interval</label>
          <input id="fRunEveryInterval" type="number" min="1" value="5" />
        </div>
        <div class="field">
          <label for="fRunEveryUnit">Run Every Unit</label>
          <select id="fRunEveryUnit">
            <option value="seconds">seconds</option>
            <option value="minutes" selected>minutes</option>
            <option value="hours">hours</option>
          </select>
        </div>
        <div class="field">
          <label for="fRestartEveryInterval">Restart Every Interval</label>
          <input id="fRestartEveryInterval" type="number" min="1" value="5" />
        </div>
        <div class="field">
          <label for="fRestartEveryUnit">Restart Every Unit</label>
          <select id="fRestartEveryUnit">
            <option value="seconds">seconds</option>
            <option value="minutes" selected>minutes</option>
            <option value="hours">hours</option>
          </select>
        </div>
      </div>

      <div class="checks">
        <label><input id="fAutoRestart" type="checkbox" /> Auto-restart on exit</label>
        <label><input id="fRunOnStart" type="checkbox" /> Run on start</label>
        <label><input id="fUsePowerShell" type="checkbox" /> Use PowerShell</label>
        <label><input id="fRunEveryEnabled" type="checkbox" /> Enable run every</label>
        <label><input id="fRestartEveryEnabled" type="checkbox" /> Enable restart every</label>
      </div>

      <div class="foot">
        <button id="cancelCommandBtn" class="alt">Cancel</button>
        <button id="saveCommandBtn" class="good">Save</button>
      </div>
    </div>
  </div>

  <div id="toast" class="toast"></div>

<script>
  const apiBase = location.protocol + '//' + location.hostname + ':{{apiPort}}/api';
  const REFRESH_MS = 2000;
  const LOG_REFRESH_MS = 1600;
  const CPU_HISTORY_MAX = 60;

  const state = {
    password: '',
    processes: [],
    selectedLogsProcessId: null,
    selectedPerfProcessId: null,
    cpuHistory: [],
    commandEditId: null,
    processTimer: null,
    logTimer: null,
    isBusy: false
  };

  const dom = {
    loginScreen: document.getElementById('loginScreen'),
    appRoot: document.getElementById('appRoot'),
    loginPassword: document.getElementById('loginPassword'),
    rememberPwd: document.getElementById('rememberPwd'),
    loginBtn: document.getElementById('loginBtn'),
    loginErr: document.getElementById('loginErr'),
    logoutBtn: document.getElementById('logoutBtn'),
    apiBase: document.getElementById('apiBase'),
    runningChip: document.getElementById('runningChip'),
    refreshChip: document.getElementById('refreshChip'),
    tabs: Array.from(document.querySelectorAll('.tab')),
    views: Array.from(document.querySelectorAll('.view')),
    processGrid: document.getElementById('processGrid'),
    refreshProcessesBtn: document.getElementById('refreshProcessesBtn'),
    newCommandBtn: document.getElementById('newCommandBtn'),
    logsProcessList: document.getElementById('logsProcessList'),
    logsTailInput: document.getElementById('logsTailInput'),
    logsAutoScroll: document.getElementById('logsAutoScroll'),
    logsAutoRefresh: document.getElementById('logsAutoRefresh'),
    clearLogsBtn: document.getElementById('clearLogsBtn'),
    copyLogsBtn: document.getElementById('copyLogsBtn'),
    refreshLogsBtn: document.getElementById('refreshLogsBtn'),
    logsTitle: document.getElementById('logsTitle'),
    logsMeta: document.getElementById('logsMeta'),
    logsOutput: document.getElementById('logsOutput'),
    perfProcessSelect: document.getElementById('perfProcessSelect'),
    refreshPerfBtn: document.getElementById('refreshPerfBtn'),
    mStatus: document.getElementById('mStatus'),
    mCpu: document.getElementById('mCpu'),
    mPid: document.getElementById('mPid'),
    mWorking: document.getElementById('mWorking'),
    mPrivate: document.getElementById('mPrivate'),
    mThreads: document.getElementById('mThreads'),
    cpuCanvas: document.getElementById('cpuCanvas'),
    commandDialogOverlay: document.getElementById('commandDialogOverlay'),
    commandDialogTitle: document.getElementById('commandDialogTitle'),
    cancelCommandBtn: document.getElementById('cancelCommandBtn'),
    saveCommandBtn: document.getElementById('saveCommandBtn'),
    fName: document.getElementById('fName'),
    fCommand: document.getElementById('fCommand'),
    fWorkingDirectory: document.getElementById('fWorkingDirectory'),
    fAutoRestart: document.getElementById('fAutoRestart'),
    fRunOnStart: document.getElementById('fRunOnStart'),
    fUsePowerShell: document.getElementById('fUsePowerShell'),
    fRunEveryEnabled: document.getElementById('fRunEveryEnabled'),
    fRunEveryInterval: document.getElementById('fRunEveryInterval'),
    fRunEveryUnit: document.getElementById('fRunEveryUnit'),
    fRestartEveryEnabled: document.getElementById('fRestartEveryEnabled'),
    fRestartEveryInterval: document.getElementById('fRestartEveryInterval'),
    fRestartEveryUnit: document.getElementById('fRestartEveryUnit'),
    toast: document.getElementById('toast')
  };

  dom.apiBase.textContent = apiBase;

  function esc(value) {
    return String(value ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#39;');
  }

  function toUnit(value) {
    const v = String(value ?? '').trim().toLowerCase();
    if (v === 'second') return 'seconds';
    if (v === 'minute') return 'minutes';
    if (v === 'hour') return 'hours';
    if (v === 'seconds' || v === 'minutes' || v === 'hours') return v;
    return 'minutes';
  }

  function statusClass(status) {
    if (status === 'Running') return 'status-running';
    if (status === 'Crashed') return 'status-crashed';
    return 'status-stopped';
  }

  function fmtBytes(bytes) {
    if (typeof bytes !== 'number' || !Number.isFinite(bytes)) return '-';
    const kb = 1024;
    const mb = kb * 1024;
    const gb = mb * 1024;
    if (bytes >= gb) return (bytes / gb).toFixed(2) + ' GB';
    if (bytes >= mb) return (bytes / mb).toFixed(2) + ' MB';
    if (bytes >= kb) return (bytes / kb).toFixed(2) + ' KB';
    return bytes + ' B';
  }

  function showToast(message) {
    dom.toast.textContent = message;
    dom.toast.classList.add('show');
    clearTimeout(showToast._timer);
    showToast._timer = setTimeout(() => dom.toast.classList.remove('show'), 2100);
  }

  function setRefreshChip(value) {
    dom.refreshChip.textContent = value;
  }

  function setLoginError(message) {
    if (!message) {
      dom.loginErr.style.display = 'none';
      dom.loginErr.textContent = '';
      return;
    }

    dom.loginErr.textContent = message;
    dom.loginErr.style.display = 'block';
  }

  function headers(contentType = true) {
    const h = { 'X-CmdHub-Password': state.password || '' };
    if (contentType) {
      h['Content-Type'] = 'application/json';
    }
    return h;
  }

  async function req(path, options = {}) {
    const merged = Object.assign({ method: 'GET' }, options);
    const hasBody = merged.body !== undefined;
    merged.headers = Object.assign(headers(hasBody), merged.headers || {});

    const response = await fetch(apiBase + path, merged);
    const raw = await response.text();

    let json = null;
    try {
      json = raw ? JSON.parse(raw) : null;
    } catch {
      json = null;
    }

    if (!response.ok) {
      const message = json && json.error ? json.error : ('Request failed (' + response.status + ')');
      const err = new Error(message);
      err.status = response.status;
      throw err;
    }

    return json;
  }

  function getCurrentViewId() {
    const active = dom.views.find(v => v.classList.contains('active'));
    return active ? active.id : 'processesView';
  }

  function switchView(id) {
    dom.tabs.forEach(t => t.classList.toggle('active', t.dataset.view === id));
    dom.views.forEach(v => v.classList.toggle('active', v.id === id));
    if (id === 'logsView') {
      void refreshLogs();
    }
    if (id === 'performanceView') {
      renderPerformance();
    }
  }

  function buildCommandPayload() {
    return {
      name: dom.fName.value.trim(),
      command: dom.fCommand.value.trim(),
      workingDirectory: dom.fWorkingDirectory.value.trim(),
      autoRestart: dom.fAutoRestart.checked,
      runOnStart: dom.fRunOnStart.checked,
      usePowerShell: dom.fUsePowerShell.checked,
      runEveryEnabled: dom.fRunEveryEnabled.checked,
      runEveryInterval: Number(dom.fRunEveryInterval.value || '5'),
      runEveryUnit: toUnit(dom.fRunEveryUnit.value),
      restartEveryEnabled: dom.fRestartEveryEnabled.checked,
      restartEveryInterval: Number(dom.fRestartEveryInterval.value || '5'),
      restartEveryUnit: toUnit(dom.fRestartEveryUnit.value)
    };
  }

  function openCommandDialog(process = null) {
    state.commandEditId = process ? process.id : null;
    dom.commandDialogTitle.textContent = process ? 'Edit Command' : 'New Command';

    dom.fName.value = process ? process.name || '' : '';
    dom.fCommand.value = process ? process.command || '' : '';
    dom.fWorkingDirectory.value = process ? process.workingDirectory || '' : '';
    dom.fAutoRestart.checked = process ? !!process.autoRestart : false;
    dom.fRunOnStart.checked = process ? !!process.runOnStart : false;
    dom.fUsePowerShell.checked = process ? !!process.usePowerShell : false;
    dom.fRunEveryEnabled.checked = process ? !!process.runEveryEnabled : false;
    dom.fRunEveryInterval.value = String(process ? process.runEveryInterval || 5 : 5);
    dom.fRunEveryUnit.value = toUnit(process ? process.runEveryUnit : 'minutes');
    dom.fRestartEveryEnabled.checked = process ? !!process.restartEveryEnabled : false;
    dom.fRestartEveryInterval.value = String(process ? process.restartEveryInterval || 5 : 5);
    dom.fRestartEveryUnit.value = toUnit(process ? process.restartEveryUnit : 'minutes');

    dom.commandDialogOverlay.classList.add('open');
  }

  function closeCommandDialog() {
    dom.commandDialogOverlay.classList.remove('open');
  }

  async function saveCommand() {
    const payload = buildCommandPayload();

    if (!payload.name || !payload.command) {
      showToast('Name and command are required.');
      return;
    }

    if (payload.runEveryInterval <= 0 || payload.restartEveryInterval <= 0) {
      showToast('Interval values must be greater than 0.');
      return;
    }

    if (state.commandEditId) {
      await req('/processes/' + encodeURIComponent(state.commandEditId), {
        method: 'PUT',
        body: JSON.stringify(payload)
      });
      showToast('Command updated.');
    } else {
      await req('/processes', {
        method: 'POST',
        body: JSON.stringify(payload)
      });
      showToast('Command created.');
    }

    closeCommandDialog();
    await refreshProcesses(true);
  }

  async function deleteProcess(processId) {
    if (!confirm('Delete this command?')) return;
    await req('/processes/' + encodeURIComponent(processId), { method: 'DELETE' });
    showToast('Command deleted.');
    await refreshProcesses(true);
  }

  async function processAction(processId, action) {
    await req('/processes/' + encodeURIComponent(processId) + '/actions/' + encodeURIComponent(action), { method: 'POST' });
    await refreshProcesses(true);
  }

  function processTags(p) {
    const tags = [];
    tags.push(p.autoRestart ? 'AutoRestart' : 'No AutoRestart');
    tags.push(p.runOnStart ? 'RunOnStart' : 'Manual Start');
    tags.push(p.usePowerShell ? 'PowerShell' : 'Direct');
    if (p.runEveryEnabled) {
      tags.push('Run every ' + p.runEveryInterval + ' ' + toUnit(p.runEveryUnit));
    }
    if (p.restartEveryEnabled) {
      tags.push('Restart every ' + p.restartEveryInterval + ' ' + toUnit(p.restartEveryUnit));
    }
    return tags;
  }

  function renderProcesses() {
    const runningCount = state.processes.filter(p => p.status === 'Running').length;
    dom.runningChip.textContent = runningCount + ' running';

    if (state.processes.length === 0) {
      dom.processGrid.innerHTML = '<div class="panel muted">No commands configured yet. Create one to get started.</div>';
      return;
    }

    const html = state.processes.map((p) => {
      const tags = processTags(p).map(t => '<span class="tag">' + esc(t) + '</span>').join('');
      const status = '<span class="badge ' + statusClass(p.status) + '">' + esc(p.status) + '</span>';
      const cpu = typeof p.cpuPercent === 'number' ? p.cpuPercent.toFixed(1) + '%' : 'warming up';
      const pid = p.pid ?? '-';
      const threads = typeof p.threadCount === 'number' ? p.threadCount : '-';
      const handles = typeof p.handleCount === 'number' ? p.handleCount : 'n/a';

      return '' +
        '<article class="proc-card" data-id="' + esc(p.id) + '">' +
          '<div class="proc-head">' +
            '<div>' +
              '<h3 class="proc-name">' + esc(p.name) + '</h3>' +
              '<p class="proc-cmd mono">' + esc(p.command) + '</p>' +
            '</div>' +
            status +
          '</div>' +
          '<div class="kv">' +
            '<div><strong>PID</strong><br><span class="mono">' + esc(pid) + '</span></div>' +
            '<div><strong>CPU</strong><br><span>' + esc(cpu) + '</span></div>' +
            '<div><strong>Working Set</strong><br><span>' + esc(fmtBytes(p.workingSetBytes)) + '</span></div>' +
            '<div><strong>Private Memory</strong><br><span>' + esc(fmtBytes(p.privateMemoryBytes)) + '</span></div>' +
            '<div><strong>Threads / Handles</strong><br><span>' + esc(threads + ' / ' + handles) + '</span></div>' +
            '<div><strong>Working Directory</strong><br><span class="mono">' + esc(p.workingDirectory || '-') + '</span></div>' +
          '</div>' +
          '<div class="tag-row">' + tags + '</div>' +
          '<div class="action-row">' +
            '<button data-cmd="start">Start</button>' +
            '<button data-cmd="stop">Stop</button>' +
            '<button data-cmd="restart">Restart</button>' +
            '<button data-cmd="ctrlc" class="alt">Ctrl+C</button>' +
            '<button data-cmd="open-logs" class="alt">Logs</button>' +
            '<button data-cmd="open-perf" class="alt">Perf</button>' +
            '<button data-cmd="edit" class="alt">Edit</button>' +
            '<button data-cmd="delete" class="bad">Delete</button>' +
          '</div>' +
        '</article>';
    }).join('');

    dom.processGrid.innerHTML = html;
  }

  function renderLogsProcessList() {
    if (state.processes.length === 0) {
      dom.logsProcessList.innerHTML = '<div class="muted">No processes available.</div>';
      dom.logsTitle.textContent = 'No process selected';
      dom.logsMeta.textContent = '';
      dom.logsOutput.textContent = 'No process selected.';
      return;
    }

    if (!state.selectedLogsProcessId || !state.processes.some(p => p.id === state.selectedLogsProcessId)) {
      state.selectedLogsProcessId = state.processes[0].id;
    }

    const html = state.processes.map(p => {
      const active = p.id === state.selectedLogsProcessId ? ' active' : '';
      return '' +
        '<button class="process-item' + active + '" data-id="' + esc(p.id) + '">' +
          '<div style="font-weight:700">' + esc(p.name) + '</div>' +
          '<div class="muted" style="font-size:12px">' + esc(p.status) + ' | PID: ' + esc(p.pid ?? '-') + '</div>' +
        '</button>';
    }).join('');

    dom.logsProcessList.innerHTML = html;
  }

  async function refreshLogs() {
    if (getCurrentViewId() !== 'logsView') return;
    if (!state.selectedLogsProcessId) return;

    const tail = Math.max(100, Math.min(200000, Number(dom.logsTailInput.value || '16000')));
    dom.logsTailInput.value = String(tail);

    const proc = state.processes.find(p => p.id === state.selectedLogsProcessId);
    if (proc) {
      dom.logsTitle.textContent = proc.name;
      dom.logsMeta.textContent = proc.status + ' | PID: ' + (proc.pid ?? '-') + ' | Tail: ' + tail;
    }

    try {
      const data = await req('/processes/' + encodeURIComponent(state.selectedLogsProcessId) + '/logs?tail=' + encodeURIComponent(String(tail)));
      dom.logsOutput.textContent = (data.logs || []).join('\n');
      if (dom.logsAutoScroll.checked) {
        dom.logsOutput.scrollTop = dom.logsOutput.scrollHeight;
      }
    } catch (err) {
      dom.logsOutput.textContent = 'Failed to load logs: ' + (err.message || String(err));
    }
  }

  function ensurePerfSelection() {
    if (state.processes.length === 0) {
      state.selectedPerfProcessId = null;
      dom.perfProcessSelect.innerHTML = '<option value="">No processes</option>';
      return;
    }

    if (!state.selectedPerfProcessId || !state.processes.some(p => p.id === state.selectedPerfProcessId)) {
      state.selectedPerfProcessId = state.processes[0].id;
      state.cpuHistory = [];
    }

    const html = state.processes
      .map(p => '<option value="' + esc(p.id) + '"' + (p.id === state.selectedPerfProcessId ? ' selected' : '') + '>' + esc(p.name + ' (' + p.status + ')') + '</option>')
      .join('');
    dom.perfProcessSelect.innerHTML = html;
  }

  function renderPerformance() {
    ensurePerfSelection();

    const p = state.processes.find(x => x.id === state.selectedPerfProcessId);
    if (!p) {
      dom.mStatus.textContent = '-';
      dom.mCpu.textContent = '-';
      dom.mPid.textContent = '-';
      dom.mWorking.textContent = '-';
      dom.mPrivate.textContent = '-';
      dom.mThreads.textContent = '-';
      state.cpuHistory = [];
      drawCpuHistory();
      return;
    }

    dom.mStatus.textContent = p.status || '-';
    dom.mCpu.textContent = typeof p.cpuPercent === 'number' ? p.cpuPercent.toFixed(1) + '%' : 'warming up';
    dom.mPid.textContent = String(p.pid ?? '-');
    dom.mWorking.textContent = fmtBytes(p.workingSetBytes);
    dom.mPrivate.textContent = fmtBytes(p.privateMemoryBytes);
    dom.mThreads.textContent = (p.threadCount ?? '-') + ' / ' + (p.handleCount ?? 'n/a');

    if (typeof p.cpuPercent === 'number') {
      state.cpuHistory.push(Math.max(0, Math.min(100, p.cpuPercent)));
      if (state.cpuHistory.length > CPU_HISTORY_MAX) {
        state.cpuHistory.splice(0, state.cpuHistory.length - CPU_HISTORY_MAX);
      }
    }

    drawCpuHistory();
  }

  function drawCpuHistory() {
    const canvas = dom.cpuCanvas;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const width = canvas.width;
    const height = canvas.height;
    ctx.clearRect(0, 0, width, height);

    ctx.fillStyle = '#07131d';
    ctx.fillRect(0, 0, width, height);

    ctx.strokeStyle = 'rgba(112, 150, 178, 0.28)';
    ctx.lineWidth = 1;
    for (let i = 0; i <= 5; i++) {
      const y = Math.round((height - 1) * i / 5);
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(width, y);
      ctx.stroke();
    }

    const values = state.cpuHistory;
    if (values.length < 2) {
      ctx.fillStyle = 'rgba(189, 220, 245, 0.8)';
      ctx.font = '12px "JetBrains Mono"';
      ctx.fillText('Collecting samples...', 10, 20);
      return;
    }

    const step = width / Math.max(values.length - 1, 1);
    ctx.beginPath();
    for (let i = 0; i < values.length; i++) {
      const x = i * step;
      const y = height - (values[i] / 100) * height;
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    }
    ctx.strokeStyle = '#22d2a2';
    ctx.lineWidth = 2;
    ctx.stroke();

    const last = values[values.length - 1];
    const lx = (values.length - 1) * step;
    const ly = height - (last / 100) * height;
    ctx.beginPath();
    ctx.arc(lx, ly, 3.2, 0, Math.PI * 2);
    ctx.fillStyle = '#27f6b9';
    ctx.fill();

    ctx.fillStyle = 'rgba(225, 244, 255, 0.9)';
    ctx.font = '12px "JetBrains Mono"';
    ctx.fillText(last.toFixed(1) + '%', Math.max(8, lx - 34), Math.max(14, ly - 8));
  }

  async function refreshProcesses(showFeedback = false) {
    if (state.isBusy) return;
    state.isBusy = true;

    try {
      setRefreshChip('refreshing...');
      const data = await req('/processes');
      state.processes = (data && Array.isArray(data.processes)) ? data.processes : [];
      renderProcesses();
      renderLogsProcessList();
      renderPerformance();

      if (getCurrentViewId() === 'logsView') {
        await refreshLogs();
      }

      if (showFeedback) {
        showToast('Updated.');
      }
      setRefreshChip('updated ' + new Date().toLocaleTimeString());
    } catch (err) {
      if (err.status === 401) {
        signOut(true);
        return;
      }
      setRefreshChip('refresh failed');
      showToast('Refresh failed: ' + (err.message || String(err)));
    } finally {
      state.isBusy = false;
    }
  }

  function startAutoRefresh() {
    stopAutoRefresh();
    state.processTimer = setInterval(() => {
      void refreshProcesses(false);
    }, REFRESH_MS);

    state.logTimer = setInterval(() => {
      if (getCurrentViewId() === 'logsView' && dom.logsAutoRefresh.checked) {
        void refreshLogs();
      }
    }, LOG_REFRESH_MS);
  }

  function stopAutoRefresh() {
    if (state.processTimer) clearInterval(state.processTimer);
    if (state.logTimer) clearInterval(state.logTimer);
    state.processTimer = null;
    state.logTimer = null;
  }

  async function signIn() {
    setLoginError('');
    state.password = dom.loginPassword.value.trim();

    try {
      await req('/processes');

      if (dom.rememberPwd.checked) {
        localStorage.setItem('cmdhub.remote.password', state.password);
      } else {
        localStorage.removeItem('cmdhub.remote.password');
      }

      dom.loginScreen.classList.add('hidden');
      dom.appRoot.classList.remove('hidden');
      await refreshProcesses(true);
      startAutoRefresh();
    } catch (err) {
      state.password = '';
      setLoginError('Authentication failed. Check your password and try again.');
    }
  }

  function signOut(expired = false) {
    stopAutoRefresh();
    state.password = '';
    state.processes = [];
    state.cpuHistory = [];
    state.selectedLogsProcessId = null;
    state.selectedPerfProcessId = null;
    dom.appRoot.classList.add('hidden');
    dom.loginScreen.classList.remove('hidden');
    dom.loginPassword.value = '';
    setLoginError(expired ? 'Session expired or unauthorized. Sign in again.' : '');
    if (!dom.rememberPwd.checked) {
      localStorage.removeItem('cmdhub.remote.password');
    }
  }

  function bindEvents() {
    dom.loginBtn.addEventListener('click', () => void signIn());
    dom.loginPassword.addEventListener('keydown', (ev) => {
      if (ev.key === 'Enter') {
        ev.preventDefault();
        void signIn();
      }
    });

    dom.logoutBtn.addEventListener('click', () => signOut(false));

    dom.tabs.forEach((tab) => {
      tab.addEventListener('click', () => switchView(tab.dataset.view));
    });

    dom.refreshProcessesBtn.addEventListener('click', () => void refreshProcesses(true));
    dom.newCommandBtn.addEventListener('click', () => openCommandDialog(null));

    dom.processGrid.addEventListener('click', (ev) => {
      const button = ev.target.closest('button[data-cmd]');
      if (!button) return;

      const card = ev.target.closest('.proc-card');
      if (!card) return;
      const id = card.getAttribute('data-id');
      if (!id) return;

      const cmd = button.getAttribute('data-cmd');
      const proc = state.processes.find(p => p.id === id);
      if (!proc) return;

      if (cmd === 'edit') {
        openCommandDialog(proc);
      } else if (cmd === 'delete') {
        void deleteProcess(id);
      } else if (cmd === 'open-logs') {
        state.selectedLogsProcessId = id;
        switchView('logsView');
        renderLogsProcessList();
        void refreshLogs();
      } else if (cmd === 'open-perf') {
        state.selectedPerfProcessId = id;
        state.cpuHistory = [];
        switchView('performanceView');
        renderPerformance();
      } else {
        void processAction(id, cmd);
      }
    });

    dom.logsProcessList.addEventListener('click', (ev) => {
      const button = ev.target.closest('.process-item[data-id]');
      if (!button) return;
      state.selectedLogsProcessId = button.getAttribute('data-id');
      renderLogsProcessList();
      void refreshLogs();
    });

    dom.refreshLogsBtn.addEventListener('click', () => void refreshLogs());

    dom.clearLogsBtn.addEventListener('click', async () => {
      if (!state.selectedLogsProcessId) return;
      await processAction(state.selectedLogsProcessId, 'clear-logs');
      await refreshLogs();
      showToast('Logs cleared.');
    });

    dom.copyLogsBtn.addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(dom.logsOutput.textContent || '');
        showToast('Logs copied to clipboard.');
      } catch {
        showToast('Clipboard write failed.');
      }
    });

    dom.perfProcessSelect.addEventListener('change', () => {
      state.selectedPerfProcessId = dom.perfProcessSelect.value || null;
      state.cpuHistory = [];
      renderPerformance();
    });

    dom.refreshPerfBtn.addEventListener('click', () => {
      void refreshProcesses(true);
    });

    dom.cancelCommandBtn.addEventListener('click', closeCommandDialog);
    dom.commandDialogOverlay.addEventListener('click', (ev) => {
      if (ev.target === dom.commandDialogOverlay) {
        closeCommandDialog();
      }
    });

    dom.saveCommandBtn.addEventListener('click', async () => {
      try {
        await saveCommand();
      } catch (err) {
        showToast(err.message || String(err));
      }
    });
  }

  function initialize() {
    bindEvents();

    const remembered = localStorage.getItem('cmdhub.remote.password') || '';
    if (remembered) {
      dom.loginPassword.value = remembered;
      dom.rememberPwd.checked = true;
      void signIn();
    }
  }

  initialize();
</script>
</body>
</html>
""";
    }

    private static void TryWritePlainText(HttpListenerResponse response, int statusCode, string text)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            response.StatusCode = statusCode;
            response.ContentType = "text/plain; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }
        catch
        {
            // Best effort.
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
