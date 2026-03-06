using System;

namespace CmdHub.Models;

public class CommandEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool AutoRestart { get; set; }
    public bool RunOnStart { get; set; }
    public bool UsePowerShell { get; set; }
}
