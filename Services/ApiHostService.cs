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
using CmdHub.ViewModels;

namespace CmdHub.Services;

public sealed class ApiHostService : IDisposable
{
    private readonly Func<IReadOnlyList<CommandViewModel>> _getCommands;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly ConcurrentDictionary<Guid, CpuSample> _cpuSamples = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public int Port { get; }
    public bool IsRunning => _listener?.IsListening == true;

    public ApiHostService(Func<IReadOnlyList<CommandViewModel>> getCommands, int port)
    {
        _getCommands = getCommands ?? throw new ArgumentNullException(nameof(getCommands));
        Port = port;
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
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

        if (!HttpMethods.IsGet(request.HttpMethod))
        {
            await WriteJsonAsync(response, 405, new { error = "Method not allowed." });
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

        if (string.Equals(path, "/api/processes", StringComparison.OrdinalIgnoreCase))
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

        if (TryParseLogsPath(path, out Guid processId))
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

        await WriteJsonAsync(response, 404, new { error = "Endpoint not found." });
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
                status = vm.StatusText,
                autoRestart = vm.AutoRestart,
                runOnStart = vm.RunOnStart,
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
            status = vm.StatusText,
            autoRestart = vm.AutoRestart,
            runOnStart = vm.RunOnStart,
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
    }
}
