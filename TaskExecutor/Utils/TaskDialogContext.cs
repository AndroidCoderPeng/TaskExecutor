using TaskExecutor.Cache;

namespace TaskExecutor.Utils;

public sealed record TaskDialogContext(string DeviceName, string? SystemVersion, TaskRow? Task = null);