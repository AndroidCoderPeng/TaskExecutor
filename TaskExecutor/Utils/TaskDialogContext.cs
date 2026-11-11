namespace TaskExecutor.Utils;

public sealed record TaskDialogContext(string DeviceName, string? SystemVersion, int EditIndex = -1)
{
    public bool IsEditing => EditIndex >= 0;
}