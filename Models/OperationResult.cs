using System;

namespace CmdHub.Models;

public sealed class OperationResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public Guid? Id { get; init; }

    public static OperationResult Ok(Guid? id = null) => new() { Success = true, Id = id };

    public static OperationResult Fail(string error) => new() { Success = false, Error = error };
}
