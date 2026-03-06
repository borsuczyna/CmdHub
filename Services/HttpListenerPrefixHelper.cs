using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace CmdHub.Services;

public static class HttpListenerPrefixHelper
{
    public static IReadOnlyList<string> BuildLocalPrefixes(int port)
        => new[]
        {
            $"http://localhost:{port}/",
            $"http://127.0.0.1:{port}/"
        };

    public static IReadOnlyList<string> BuildPrefixes(int port)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "localhost",
            "127.0.0.1"
        };

        try
        {
            hosts.Add(Dns.GetHostName());
            foreach (var address in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (IPAddress.IsLoopback(address))
                {
                    continue;
                }

                hosts.Add(address.ToString());
            }
        }
        catch
        {
            // Fallback to localhost only.
        }

        var prefixes = new List<string>();
        foreach (var host in hosts)
        {
            prefixes.Add($"http://{host}:{port}/");
        }

        return prefixes;
    }

    public static string BuildUrlAclCommand(int port)
    {
        string user = $"{Environment.UserDomainName}\\{Environment.UserName}";
        return $"netsh http add urlacl url=http://+:{port}/ user=\"{user}\"";
    }
}
