using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CmdHub.Models;

namespace CmdHub.Services;

public class ConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CmdHub");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return CreateDefault();

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    public void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
        }
        catch { }
    }

    private static AppConfig CreateDefault() => new()
    {
        Commands = new List<CommandEntry>
        {
            new()
            {
                Name = "Ping Localhost",
                Command = "ping -t 127.0.0.1",
                WorkingDirectory = string.Empty,
                AutoRestart = true,
                RunOnStart = false
            },
            new()
            {
                Name = "System Info",
                Command = "cmd /c systeminfo",
                WorkingDirectory = string.Empty,
                AutoRestart = false,
                RunOnStart = false
            }
        }
    };
}
