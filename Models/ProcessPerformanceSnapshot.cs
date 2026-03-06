using System;

namespace CmdHub.Models;

public sealed class ProcessPerformanceSnapshot
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public TimeSpan TotalProcessorTime { get; init; }
    public long WorkingSetBytes { get; init; }
    public long PrivateMemoryBytes { get; init; }
    public int ThreadCount { get; init; }
    public int? HandleCount { get; init; }
    public DateTime? StartTimeLocal { get; init; }
    public DateTime SampledAtUtc { get; init; }
}
