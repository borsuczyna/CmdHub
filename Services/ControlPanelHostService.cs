using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CmdHub.Services;

public sealed class ControlPanelHostService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<string, string> MimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8",
            [".js"] = "text/javascript; charset=utf-8",
            [".css"] = "text/css; charset=utf-8",
            [".json"] = "application/json; charset=utf-8",
            [".svg"] = "image/svg+xml",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".ico"] = "image/x-icon",
            [".woff"] = "font/woff",
            [".woff2"] = "font/woff2"
        };

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
        string requestPath = context.Request.Url?.AbsolutePath ?? "/";
        if (string.Equals(requestPath, "/panel-config.json", StringComparison.OrdinalIgnoreCase))
        {
            await WritePanelConfigAsync(context.Response);
            return;
        }

        string assetPath = NormalizeAssetPath(requestPath);
        if (!TryOpenEmbeddedAsset(assetPath, out var stream, out string contentType, out bool isIndex))
        {
            if (!assetPath.Contains('.'))
            {
                assetPath = "index.html";
                if (!TryOpenEmbeddedAsset(assetPath, out stream, out contentType, out isIndex))
                {
                    TryWritePlainText(context.Response, 404, "Control panel assets not found. Build frontend first.");
                    return;
                }
            }
            else
            {
                TryWritePlainText(context.Response, 404, "Not found.");
                return;
            }
        }

        await using (stream)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = contentType;

            if (isIndex)
            {
                context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            }
            else
            {
                context.Response.Headers["Cache-Control"] = "public, max-age=86400";
            }

            context.Response.ContentLength64 = stream.Length;
            await stream.CopyToAsync(context.Response.OutputStream);
            context.Response.OutputStream.Close();
        }
    }

    private async Task WritePanelConfigAsync(HttpListenerResponse response)
    {
        var payload = new
        {
            apiPort = _getApiPort(),
            panelPort = Port
        };

        byte[] buffer = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        response.StatusCode = 200;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.Headers["Cache-Control"] = "no-cache";
        await response.OutputStream.WriteAsync(buffer);
        response.OutputStream.Close();
    }

    private static string NormalizeAssetPath(string requestPath)
    {
        string normalized = requestPath.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized == "/")
        {
            return "index.html";
        }

        normalized = normalized.TrimStart('/');
        if (normalized.EndsWith('/'))
        {
            normalized += "index.html";
        }

        return normalized.Replace('\\', '/');
    }

    private static bool TryOpenEmbeddedAsset(string assetPath, out Stream stream, out string contentType, out bool isIndex)
    {
        stream = Stream.Null;
        contentType = "application/octet-stream";
        isIndex = string.Equals(assetPath, "index.html", StringComparison.OrdinalIgnoreCase);

        var archiveBytes = FrontendBundleData.ArchiveBytes;
        if (archiveBytes.Length == 0)
        {
            return false;
        }

        using var archiveStream = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);

        string normalizedPath = assetPath.Replace('\\', '/');
        var entry = archive.GetEntry(normalizedPath)
            ?? archive.GetEntry(normalizedPath.Replace('/', '\\'));

        if (entry == null)
        {
            // Some ZIP creators store Windows separators or preserve differing case.
            foreach (var candidate in archive.Entries)
            {
                string candidatePath = candidate.FullName.Replace('\\', '/');
                if (string.Equals(candidatePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    break;
                }
            }
        }

        if (entry == null)
        {
            return false;
        }

        var memory = new MemoryStream();
        using (var entryStream = entry.Open())
        {
            entryStream.CopyTo(memory);
        }

        memory.Position = 0;
        stream = memory;
        contentType = GetContentType(assetPath);
        return true;
    }

    private static string GetContentType(string path)
    {
        string extension = Path.GetExtension(path);
        if (MimeTypes.TryGetValue(extension, out string? contentType))
        {
            return contentType;
        }

        return "application/octet-stream";
    }

    private static void TryWritePlainText(HttpListenerResponse response, int statusCode, string message)
    {
        try
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            response.StatusCode = statusCode;
            response.ContentType = "text/plain; charset=utf-8";
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
}
