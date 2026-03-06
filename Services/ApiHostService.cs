using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CmdHub.Models;
using CmdHub.ViewModels;

namespace CmdHub.Services;

public sealed class ApiHostService : IDisposable
{
    private readonly Func<IReadOnlyList<CommandViewModel>> _getCommands;
    private readonly Func<string> _getPassword;
    private readonly Func<RemoteCommandUpsert, OperationResult> _createCommand;
    private readonly Func<Guid, RemoteCommandUpsert, OperationResult> _updateCommand;
    private readonly Func<Guid, OperationResult> _deleteCommand;
    private readonly Func<Guid, OperationResult> _startCommand;
    private readonly Func<Guid, OperationResult> _stopCommand;
    private readonly Func<Guid, OperationResult> _restartCommand;
    private readonly Func<Guid, OperationResult> _ctrlCCommand;
    private readonly Func<Guid, OperationResult> _clearLogsCommand;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly ConcurrentDictionary<Guid, CpuSample> _cpuSamples = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public int Port { get; }
    public bool IsRunning => _listener?.IsListening == true;
    public bool IsLanAccessible { get; private set; }

    public ApiHostService(
        Func<IReadOnlyList<CommandViewModel>> getCommands,
        Func<string> getPassword,
        Func<RemoteCommandUpsert, OperationResult> createCommand,
        Func<Guid, RemoteCommandUpsert, OperationResult> updateCommand,
        Func<Guid, OperationResult> deleteCommand,
        Func<Guid, OperationResult> startCommand,
        Func<Guid, OperationResult> stopCommand,
        Func<Guid, OperationResult> restartCommand,
        Func<Guid, OperationResult> ctrlCCommand,
        Func<Guid, OperationResult> clearLogsCommand,
        int port)
    {
        _getCommands = getCommands ?? throw new ArgumentNullException(nameof(getCommands));
        _getPassword = getPassword ?? throw new ArgumentNullException(nameof(getPassword));
        _createCommand = createCommand ?? throw new ArgumentNullException(nameof(createCommand));
        _updateCommand = updateCommand ?? throw new ArgumentNullException(nameof(updateCommand));
        _deleteCommand = deleteCommand ?? throw new ArgumentNullException(nameof(deleteCommand));
        _startCommand = startCommand ?? throw new ArgumentNullException(nameof(startCommand));
        _stopCommand = stopCommand ?? throw new ArgumentNullException(nameof(stopCommand));
        _restartCommand = restartCommand ?? throw new ArgumentNullException(nameof(restartCommand));
        _ctrlCCommand = ctrlCCommand ?? throw new ArgumentNullException(nameof(ctrlCCommand));
        _clearLogsCommand = clearLogsCommand ?? throw new ArgumentNullException(nameof(clearLogsCommand));
        Port = port;
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
        _cpuSamples.Clear();
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
                    TryWriteError(context.Response, 500, "Internal server error.");
                }
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        response.ContentType = "application/json; charset=utf-8";
        ApplyCorsHeaders(response);

        if (HttpMethods.IsOptions(request.HttpMethod))
        {
            response.StatusCode = 204;
            response.OutputStream.Close();
            return;
        }

        string path = (request.Url?.AbsolutePath ?? "/").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        if (string.Equals(path, "/api/health", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(response, 200, new
            {
                status = "ok",
                baseUrl = $"http://localhost:{Port}/api"
            });
            return;
        }

        if (!IsAuthorized(request))
        {
            await WriteJsonAsync(response, 401, new { error = "Unauthorized." });
            return;
        }

        if (string.Equals(path, "/api/processes", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsGet(request.HttpMethod))
        {
            var processes = _getCommands()
                .Select(BuildProcessDto)
                .ToList();

            await WriteJsonAsync(response, 200, new
            {
                count = processes.Count,
                processes
            });
            return;
        }

        if (string.Equals(path, "/api/processes", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(request.HttpMethod))
        {
            var payload = await ReadJsonBodyAsync<RemoteCommandUpsert>(request);
            if (payload == null)
            {
                await WriteJsonAsync(response, 400, new { error = "Invalid request body." });
                return;
            }

            var result = _createCommand(payload);
            await WriteOperationResultAsync(response, result);
            return;
        }

        if (TryParseProcessPath(path, out Guid processPathId))
        {
            if (HttpMethods.IsPut(request.HttpMethod))
            {
                var payload = await ReadJsonBodyAsync<RemoteCommandUpsert>(request);
                if (payload == null)
                {
                    await WriteJsonAsync(response, 400, new { error = "Invalid request body." });
                    return;
                }

                var result = _updateCommand(processPathId, payload);
                await WriteOperationResultAsync(response, result);
                return;
            }

            if (HttpMethods.IsDelete(request.HttpMethod))
            {
                var result = _deleteCommand(processPathId);
                await WriteOperationResultAsync(response, result);
                return;
            }
        }

        if (TryParseActionPath(path, out Guid actionProcessId, out string action))
        {
            if (!HttpMethods.IsPost(request.HttpMethod))
            {
                await WriteJsonAsync(response, 405, new { error = "Method not allowed." });
                return;
            }

            var result = action.ToLowerInvariant() switch
            {
                "start" => _startCommand(actionProcessId),
                "stop" => _stopCommand(actionProcessId),
                "restart" => _restartCommand(actionProcessId),
                "ctrlc" => _ctrlCCommand(actionProcessId),
                "clear-logs" => _clearLogsCommand(actionProcessId),
                _ => OperationResult.Fail("Unsupported action.")
            };

            await WriteOperationResultAsync(response, result);
            return;
        }

        if (TryParseLogsPath(path, out Guid processId) && HttpMethods.IsGet(request.HttpMethod))
        {
            var vm = _getCommands().FirstOrDefault(c => c.Entry.Id == processId);
            if (vm == null)
            {
                await WriteJsonAsync(response, 404, new { error = "Process not found." });
                return;
            }

            int tail = ParseTail(request.QueryString["tail"]);
            string fullLog = vm.GetFullOutput();
            string resultLog = fullLog.Length > tail ? fullLog[^tail..] : fullLog;
            string[] logLines = SplitLogLines(resultLog);

            await WriteJsonAsync(response, 200, new
            {
                id = vm.Entry.Id,
                name = vm.Name,
                status = vm.StatusText,
                logLength = fullLog.Length,
                returnedLength = resultLog.Length,
                returnedLines = logLines.Length,
                truncated = resultLog.Length < fullLog.Length,
                logs = logLines
            });
            return;
        }

        if (!HttpMethods.IsGet(request.HttpMethod) &&
            !HttpMethods.IsPost(request.HttpMethod) &&
            !HttpMethods.IsPut(request.HttpMethod) &&
            !HttpMethods.IsDelete(request.HttpMethod))
        {
            await WriteJsonAsync(response, 405, new { error = "Method not allowed." });
            return;
        }

        await WriteJsonAsync(response, 404, new { error = "Endpoint not found." });
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        string expected = _getPassword().Trim();
        if (string.IsNullOrEmpty(expected))
        {
            return true;
        }

        string provided = request.Headers["X-CmdHub-Password"] ?? request.QueryString["password"] ?? string.Empty;
        return string.Equals(expected, provided, StringComparison.Ordinal);
    }

    private static void ApplyCorsHeaders(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-CmdHub-Password";
        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
    }

    private object BuildProcessDto(CommandViewModel vm)
    {
        if (!vm.TryCaptureProcessSnapshot(out var snapshot))
        {
            _cpuSamples.TryRemove(vm.Entry.Id, out _);
            return new
            {
                id = vm.Entry.Id,
                name = vm.Name,
                command = vm.Command,
                workingDirectory = vm.WorkingDirectory,
                status = vm.StatusText,
                autoRestart = vm.AutoRestart,
                runOnStart = vm.RunOnStart,
                usePowerShell = vm.UsePowerShell,
                runEveryEnabled = vm.RunEveryEnabled,
                runEveryInterval = vm.RunEveryInterval,
                runEveryUnit = vm.RunEveryUnit,
                restartEveryEnabled = vm.RestartEveryEnabled,
                restartEveryInterval = vm.RestartEveryInterval,
                restartEveryUnit = vm.RestartEveryUnit,
                isRunning = false,
                pid = (int?)null,
                cpuPercent = (double?)null,
                workingSetBytes = (long?)null,
                privateMemoryBytes = (long?)null,
                threadCount = (int?)null,
                handleCount = (int?)null,
                lastUpdatedUtc = (DateTime?)null
            };
        }

        double? cpuPercent = TryComputeCpuPercent(vm.Entry.Id, snapshot.TotalProcessorTime, snapshot.SampledAtUtc);

        return new
        {
            id = vm.Entry.Id,
            name = vm.Name,
            command = vm.Command,
            workingDirectory = vm.WorkingDirectory,
            status = vm.StatusText,
            autoRestart = vm.AutoRestart,
            runOnStart = vm.RunOnStart,
            usePowerShell = vm.UsePowerShell,
            runEveryEnabled = vm.RunEveryEnabled,
            runEveryInterval = vm.RunEveryInterval,
            runEveryUnit = vm.RunEveryUnit,
            restartEveryEnabled = vm.RestartEveryEnabled,
            restartEveryInterval = vm.RestartEveryInterval,
            restartEveryUnit = vm.RestartEveryUnit,
            isRunning = true,
            pid = snapshot.ProcessId,
            cpuPercent,
            workingSetBytes = snapshot.WorkingSetBytes,
            privateMemoryBytes = snapshot.PrivateMemoryBytes,
            threadCount = snapshot.ThreadCount,
            handleCount = snapshot.HandleCount,
            lastUpdatedUtc = snapshot.SampledAtUtc
        };
    }

    private double? TryComputeCpuPercent(Guid id, TimeSpan totalProcessorTime, DateTime sampledAtUtc)
    {
        var current = new CpuSample(totalProcessorTime, sampledAtUtc);
        if (!_cpuSamples.TryGetValue(id, out var previous))
        {
            _cpuSamples[id] = current;
            return null;
        }

        _cpuSamples[id] = current;

        double cpuMs = (totalProcessorTime - previous.TotalProcessorTime).TotalMilliseconds;
        double elapsedMs = (sampledAtUtc - previous.SampledAtUtc).TotalMilliseconds;
        if (elapsedMs <= 0)
        {
            return 0;
        }

        double cpu = cpuMs / (elapsedMs * Environment.ProcessorCount) * 100.0;
        return Math.Round(Math.Clamp(cpu, 0, 100), 2);
    }

    private static bool TryParseLogsPath(string path, out Guid processId)
    {
        processId = Guid.Empty;
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 4)
        {
            return false;
        }

        if (!string.Equals(parts[0], "api", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parts[1], "processes", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parts[3], "logs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParse(parts[2], out processId);
    }

    private static bool TryParseProcessPath(string path, out Guid processId)
    {
        processId = Guid.Empty;
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        if (!string.Equals(parts[0], "api", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parts[1], "processes", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParse(parts[2], out processId);
    }

    private static bool TryParseActionPath(string path, out Guid processId, out string action)
    {
        processId = Guid.Empty;
        action = string.Empty;

        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
        {
            return false;
        }

        if (!string.Equals(parts[0], "api", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parts[1], "processes", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parts[3], "actions", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Guid.TryParse(parts[2], out processId))
        {
            return false;
        }

        action = parts[4];
        return true;
    }

    private static int ParseTail(string? value)
    {
        const int defaultTail = 16000;
        const int maxTail = 200000;

        if (!int.TryParse(value, out int parsed) || parsed <= 0)
        {
            return defaultTail;
        }

        return Math.Min(parsed, maxTail);
    }

    private static string[] SplitLogLines(string log)
    {
        if (string.IsNullOrEmpty(log))
        {
            return Array.Empty<string>();
        }

        return log
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, object payload)
    {
        response.StatusCode = statusCode;
        byte[] buffer = JsonSerializer.SerializeToUtf8Bytes(payload, _jsonOptions);
        response.ContentLength64 = buffer.Length;

        await response.OutputStream.WriteAsync(buffer);
        response.OutputStream.Close();
    }

    private async Task<T?> ReadJsonBodyAsync<T>(HttpListenerRequest request) where T : class
    {
        if (request.InputStream == Stream.Null)
        {
            return null;
        }

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8, leaveOpen: false);
        string body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task WriteOperationResultAsync(HttpListenerResponse response, OperationResult result)
    {
        if (result.Success)
        {
            await WriteJsonAsync(response, 200, new
            {
                success = true,
                id = result.Id
            });
        }
        else
        {
            await WriteJsonAsync(response, 400, new
            {
                success = false,
                error = result.Error ?? "Operation failed."
            });
        }
    }

    private static void TryWriteError(HttpListenerResponse response, int statusCode, string message)
    {
        try
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            byte[] payload = Encoding.UTF8.GetBytes($"{{\"error\":\"{message.Replace("\"", "'")}\"}}");
            response.ContentLength64 = payload.Length;
            response.OutputStream.Write(payload, 0, payload.Length);
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

    private readonly record struct CpuSample(TimeSpan TotalProcessorTime, DateTime SampledAtUtc);

    private static class HttpMethods
    {
        public static bool IsGet(string? method)
            => string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);

        public static bool IsPost(string? method)
            => string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);

        public static bool IsPut(string? method)
            => string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase);

        public static bool IsDelete(string? method)
            => string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);

        public static bool IsOptions(string? method)
            => string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase);
    }
}
