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
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>CmdHub Control Panel</title>
  <style>
    :root {
      --bg:#0f1115; --surface:#171a21; --surface2:#1e2230; --border:#2b2f3a;
      --text:#e8ebf2; --muted:#9ea7b7; --ok:#4caf50; --warn:#ffb300;
      --bad:#f44336; --accent:#3aa4ff; --accent-h:#5bb6ff; --sw:220px;
    }
    *, *::before, *::after { box-sizing:border-box; }
    body { margin:0; background:var(--bg); color:var(--text); font:14px/1.5 'Segoe UI',system-ui,Arial,sans-serif; min-height:100vh; }
    /* Login */
    #loginScreen { display:flex; align-items:center; justify-content:center; min-height:100vh; padding:20px; }
    .lcard { background:var(--surface); border:1px solid var(--border); border-radius:14px; padding:40px; width:100%; max-width:420px; text-align:center; }
    .lcard .logo { font-size:38px; margin-bottom:8px; }
    .lcard h1 { font-size:22px; font-weight:700; margin:0 0 4px; }
    .lcard .sub { color:var(--muted); font-size:13px; margin-bottom:28px; }
    .lfield { width:100%; background:#0d1018; color:var(--text); border:1px solid var(--border); border-radius:8px; padding:12px 14px; font-size:15px; font-family:inherit; margin-bottom:14px; outline:none; transition:border-color .15s; }
    .lfield:focus { border-color:var(--accent); }
    .lbtn { width:100%; background:var(--accent); color:#fff; border:none; border-radius:8px; padding:12px; font-size:15px; font-weight:600; cursor:pointer; transition:background .15s; font-family:inherit; }
    .lbtn:hover { background:var(--accent-h); }
    .lbtn:disabled { opacity:.6; cursor:not-allowed; }
    .lerr { color:var(--bad); font-size:13px; margin-top:10px; min-height:18px; }
    .lnote { color:var(--muted); font-size:12px; margin-top:14px; }
    /* App */
    #app { display:none; min-height:100vh; }
    #app.vis { display:flex; }
    /* Sidebar */
    .sidebar { width:var(--sw); min-height:100vh; background:var(--surface); border-right:1px solid var(--border); display:flex; flex-direction:column; position:fixed; top:0; left:0; bottom:0; z-index:50; }
    .sb-brand { padding:20px 18px 14px; border-bottom:1px solid var(--border); display:flex; align-items:center; gap:10px; }
    .sb-brand-icon { font-size:24px; line-height:1; }
    .sb-brand-text { font-size:16px; font-weight:700; }
    .sb-brand-sub { font-size:11px; color:var(--muted); }
    .sb-nav { flex:1; padding:10px 0; }
    .nav-item { display:flex; align-items:center; gap:12px; padding:10px 18px; color:var(--muted); cursor:pointer; font-size:14px; font-weight:500; border-left:3px solid transparent; transition:all .15s; user-select:none; }
    .nav-item:hover { color:var(--text); background:rgba(255,255,255,.04); }
    .nav-item.active { color:var(--accent); border-left-color:var(--accent); background:rgba(58,164,255,.08); }
    .nav-icon { font-size:17px; width:22px; text-align:center; flex-shrink:0; }
    .sb-footer { padding:12px 16px; border-top:1px solid var(--border); }
    .sb-footer-info { font-size:11px; color:var(--muted); margin-bottom:8px; }
    .logout-btn { width:100%; background:transparent; border:1px solid var(--border); color:var(--muted); border-radius:6px; padding:7px; font-size:13px; cursor:pointer; transition:all .15s; font-family:inherit; }
    .logout-btn:hover { border-color:var(--bad); color:var(--bad); }
    /* Main */
    .main { margin-left:var(--sw); flex:1; min-height:100vh; display:flex; flex-direction:column; }
    .topbar { background:var(--surface); border-bottom:1px solid var(--border); padding:12px 20px; display:flex; align-items:center; justify-content:space-between; gap:12px; position:sticky; top:0; z-index:40; }
    .topbar-title { font-size:16px; font-weight:700; }
    .topbar-actions { display:flex; align-items:center; gap:8px; }
    .dot { width:8px; height:8px; border-radius:50%; background:var(--ok); display:inline-block; margin-right:4px; }
    .dot.off { background:var(--bad); }
    .page { padding:20px; display:none; flex:1; }
    .page.active { display:block; }
    /* Card */
    .card { background:var(--surface); border:1px solid var(--border); border-radius:10px; padding:16px; margin-bottom:14px; }
    .card-hdr { display:flex; align-items:center; justify-content:space-between; margin-bottom:14px; gap:10px; flex-wrap:wrap; }
    .card-title { font-size:15px; font-weight:600; margin:0; }
    /* Buttons */
    .btn { background:#1f6fb2; color:#fff; border:none; border-radius:6px; padding:7px 12px; font-size:13px; cursor:pointer; font-family:inherit; transition:background .15s,opacity .15s; white-space:nowrap; }
    .btn:hover { background:#2580c8; }
    .btn:disabled { opacity:.5; cursor:not-allowed; }
    .btn-sm { padding:4px 9px; font-size:12px; border-radius:5px; }
    .btn-alt { background:var(--surface2); border:1px solid var(--border); color:var(--text); }
    .btn-alt:hover { background:#263040; }
    .btn-ok { background:#1d5c27; } .btn-ok:hover { background:#226b2e; }
    .btn-warn { background:#7a5800; } .btn-warn:hover { background:#8a6400; }
    .btn-bad { background:#8f2e2e; } .btn-bad:hover { background:#a03535; }
    .btn-ghost { background:transparent; border:1px solid var(--border); color:var(--muted); }
    .btn-ghost:hover { border-color:var(--accent); color:var(--accent); }
    /* Inputs */
    input[type=text],input[type=password],input[type=number],select,textarea { background:#0d1018; color:var(--text); border:1px solid var(--border); border-radius:6px; padding:8px 10px; font-size:13px; font-family:inherit; outline:none; transition:border-color .15s; }
    input[type=text]:focus,input[type=password]:focus,input[type=number]:focus,select:focus,textarea:focus { border-color:var(--accent); }
    input[type=checkbox] { accent-color:var(--accent); width:15px; height:15px; cursor:pointer; }
    label { display:flex; align-items:center; gap:6px; cursor:pointer; font-size:13px; color:var(--muted); user-select:none; }
    /* Table */
    .tbl-wrap { overflow-x:auto; }
    table { width:100%; border-collapse:collapse; min-width:560px; }
    th { color:var(--muted); font-size:11px; font-weight:600; text-transform:uppercase; letter-spacing:.5px; padding:8px 10px; border-bottom:1px solid var(--border); text-align:left; white-space:nowrap; }
    td { padding:10px; border-bottom:1px solid rgba(43,47,58,.6); vertical-align:middle; }
    tr:last-child td { border-bottom:none; }
    tr:hover td { background:rgba(255,255,255,.02); }
    /* Pills */
    .pill { display:inline-flex; align-items:center; gap:5px; padding:3px 10px; border-radius:999px; font-size:12px; font-weight:600; white-space:nowrap; }
    .pill::before { content:''; width:6px; height:6px; border-radius:50%; display:block; }
    .p-run { background:rgba(76,175,80,.15); color:#7ee083; border:1px solid rgba(76,175,80,.25); }
    .p-run::before { background:#4caf50; animation:pulse 1.5s infinite; }
    .p-stop { background:rgba(158,167,183,.08); color:#aeb6c8; border:1px solid rgba(158,167,183,.15); }
    .p-stop::before { background:#6b7384; }
    .p-crash { background:rgba(244,67,54,.12); color:#ff7b7b; border:1px solid rgba(244,67,54,.22); }
    .p-crash::before { background:var(--bad); }
    @keyframes pulse { 0%,100%{opacity:1}50%{opacity:.4} }
    /* Log box */
    .logbox { background:#060810; border:1px solid #1e2436; border-radius:8px; padding:12px; font:12px/1.6 'Cascadia Code','Consolas','Courier New',monospace; white-space:pre-wrap; word-break:break-all; overflow-y:auto; color:#d4dbe9; }
    /* Performance */
    .perf-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(280px,1fr)); gap:14px; }
    .pcrd { background:var(--surface); border:1px solid var(--border); border-radius:10px; padding:14px; }
    .pcrd-title { font-size:13px; font-weight:600; margin-bottom:12px; display:flex; align-items:center; gap:8px; }
    .pmetrics { display:grid; grid-template-columns:1fr 1fr; gap:8px; margin-bottom:12px; }
    .pm { background:rgba(0,0,0,.25); border-radius:6px; padding:8px; }
    .pm-lbl { font-size:10px; color:var(--muted); text-transform:uppercase; letter-spacing:.4px; margin-bottom:2px; }
    .pm-val { font-size:16px; font-weight:700; }
    .pm-val.ac { color:var(--accent); } .pm-val.ok { color:var(--ok); } .pm-val.wn { color:var(--warn); } .pm-val.bd { color:var(--bad); }
    .spark-wrap { height:50px; position:relative; }
    .spark-wrap canvas { width:100%; height:100%; }
    /* Form */
    .fgrid { display:grid; grid-template-columns:repeat(auto-fill,minmax(220px,1fr)); gap:12px; margin-bottom:14px; }
    .ff { display:flex; flex-direction:column; gap:5px; }
    .flbl { font-size:12px; color:var(--muted); font-weight:500; }
    .ff input,.ff select { width:100%; }
    .fchecks { display:flex; flex-wrap:wrap; gap:14px; margin-bottom:14px; }
    .fbox { background:rgba(0,0,0,.2); border:1px solid var(--border); border-radius:8px; padding:14px; margin-bottom:14px; }
    /* Modal */
    .overlay { display:none; position:fixed; inset:0; background:rgba(0,0,0,.65); z-index:200; align-items:center; justify-content:center; padding:20px; }
    .overlay.open { display:flex; }
    .modal { background:var(--surface); border:1px solid var(--border); border-radius:12px; width:100%; max-width:600px; max-height:90vh; overflow-y:auto; display:flex; flex-direction:column; }
    .mhdr { padding:16px 20px; border-bottom:1px solid var(--border); display:flex; align-items:center; justify-content:space-between; }
    .mtitle { font-size:16px; font-weight:700; }
    .mclose { background:none; border:none; color:var(--muted); font-size:20px; cursor:pointer; line-height:1; padding:2px 6px; border-radius:4px; }
    .mclose:hover { color:var(--text); background:rgba(255,255,255,.06); }
    .mbody { padding:20px; flex:1; }
    .mfooter { padding:12px 20px; border-top:1px solid var(--border); display:flex; justify-content:flex-end; gap:8px; }
    /* Misc */
    .muted { color:var(--muted); } .tsm { font-size:12px; } .tmono { font-family:'Consolas',monospace; }
    .row { display:flex; gap:8px; flex-wrap:wrap; align-items:center; }
    .mt8 { margin-top:8px; } .mb8 { margin-bottom:8px; } .wf { width:100%; }
    .spin { display:inline-block; animation:spin .6s linear infinite; } @keyframes spin { to{transform:rotate(360deg)} }
    .ri { font-size:12px; color:var(--muted); display:flex; align-items:center; gap:5px; }
    /* Toggle */
    .tog { display:flex; align-items:center; gap:6px; font-size:12px; color:var(--muted); cursor:pointer; user-select:none; }
    .tswitch { position:relative; width:32px; height:17px; flex-shrink:0; }
    .tswitch input { opacity:0; width:0; height:0; position:absolute; }
    .ttrack { position:absolute; inset:0; background:var(--border); border-radius:999px; transition:background .2s; cursor:pointer; }
    .ttrack::after { content:''; position:absolute; width:11px; height:11px; border-radius:50%; background:#fff; top:3px; left:3px; transition:transform .2s; }
    .tswitch input:checked + .ttrack { background:var(--accent); }
    .tswitch input:checked + .ttrack::after { transform:translateX(15px); }
    /* Toast */
    #tc { position:fixed; bottom:20px; right:20px; z-index:9999; display:flex; flex-direction:column; gap:8px; }
    .toast { background:var(--surface2); border:1px solid var(--border); border-radius:8px; padding:10px 16px; font-size:13px; max-width:340px; box-shadow:0 4px 20px rgba(0,0,0,.4); animation:tin .2s ease; display:flex; align-items:center; gap:8px; }
    .toast.ok { border-left:3px solid var(--ok); } .toast.err { border-left:3px solid var(--bad); } .toast.inf { border-left:3px solid var(--accent); }
    @keyframes tin { from{opacity:0;transform:translateY(10px)} to{opacity:1;transform:translateY(0)} }
    /* Empty state */
    .empty { text-align:center; padding:48px 20px; color:var(--muted); }
    .empty-icon { font-size:40px; margin-bottom:12px; }
    /* Mobile nav */
    .mob-nav { display:none; position:fixed; bottom:0; left:0; right:0; background:var(--surface); border-top:1px solid var(--border); z-index:60; }
    .mob-nav-items { display:flex; justify-content:space-around; }
    .mni { flex:1; display:flex; flex-direction:column; align-items:center; gap:3px; padding:10px 6px; cursor:pointer; color:var(--muted); font-size:10px; transition:color .15s; user-select:none; }
    .mni .nav-icon { font-size:20px; width:auto; }
    .mni.active { color:var(--accent); }
    /* Proc */
    .proc-name { font-weight:600; font-size:14px; }
    .proc-id { font-size:11px; color:var(--muted); font-family:'Consolas',monospace; margin-top:2px; }
    .pact { display:flex; gap:5px; flex-wrap:wrap; }
    .ubar-w { height:4px; background:var(--border); border-radius:2px; margin-top:4px; overflow:hidden; }
    .ubar { height:100%; border-radius:2px; background:var(--accent); transition:width .4s; }
    .ubar.wn { background:var(--warn); } .ubar.bd { background:var(--bad); }
    /* Responsive */
    @media(max-width:768px){
      .sidebar{display:none} .mob-nav{display:block}
      .main{margin-left:0;padding-bottom:65px}
      .page{padding:14px} .perf-grid{grid-template-columns:1fr}
      table{min-width:0} .topbar{padding:10px 14px} .fgrid{grid-template-columns:1fr}
    }
  </style>
</head>
<body>
<div id="tc"></div>

<!-- LOGIN -->
<div id="loginScreen">
  <div class="lcard">
    <div class="logo">⚙️</div>
    <h1>CmdHub</h1>
    <label for="lpwd" class="sub">Remote Control Panel</label>
    <input class="lfield" id="lpwd" type="password" placeholder="Enter password (leave empty if none)" autocomplete="current-password" />
    <button class="lbtn" id="lbtn" onclick="doLogin()">Connect</button>
    <div class="lerr" id="lerr"></div>
    <div class="lnote">API endpoint: <span id="lapi" class="tmono"></span></div>
  </div>
</div>

<!-- APP -->
<div id="app">
  <nav class="sidebar">
    <div class="sb-brand">
      <div class="sb-brand-icon">⚙️</div>
      <div>
        <div class="sb-brand-text">CmdHub</div>
        <div class="sb-brand-sub">Control Panel</div>
      </div>
    </div>
    <div class="sb-nav">
      <div class="nav-item active" data-page="processes" onclick="showPage('processes')"><span class="nav-icon">📋</span> Processes</div>
      <div class="nav-item" data-page="logs" onclick="showPage('logs')"><span class="nav-icon">📄</span> Logs</div>
      <div class="nav-item" data-page="performance" onclick="showPage('performance')"><span class="nav-icon">📊</span> Performance</div>
      <div class="nav-item" data-page="newproc" onclick="showPage('newproc')"><span class="nav-icon">➕</span> New Process</div>
    </div>
    <div class="sb-footer">
      <div class="sb-footer-info"><span class="dot" id="cdot"></span><span id="cstatus" role="status" aria-live="polite">Connected</span></div>
      <button class="logout-btn" onclick="doLogout()">🚪 Logout</button>
    </div>
  </nav>
  <nav class="mob-nav">
    <div class="mob-nav-items">
      <div class="mni active" data-page="processes" onclick="showPage('processes')"><span class="nav-icon">📋</span>Processes</div>
      <div class="mni" data-page="logs" onclick="showPage('logs')"><span class="nav-icon">📄</span>Logs</div>
      <div class="mni" data-page="performance" onclick="showPage('performance')"><span class="nav-icon">📊</span>Perf</div>
      <div class="mni" data-page="newproc" onclick="showPage('newproc')"><span class="nav-icon">➕</span>New</div>
    </div>
  </nav>
  <div class="main">
    <div class="topbar">
      <div class="topbar-title" id="ttitle">Processes</div>
      <div class="topbar-actions">
        <div class="ri" id="rind" role="status" aria-live="polite" style="display:none"><span class="spin">⟳</span> Refreshing</div>
        <button class="btn btn-ghost btn-sm" onclick="manualRefresh()">⟳ Refresh</button>
      </div>
    </div>

    <!-- PROCESSES -->
    <div class="page active" id="page-processes">
      <div class="card">
        <div class="card-hdr">
          <h2 class="card-title">Processes</h2>
          <label class="tog"><span class="tswitch"><input type="checkbox" id="par" aria-label="Auto-refresh processes" checked onchange="onPAR()"><span class="ttrack"></span></span>Auto-refresh</label>
        </div>
        <div class="tbl-wrap">
          <table>
            <thead><tr><th>Name</th><th>Status</th><th>Performance</th><th>Command</th><th>Actions</th></tr></thead>
            <tbody id="pbody"><tr><td colspan="5"><div class="empty"><div class="empty-icon">⏳</div>Loading…</div></td></tr></tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- LOGS -->
    <div class="page" id="page-logs">
      <div class="card">
        <div class="card-hdr">
          <h2 class="card-title">Process Logs</h2>
          <label class="tog"><span class="tswitch"><input type="checkbox" id="lar" aria-label="Auto-refresh logs" checked onchange="onLAR()"><span class="ttrack"></span></span>Auto-refresh</label>
        </div>
        <div class="row mb8" style="flex-wrap:wrap;gap:8px">
          <select id="lproc" style="flex:1;min-width:160px" onchange="onLProcChange()"><option value="">— Select process —</option></select>
          <input type="number" id="ltail" value="16000" style="width:90px" title="Max characters" />
          <button class="btn" onclick="loadLogs()">Load</button>
          <button class="btn btn-bad btn-sm" onclick="clearLogs()">🗑 Clear</button>
        </div>
        <div id="lstatus" class="tsm muted mb8" style="min-height:18px"></div>
        <div id="lbox" class="logbox" style="height:520px;max-height:70vh"></div>
      </div>
    </div>

    <!-- PERFORMANCE -->
    <div class="page" id="page-performance">
      <div class="card-hdr" style="margin-bottom:14px;padding:0 4px">
        <h2 class="card-title" style="margin:0">Performance Overview</h2>
        <label class="tog"><span class="tswitch"><input type="checkbox" id="perfar" aria-label="Auto-refresh performance" checked onchange="onPerfAR()"><span class="ttrack"></span></span>Auto-refresh</label>
      </div>
      <div class="perf-grid" id="pgrid"><div class="pcrd"><div class="empty"><div class="empty-icon">⏳</div>Loading…</div></div></div>
    </div>

    <!-- NEW PROCESS -->
    <div class="page" id="page-newproc">
      <div class="card">
        <div class="card-hdr"><h2 class="card-title">Create New Process</h2></div>
        <div class="fgrid">
          <div class="ff"><span class="flbl">Name *</span><input type="text" id="nn" placeholder="My Server" /></div>
          <div class="ff"><span class="flbl">Command *</span><input type="text" id="nc" placeholder="node server.js" /></div>
          <div class="ff" style="grid-column:1/-1"><span class="flbl">Working Directory</span><input type="text" id="nwd" placeholder="C:\projects\server" /></div>
        </div>
        <div class="fchecks">
          <label><input type="checkbox" id="nar" /> Auto Restart</label>
          <label><input type="checkbox" id="nros" /> Run on Start</label>
          <label><input type="checkbox" id="nups" /> Use PowerShell</label>
        </div>
        <div class="fbox">
          <div class="flbl mb8">Run Every (scheduled)</div>
          <div class="row mb8"><label><input type="checkbox" id="nree" /> Enable</label></div>
          <div class="fgrid" style="margin-bottom:0">
            <div class="ff"><span class="flbl">Interval</span><input type="number" id="nrei" value="5" min="1" /></div>
            <div class="ff"><span class="flbl">Unit</span><select id="nreu"><option value="seconds">Seconds</option><option value="minutes" selected>Minutes</option><option value="hours">Hours</option></select></div>
          </div>
        </div>
        <div class="fbox">
          <div class="flbl mb8">Restart Every</div>
          <div class="row mb8"><label><input type="checkbox" id="nrste" /> Enable</label></div>
          <div class="fgrid" style="margin-bottom:0">
            <div class="ff"><span class="flbl">Interval</span><input type="number" id="nrsti" value="5" min="1" /></div>
            <div class="ff"><span class="flbl">Unit</span><select id="nrstu"><option value="seconds">Seconds</option><option value="minutes" selected>Minutes</option><option value="hours">Hours</option></select></div>
          </div>
        </div>
        <div class="row" style="justify-content:flex-end">
          <button class="btn btn-ok" onclick="createCommand()">✓ Create Process</button>
        </div>
      </div>
    </div>
  </div>
</div>

<!-- EDIT MODAL -->
<div class="overlay" id="editModal">
  <div class="modal" role="dialog" aria-modal="true" aria-labelledby="editModalTitle">
    <div class="mhdr">
      <span class="mtitle" id="editModalTitle">Edit Process</span>
      <button class="mclose" onclick="closeEdit()">✕</button>
    </div>
    <div class="mbody">
      <input type="hidden" id="eid" />
      <div class="fgrid">
        <div class="ff"><span class="flbl">Name *</span><input type="text" id="en" /></div>
        <div class="ff"><span class="flbl">Command *</span><input type="text" id="ec" /></div>
        <div class="ff" style="grid-column:1/-1"><span class="flbl">Working Directory</span><input type="text" id="ewd" /></div>
      </div>
      <div class="fchecks">
        <label><input type="checkbox" id="ear" /> Auto Restart</label>
        <label><input type="checkbox" id="eros" /> Run on Start</label>
        <label><input type="checkbox" id="eups" /> Use PowerShell</label>
      </div>
      <div class="fbox">
        <div class="flbl mb8">Run Every</div>
        <div class="row mb8"><label><input type="checkbox" id="eree" /> Enable</label></div>
        <div class="fgrid" style="margin-bottom:0">
          <div class="ff"><span class="flbl">Interval</span><input type="number" id="erei" min="1" /></div>
          <div class="ff"><span class="flbl">Unit</span><select id="ereu"><option value="seconds">Seconds</option><option value="minutes">Minutes</option><option value="hours">Hours</option></select></div>
        </div>
      </div>
      <div class="fbox">
        <div class="flbl mb8">Restart Every</div>
        <div class="row mb8"><label><input type="checkbox" id="erste" /> Enable</label></div>
        <div class="fgrid" style="margin-bottom:0">
          <div class="ff"><span class="flbl">Interval</span><input type="number" id="ersti" min="1" /></div>
          <div class="ff"><span class="flbl">Unit</span><select id="erstu"><option value="seconds">Seconds</option><option value="minutes">Minutes</option><option value="hours">Hours</option></select></div>
        </div>
      </div>
    </div>
    <div class="mfooter">
      <button class="btn btn-alt" onclick="closeEdit()">Cancel</button>
      <button class="btn" onclick="saveEdit()">Save Changes</button>
    </div>
  </div>
</div>

<script>
// ---- Config ----
const AP = {{apiPort}};
const apiBase = `${location.protocol}//${location.hostname}:${AP}/api`;
document.getElementById('lapi').textContent = apiBase;

// ---- Constants ----
const PROC_REFRESH_MS = 3000;
const LOGS_REFRESH_MS = 2000;
const PERF_REFRESH_MS = 2000;
const PERF_HISTORY_SIZE = 60;

// ---- State ----
let pwd = '';
let curPage = 'processes';
let pInt = null, lInt = null, perfInt = null;
let perfHist = {};
let procMap = {}; // id -> process object for safe edit lookup

// ---- Login ----
function doLogin() {
  const p = document.getElementById('lpwd').value;
  document.getElementById('lbtn').disabled = true;
  document.getElementById('lerr').textContent = '';
  const h = { 'Content-Type': 'application/json' };
  if (p.trim()) h['X-CmdHub-Password'] = p.trim();
  fetch(`${apiBase}/processes`, { headers: h })
    .then(r => {
      if (r.status === 401) throw new Error('Invalid password.');
      if (!r.ok) throw new Error(`Server error (${r.status}).`);
      return r.json();
    })
    .then(data => {
      pwd = p;
      try { sessionStorage.setItem('ch_pwd', p); } catch {}
      document.getElementById('loginScreen').style.display = 'none';
      const app = document.getElementById('app');
      app.classList.add('vis');
      startApp(data);
    })
    .catch(err => { document.getElementById('lerr').textContent = err.message || String(err); })
    .finally(() => { document.getElementById('lbtn').disabled = false; });
}
function doLogout() {
  stopAll();
  try { sessionStorage.removeItem('ch_pwd'); } catch {}
  pwd = '';
  document.getElementById('lpwd').value = '';
  document.getElementById('lerr').textContent = '';
  document.getElementById('app').classList.remove('vis');
  document.getElementById('loginScreen').style.display = '';
}
(function() {
  try { const s = sessionStorage.getItem('ch_pwd'); if (s !== null) document.getElementById('lpwd').value = s; } catch {}
})();
document.getElementById('lpwd').addEventListener('keydown', e => { if (e.key === 'Enter') doLogin(); });

// ---- Auth ----
function hdrs() {
  const h = { 'Content-Type': 'application/json' };
  if (pwd.trim()) h['X-CmdHub-Password'] = pwd.trim();
  return h;
}
async function req(url, opts = {}) {
  const o = Object.assign({ headers: hdrs() }, opts);
  const r = await fetch(url, o);
  const t = await r.text();
  let j = null;
  try { j = t ? JSON.parse(t) : null; } catch {}
  if (!r.ok) {
    if (r.status === 401) { toast('Session expired.', 'err'); doLogout(); throw new Error('Unauthorized'); }
    throw new Error((j && j.error) ? j.error : `HTTP ${r.status}`);
  }
  return j;
}

// ---- Toast ----
function toast(msg, type = 'inf', dur = 3500) {
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  el.textContent = msg;
  document.getElementById('tc').appendChild(el);
  setTimeout(() => el.remove(), dur);
}

// ---- App ----
function startApp(initialData) {
  if (initialData) renderProcs(initialData.processes || []);
  updateLogSel(initialData ? initialData.processes || [] : []);
  startPR();
}
function stopAll() { clearInterval(pInt); clearInterval(lInt); clearInterval(perfInt); pInt = lInt = perfInt = null; }

// ---- Navigation ----
const ptitles = { processes:'Processes', logs:'Logs', performance:'Performance', newproc:'New Process' };
function showPage(name) {
  curPage = name;
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.getElementById(`page-${name}`).classList.add('active');
  document.querySelectorAll('.nav-item,.mni').forEach(i => i.classList.toggle('active', i.dataset.page === name));
  document.getElementById('ttitle').textContent = ptitles[name] || name;
  if (name === 'logs') { startLR(); } else { stopLR(); }
  if (name === 'performance') { loadPerf(); startPerfR(); } else { stopPerfR(); }
}

// ---- Auto-refresh ----
function startPR() { if (!document.getElementById('par').checked || pInt) return; pInt = setInterval(loadProcs, PROC_REFRESH_MS); }
function stopPR() { clearInterval(pInt); pInt = null; }
function onPAR() { document.getElementById('par').checked ? (startPR()) : stopPR(); }
function startLR() { if (!document.getElementById('lar').checked || lInt) return; lInt = setInterval(loadLogs, LOGS_REFRESH_MS); }
function stopLR() { clearInterval(lInt); lInt = null; }
function onLAR() { if (document.getElementById('lar').checked) { startLR(); loadLogs(); } else stopLR(); }
function startPerfR() { if (!document.getElementById('perfar').checked || perfInt) return; perfInt = setInterval(loadPerf, PERF_REFRESH_MS); }
function stopPerfR() { clearInterval(perfInt); perfInt = null; }
function onPerfAR() { if (document.getElementById('perfar').checked) { startPerfR(); loadPerf(); } else stopPerfR(); }
function manualRefresh() {
  if (curPage === 'processes') loadProcs();
  else if (curPage === 'logs') loadLogs();
  else if (curPage === 'performance') loadPerf();
}

// ---- Processes ----
async function loadProcs() {
  try {
    showRI(true);
    const data = await req(`${apiBase}/processes`);
    renderProcs(data.processes || []);
    updateLogSel(data.processes || []);
    setConn(true);
  } catch (e) { if (!String(e).includes('Unauthorized')) setConn(false); }
  finally { showRI(false); }
}
function renderProcs(procs) {
  procMap = {};
  for (const p of procs) procMap[p.id] = p;
  const body = document.getElementById('pbody');
  if (!procs.length) { body.innerHTML = '<tr><td colspan="5"><div class="empty"><div class="empty-icon">📭</div>No processes configured.</div></td></tr>'; return; }
  body.innerHTML = procs.map(p => {
    const pc = p.status === 'Running' ? 'p-run' : (p.status === 'Crashed' ? 'p-crash' : 'p-stop');
    const cpu = p.cpuPercent != null ? p.cpuPercent.toFixed(1) : null;
    const ram = p.workingSetBytes != null ? fb(p.workingSetBytes) : null;
    const cb = cpu != null ? (cpu > 80 ? 'bd' : (cpu > 50 ? 'wn' : '')) : '';
    const id = esc(p.id);
    return `<tr>
      <td><div class="proc-name">${esc(p.name)}</div><div class="proc-id">${id}</div></td>
      <td><span class="pill ${pc}">${esc(p.status)}</span>${p.pid != null ? `<div class="tsm muted mt8">PID ${p.pid}</div>` : ''}</td>
      <td style="min-width:110px">
        ${cpu != null ? `<div class="tsm">CPU <b>${cpu}%</b></div><div class="ubar-w"><div class="ubar ${cb}" style="width:${Math.min(cpu,100)}%"></div></div>` : '<span class="muted tsm">—</span>'}
        ${ram != null ? `<div class="tsm muted">${ram}</div>` : ''}
      </td>
      <td class="muted tsm tmono" style="max-width:180px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap" title="${esc(p.command)}">${esc(p.command)}</td>
      <td><div class="pact">
        ${p.status !== 'Running' ? `<button class="btn btn-ok btn-sm" onclick="act('${id}','start')">▶ Start</button>` : ''}
        ${p.status === 'Running' ? `<button class="btn btn-warn btn-sm" onclick="act('${id}','stop')">⏹ Stop</button>` : ''}
        <button class="btn btn-sm" onclick="act('${id}','restart')">↺</button>
        ${p.status === 'Running' ? `<button class="btn btn-alt btn-sm" onclick="act('${id}','ctrlc')">^C</button>` : ''}
        <button class="btn btn-alt btn-sm" onclick="openEditById('${id}')">✎</button>
        <button class="btn btn-bad btn-sm" onclick="delProc('${id}','${esc(p.name)}')">🗑</button>
      </div></td>
    </tr>`;
  }).join('');
}
function updateLogSel(procs) {
  const sel = document.getElementById('lproc');
  const prev = sel.value;
  sel.innerHTML = '<option value="">— Select process —</option>';
  for (const p of procs) {
    const o = document.createElement('option');
    o.value = p.id; o.textContent = `${p.name} (${p.status})`;
    sel.appendChild(o);
  }
  if (prev) sel.value = prev;
}
async function act(id, name) {
  try { await req(`${apiBase}/processes/${id}/actions/${name}`, { method:'POST' }); toast(`Action "${name}" sent.`, 'ok'); await loadProcs(); }
  catch (e) { toast(e.message || String(e), 'err'); }
}
async function delProc(id, name) {
  if (!confirm(`Delete process "${name}"?\nThis cannot be undone.`)) return;
  try { await req(`${apiBase}/processes/${id}`, { method:'DELETE' }); toast(`Deleted "${name}".`, 'ok'); await loadProcs(); }
  catch (e) { toast(e.message || String(e), 'err'); }
}

// ---- Edit Modal ----
function openEditById(id) {
  const p = procMap[id];
  if (!p) { toast('Process not found.', 'err'); return; }
  openEdit(p);
}
function openEdit(p) {
  document.getElementById('eid').value = p.id;
  document.getElementById('en').value = p.name || '';
  document.getElementById('ec').value = p.command || '';
  document.getElementById('ewd').value = p.workingDirectory || '';
  document.getElementById('ear').checked = !!p.autoRestart;
  document.getElementById('eros').checked = !!p.runOnStart;
  document.getElementById('eups').checked = !!p.usePowerShell;
  document.getElementById('eree').checked = !!p.runEveryEnabled;
  document.getElementById('erei').value = p.runEveryInterval || 5;
  document.getElementById('ereu').value = p.runEveryUnit || 'minutes';
  document.getElementById('erste').checked = !!p.restartEveryEnabled;
  document.getElementById('ersti').value = p.restartEveryInterval || 5;
  document.getElementById('erstu').value = p.restartEveryUnit || 'minutes';
  document.getElementById('editModal').classList.add('open');
}
function closeEdit() { document.getElementById('editModal').classList.remove('open'); }
document.getElementById('editModal').addEventListener('click', e => { if (e.target === e.currentTarget) closeEdit(); });
async function saveEdit() {
  const id = document.getElementById('eid').value;
  const pl = {
    name: document.getElementById('en').value.trim(),
    command: document.getElementById('ec').value.trim(),
    workingDirectory: document.getElementById('ewd').value.trim(),
    autoRestart: document.getElementById('ear').checked,
    runOnStart: document.getElementById('eros').checked,
    usePowerShell: document.getElementById('eups').checked,
    runEveryEnabled: document.getElementById('eree').checked,
    runEveryInterval: Number(document.getElementById('erei').value) || 5,
    runEveryUnit: document.getElementById('ereu').value || 'minutes',
    restartEveryEnabled: document.getElementById('erste').checked,
    restartEveryInterval: Number(document.getElementById('ersti').value) || 5,
    restartEveryUnit: document.getElementById('erstu').value || 'minutes'
  };
  if (!pl.name) { toast('Name is required.', 'err'); return; }
  if (!pl.command) { toast('Command is required.', 'err'); return; }
  try {
    await req(`${apiBase}/processes/${id}`, { method:'PUT', body: JSON.stringify(pl) });
    toast('Process updated.', 'ok'); closeEdit(); await loadProcs();
  } catch (e) { toast(e.message || String(e), 'err'); }
}

// ---- Logs ----
function onLProcChange() { document.getElementById('lbox').textContent = ''; document.getElementById('lstatus').textContent = ''; if (document.getElementById('lproc').value) loadLogs(); }
async function loadLogs() {
  const id = document.getElementById('lproc').value;
  if (!id) return;
  const tail = document.getElementById('ltail').value || '16000';
  try {
    const data = await req(`${apiBase}/processes/${id}/logs?tail=${encodeURIComponent(tail)}`);
    const box = document.getElementById('lbox');
    const atBot = box.scrollHeight - box.scrollTop <= box.clientHeight + 40;
    const lines = data.logs || [];
    box.textContent = lines.join('\n');
    if (atBot) box.scrollTop = box.scrollHeight;
    document.getElementById('lstatus').textContent = `${data.name} · ${data.status} · ${lines.length} lines${data.truncated ? ' (truncated)' : ''}`;
  } catch (e) { if (!String(e).includes('Unauthorized')) document.getElementById('lstatus').textContent = `Error: ${e.message}`; }
}
async function clearLogs() {
  const id = document.getElementById('lproc').value;
  if (!id) { toast('Select a process first.', 'inf'); return; }
  if (!confirm('Clear logs for this process?')) return;
  try {
    await req(`${apiBase}/processes/${id}/actions/clear-logs`, { method:'POST' });
    toast('Logs cleared.', 'ok');
    document.getElementById('lbox').textContent = '';
    document.getElementById('lstatus').textContent = '';
    await loadLogs();
  } catch (e) { toast(e.message || String(e), 'err'); }
}

// ---- Performance ----
async function loadPerf() {
  try {
    const data = await req(`${apiBase}/processes`);
    renderPerf(data.processes || []);
    setConn(true);
  } catch (e) { if (!String(e).includes('Unauthorized')) setConn(false); }
}
function renderPerf(procs) {
  const grid = document.getElementById('pgrid');
  const running = procs.filter(p => p.isRunning);
  if (!running.length) {
    grid.innerHTML = '<div class="pcrd"><div class="empty"><div class="empty-icon">💤</div>No running processes.</div></div>';
    return;
  }
  for (const p of running) {
    if (!perfHist[p.id]) perfHist[p.id] = { cpu:[], mem:[] };
    const h = perfHist[p.id];
    if (p.cpuPercent != null) { h.cpu.push(p.cpuPercent); if (h.cpu.length > PERF_HISTORY_SIZE) h.cpu.shift(); }
    if (p.workingSetBytes != null) { h.mem.push(p.workingSetBytes); if (h.mem.length > PERF_HISTORY_SIZE) h.mem.shift(); }
  }
  for (const id of Object.keys(perfHist)) { if (!running.find(p => p.id === id)) delete perfHist[id]; }
  const existing = {};
  grid.querySelectorAll('.pcrd[data-id]').forEach(c => { existing[c.dataset.id] = c; });
  const frag = document.createDocumentFragment();
  for (const p of running) {
    const card = existing[p.id] || mkPerfCard(p.id);
    updPerfCard(card, p);
    frag.appendChild(card);
    delete existing[p.id];
  }
  grid.innerHTML = '';
  grid.appendChild(frag);
}
function mkPerfCard(id) {
  const c = document.createElement('div');
  c.className = 'pcrd'; c.dataset.id = id;
  c.innerHTML = `
    <div class="pcrd-title"><span class="pill p-run" style="font-size:11px;padding:2px 8px"></span><span class="pcrd-nm"></span></div>
    <div class="pmetrics">
      <div class="pm"><div class="pm-lbl">CPU</div><div class="pm-val ac pcrd-cpu">—</div></div>
      <div class="pm"><div class="pm-lbl">RAM</div><div class="pm-val pcrd-ram">—</div></div>
      <div class="pm"><div class="pm-lbl">Threads</div><div class="pm-val pcrd-thr">—</div></div>
      <div class="pm"><div class="pm-lbl">PID</div><div class="pm-val muted tsm pcrd-pid">—</div></div>
    </div>
    <div class="pm-lbl mb8">CPU % (60s)</div>
    <div class="spark-wrap"><canvas class="cc"></canvas></div>
    <div class="pm-lbl mt8 mb8">RAM (60s)</div>
    <div class="spark-wrap"><canvas class="mc"></canvas></div>`;
  return c;
}
function updPerfCard(card, p) {
  card.querySelector('.pcrd-nm').textContent = p.name;
  const cpu = p.cpuPercent != null ? `${p.cpuPercent.toFixed(1)}%` : '—';
  const cpuEl = card.querySelector('.pcrd-cpu');
  cpuEl.textContent = cpu;
  cpuEl.className = `pm-val pcrd-cpu ${p.cpuPercent > 80 ? 'bd' : p.cpuPercent > 50 ? 'wn' : 'ac'}`;
  card.querySelector('.pcrd-ram').textContent = p.workingSetBytes != null ? fb(p.workingSetBytes) : '—';
  card.querySelector('.pcrd-thr').textContent = p.threadCount != null ? p.threadCount : '—';
  card.querySelector('.pcrd-pid').textContent = p.pid != null ? p.pid : '—';
  const h = perfHist[p.id] || { cpu:[], mem:[] };
  spark(card.querySelector('.cc'), h.cpu, '%', [0,100]);
  spark(card.querySelector('.mc'), h.mem, 'MB', null, v => v/1048576);
}
function spark(canvas, data, unit, yr, tf) {
  if (!canvas) return;
  const dpr = window.devicePixelRatio || 1;
  const w = canvas.offsetWidth || canvas.parentElement.clientWidth || 240;
  const h = canvas.offsetHeight || 50;
  canvas.width = w * dpr; canvas.height = h * dpr;
  const ctx = canvas.getContext('2d');
  ctx.scale(dpr, dpr); ctx.clearRect(0,0,w,h);
  if (!data || data.length < 2) {
    ctx.fillStyle='rgba(255,255,255,.05)'; ctx.fillRect(0,0,w,h);
    ctx.fillStyle='#9ea7b7'; ctx.font='10px Segoe UI,Arial,sans-serif'; ctx.textAlign='center';
    ctx.fillText('Collecting data…', w/2, h/2+4); return;
  }
  const vals = tf ? data.map(tf) : data;
  const mn = yr ? yr[0] : Math.min(...vals);
  const mx = yr ? yr[1] : Math.max(...vals);
  const rng = mx - mn || 1;
  ctx.fillStyle='rgba(0,0,0,.3)'; ctx.fillRect(0,0,w,h);
  ctx.strokeStyle='rgba(255,255,255,.05)'; ctx.lineWidth=1;
  ctx.beginPath(); ctx.moveTo(0,h/2); ctx.lineTo(w,h/2); ctx.stroke();
  const pts = vals.map((v,i) => ({ x:(i/(vals.length-1))*w, y:h-((v-mn)/rng)*(h-4)-2 }));
  const g = ctx.createLinearGradient(0,0,0,h);
  g.addColorStop(0,'rgba(58,164,255,.45)'); g.addColorStop(1,'rgba(58,164,255,.02)');
  ctx.beginPath(); ctx.moveTo(pts[0].x,h);
  for (const pt of pts) ctx.lineTo(pt.x,pt.y);
  ctx.lineTo(pts[pts.length-1].x,h); ctx.closePath(); ctx.fillStyle=g; ctx.fill();
  ctx.beginPath(); ctx.moveTo(pts[0].x,pts[0].y);
  for (let i=1;i<pts.length;i++) ctx.lineTo(pts[i].x,pts[i].y);
  ctx.strokeStyle='#3aa4ff'; ctx.lineWidth=1.5; ctx.stroke();
  const last=vals[vals.length-1];
  const ls = unit==='MB' ? `${last.toFixed(1)}MB` : `${last.toFixed(1)}${unit}`;
  ctx.fillStyle='#e8ebf2'; ctx.font='bold 10px Segoe UI,Arial,sans-serif'; ctx.textAlign='right';
  ctx.fillText(ls, w-4, 12);
}

// ---- New Process ----
async function createCommand() {
  const pl = {
    name: document.getElementById('nn').value.trim(),
    command: document.getElementById('nc').value.trim(),
    workingDirectory: document.getElementById('nwd').value.trim(),
    autoRestart: document.getElementById('nar').checked,
    runOnStart: document.getElementById('nros').checked,
    usePowerShell: document.getElementById('nups').checked,
    runEveryEnabled: document.getElementById('nree').checked,
    runEveryInterval: Number(document.getElementById('nrei').value) || 5,
    runEveryUnit: document.getElementById('nreu').value || 'minutes',
    restartEveryEnabled: document.getElementById('nrste').checked,
    restartEveryInterval: Number(document.getElementById('nrsti').value) || 5,
    restartEveryUnit: document.getElementById('nrstu').value || 'minutes'
  };
  if (!pl.name) { toast('Name is required.', 'err'); return; }
  if (!pl.command) { toast('Command is required.', 'err'); return; }
  try {
    await req(`${apiBase}/processes`, { method:'POST', body:JSON.stringify(pl) });
    toast(`Process "${pl.name}" created.`, 'ok');
    ['nn','nc','nwd'].forEach(id => { document.getElementById(id).value=''; });
    ['nar','nros','nups','nree','nrste'].forEach(id => { document.getElementById(id).checked=false; });
    showPage('processes'); await loadProcs();
  } catch (e) { toast(e.message || String(e), 'err'); }
}

// ---- Utils ----
function esc(s) {
  return String(s??'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;').replace(/'/g,'&#39;');
}
function fb(b) {
  if (b==null) return '—';
  if (b<1024) return `${b} B`;
  if (b<1048576) return `${(b/1024).toFixed(1)} KB`;
  if (b<1073741824) return `${(b/1048576).toFixed(1)} MB`;
  return `${(b/1073741824).toFixed(2)} GB`;
}
function setConn(ok) {
  const d=document.getElementById('cdot'); const s=document.getElementById('cstatus');
  if(d) d.className=`dot${ok?'':' off'}`; if(s) s.textContent=ok?'Connected':'Offline';
}
function showRI(on) { document.getElementById('rind').style.display=on?'flex':'none'; }
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
