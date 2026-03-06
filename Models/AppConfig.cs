using System.Collections.Generic;

namespace CmdHub.Models;

public class AppConfig
{
    public List<CommandEntry> Commands { get; set; } = new();
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool ApiEnabled { get; set; }
    public int ApiPort { get; set; } = 5480;
}
