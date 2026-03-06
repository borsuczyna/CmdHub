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
    :root { --bg:#0f1115; --card:#171a21; --line:#2b2f3a; --txt:#e8ebf2; --muted:#9ea7b7; --ok:#4caf50; --warn:#ffb300; --bad:#f44336; --accent:#3aa4ff; }
    body { margin:0; background:linear-gradient(180deg,#111521,#0d1018); color:var(--txt); font:14px/1.4 Segoe UI,Arial,sans-serif; }
    .wrap { max-width:1200px; margin:0 auto; padding:18px; }
    .card { background:var(--card); border:1px solid var(--line); border-radius:10px; padding:14px; margin-bottom:12px; }
    h1 { margin:0 0 12px; font-size:20px; }
    h2 { margin:0 0 10px; font-size:15px; color:#cfd6e6; }
    .row { display:flex; gap:10px; flex-wrap:wrap; align-items:center; }
    input,select,textarea { background:#11141b; color:var(--txt); border:1px solid #394055; border-radius:6px; padding:8px; }
    input[type=checkbox] { transform:scale(1.1); }
    button { background:#1f6fb2; color:#fff; border:none; border-radius:6px; padding:8px 10px; cursor:pointer; }
    button.alt { background:#384053; }
    button.bad { background:#8f2e2e; }
    table { width:100%; border-collapse:collapse; }
    th,td { border-bottom:1px solid #2a2f3d; padding:8px 6px; text-align:left; vertical-align:top; }
    th { color:#9ea7b7; font-size:12px; text-transform:uppercase; letter-spacing:.4px; }
    .pill { padding:2px 8px; border-radius:999px; font-size:12px; }
    .run { background:#18351b; color:#7ee083; }
    .stop { background:#31353f; color:#aeb6c8; }
    .crash { background:#402224; color:#ff7b7b; }
    .muted { color:var(--muted); }
    .logs { background:#0a0d13; border:1px solid #2d3342; border-radius:8px; padding:8px; height:220px; overflow:auto; white-space:pre-wrap; font:12px Consolas,monospace; }
    .grid2 { display:grid; grid-template-columns:repeat(2,minmax(220px,1fr)); gap:8px; }
    @media (max-width:900px){ .grid2 { grid-template-columns:1fr; } table, thead, tbody, th, td, tr { display:block; } th { display:none; } td { border:none; padding:5px 0; } tr { border-bottom:1px solid #2a2f3d; padding:10px 0; } }
  </style>
</head>
<body>
  <div class="wrap">
    <div class="card">
      <h1>CmdHub Remote Control Panel</h1>
      <div class="row">
        <label>Password</label>
        <input id="pwd" type="password" placeholder="Control panel password" style="min-width:260px" />
        <button onclick="reloadAll()">Connect</button>
        <span class="muted">API: <span id="apiBase"></span></span>
      </div>
    </div>

    <div class="card">
      <div class="row" style="justify-content:space-between">
        <h2>Processes</h2>
        <button class="alt" onclick="reloadAll()">Refresh</button>
      </div>
      <table>
        <thead><tr><th>Name</th><th>Status</th><th>Usage</th><th>Command</th><th>Actions</th></tr></thead>
        <tbody id="procBody"></tbody>
      </table>
    </div>

    <div class="card">
      <h2>New Command</h2>
      <div class="grid2">
        <input id="n_name" placeholder="Name" />
        <input id="n_command" placeholder="Command" />
        <input id="n_workingDirectory" placeholder="Working Directory" />
        <input id="n_runEveryInterval" placeholder="Run every interval" value="5" />
        <input id="n_runEveryUnit" placeholder="Run every unit (seconds/minutes/hours)" value="minutes" />
        <input id="n_restartEveryInterval" placeholder="Restart every interval" value="5" />
        <input id="n_restartEveryUnit" placeholder="Restart every unit (seconds/minutes/hours)" value="minutes" />
      </div>
      <div class="row" style="margin-top:8px">
        <label><input id="n_autoRestart" type="checkbox" /> AutoRestart</label>
        <label><input id="n_runOnStart" type="checkbox" /> RunOnStart</label>
        <label><input id="n_usePowerShell" type="checkbox" /> UsePowerShell</label>
        <label><input id="n_runEveryEnabled" type="checkbox" /> RunEveryEnabled</label>
        <label><input id="n_restartEveryEnabled" type="checkbox" /> RestartEveryEnabled</label>
        <button onclick="createCommand()">Create</button>
      </div>
    </div>

    <div class="card">
      <h2>Logs</h2>
      <div class="row">
        <select id="logProc"></select>
        <input id="logTail" value="16000" style="width:100px" />
        <button onclick="loadLogs()">Load Logs</button>
      </div>
      <div id="logs" class="logs" style="margin-top:8px"></div>
    </div>
  </div>

<script>
const apiBase = `${location.protocol}//${location.hostname}:{{apiPort}}/api`;
document.getElementById('apiBase').textContent = apiBase;

function pass(){ return document.getElementById('pwd').value.trim(); }
function headers(){ return { 'Content-Type':'application/json', 'X-CmdHub-Password': pass() }; }
async function req(url, opts={}){
  const o = Object.assign({ headers: headers() }, opts);
  const r = await fetch(url, o);
  const t = await r.text();
  let j = null;
  try { j = t ? JSON.parse(t) : null; } catch { }
  if(!r.ok) throw new Error((j && j.error) ? j.error : `HTTP ${r.status}`);
  return j;
}

function rowStatus(s){
  const cls = s === 'Running' ? 'run' : (s === 'Crashed' ? 'crash' : 'stop');
  return `<span class="pill ${cls}">${s}</span>`;
}

async function action(id,name){
  await req(`${apiBase}/processes/${id}/actions/${name}`, { method:'POST' });
  await reloadAll();
}

async function del(id){
  if(!confirm('Delete this command?')) return;
  await req(`${apiBase}/processes/${id}`, { method:'DELETE' });
  await reloadAll();
}

async function editPrompt(p){
  const name = prompt('Name', p.name); if(name === null) return;
  const command = prompt('Command', p.command); if(command === null) return;
  const workingDirectory = prompt('Working Directory', p.workingDirectory || ''); if(workingDirectory === null) return;
  const payload = {
    name, command, workingDirectory,
    autoRestart: p.autoRestart,
    runOnStart: p.runOnStart,
    usePowerShell: p.usePowerShell,
    runEveryEnabled: p.runEveryEnabled,
    runEveryInterval: p.runEveryInterval,
    runEveryUnit: p.runEveryUnit,
    restartEveryEnabled: p.restartEveryEnabled,
    restartEveryInterval: p.restartEveryInterval,
    restartEveryUnit: p.restartEveryUnit
  };
  await req(`${apiBase}/processes/${p.id}`, { method:'PUT', body: JSON.stringify(payload) });
  await reloadAll();
}

async function createCommand(){
  const payload = {
    name: document.getElementById('n_name').value,
    command: document.getElementById('n_command').value,
    workingDirectory: document.getElementById('n_workingDirectory').value,
    autoRestart: document.getElementById('n_autoRestart').checked,
    runOnStart: document.getElementById('n_runOnStart').checked,
    usePowerShell: document.getElementById('n_usePowerShell').checked,
    runEveryEnabled: document.getElementById('n_runEveryEnabled').checked,
    runEveryInterval: Number(document.getElementById('n_runEveryInterval').value || '5'),
    runEveryUnit: document.getElementById('n_runEveryUnit').value || 'minutes',
    restartEveryEnabled: document.getElementById('n_restartEveryEnabled').checked,
    restartEveryInterval: Number(document.getElementById('n_restartEveryInterval').value || '5'),
    restartEveryUnit: document.getElementById('n_restartEveryUnit').value || 'minutes'
  };
  await req(`${apiBase}/processes`, { method:'POST', body: JSON.stringify(payload) });
  await reloadAll();
}

async function reloadAll(){
  try{
    const data = await req(`${apiBase}/processes`);
    const body = document.getElementById('procBody');
    body.innerHTML = '';
    const sel = document.getElementById('logProc');
    sel.innerHTML = '';

    for(const p of data.processes){
      const tr = document.createElement('tr');
      tr.innerHTML = `
        <td><b>${p.name}</b><div class="muted">${p.id}</div></td>
        <td>${rowStatus(p.status)}<div class="muted">PID: ${p.pid ?? '-'}</div></td>
        <td>
          CPU: ${p.cpuPercent ?? '-'}%<br/>
          RAM: ${p.workingSetBytes ?? '-'} bytes<br/>
          Private: ${p.privateMemoryBytes ?? '-'}
        </td>
        <td class="muted">${p.command}</td>
        <td>
          <div class="row">
            <button onclick="action('${p.id}','start')">Start</button>
            <button onclick="action('${p.id}','stop')">Stop</button>
            <button onclick="action('${p.id}','restart')">Restart</button>
            <button onclick="action('${p.id}','ctrlc')">Ctrl+C</button>
            <button class="alt" onclick='editPrompt(${JSON.stringify(p).replace(/'/g, "&#39;")})'>Edit</button>
            <button class="bad" onclick="del('${p.id}')">Delete</button>
          </div>
        </td>`;
      body.appendChild(tr);

      const opt = document.createElement('option');
      opt.value = p.id;
      opt.textContent = `${p.name} (${p.status})`;
      sel.appendChild(opt);
    }
  }catch(err){
    alert(err.message || String(err));
  }
}

async function loadLogs(){
  const id = document.getElementById('logProc').value;
  if(!id) return;
  const tail = document.getElementById('logTail').value || '16000';
  try{
    const data = await req(`${apiBase}/processes/${id}/logs?tail=${encodeURIComponent(tail)}`);
    document.getElementById('logs').textContent = (data.logs || []).join('\n');
  }catch(err){
    alert(err.message || String(err));
  }
}
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
