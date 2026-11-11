using TaskExecutor.Enums;

namespace TaskExecutor.Model;

public class RuntimeLog
{
    public string Time { get; set; } = "";
    public TaskState State { get; set; }
    public string Content { get; set; } = "";
}