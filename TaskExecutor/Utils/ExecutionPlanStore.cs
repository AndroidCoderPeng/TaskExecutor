using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaskExecutor.Utils;

public sealed class ExecutionPlanStore(string? path = null)
{
    private readonly string _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaskExecutor",
        "execution-plan.json");

    public ExecutionPlanState Load()
    {
        if (!File.Exists(_path)) return new ExecutionPlanState();

        var state = ReadState();
        ValidateState(state);
        return state;
    }

    private ExecutionPlanState ReadState()
    {
        var root = JObject.Parse(File.ReadAllText(_path));
        var tasksToken = root[nameof(ExecutionPlanState.Tasks)];

        if (tasksToken is not JArray tasks)
        {
            throw new InvalidDataException("执行计划文件缺少有效的任务列表。");
        }

        if (tasks.Any(task => task.Type != JTokenType.Object))
        {
            throw new InvalidDataException("执行计划文件包含无效的任务项。");
        }

        return root.ToObject<ExecutionPlanState>() ?? throw new InvalidDataException("执行计划文件内容为空。");
    }

    private static void ValidateState(ExecutionPlanState state)
    {
        if (state.CycleDate == default)
        {
            throw new InvalidDataException("执行计划文件缺少有效的周期日期。");
        }

        ValidateTasks(state.Tasks);
    }

    private static void ValidateTasks(List<PlannedTask> tasks)
    {
        foreach (var task in tasks)
        {
            ValidateTask(task);
        }

        var hasDuplicates = tasks
            .GroupBy(task => (task.TaskName, task.CycleDate.Date))
            .Any(group => group.Count() > 1);

        if (hasDuplicates)
        {
            throw new InvalidDataException("执行计划文件包含重复的任务。");
        }
    }

    private static void ValidateTask(PlannedTask task)
    {
        if (string.IsNullOrWhiteSpace(task.TaskName)
            || string.IsNullOrWhiteSpace(task.PlannedAt)
            || task.CycleDate == default
            || task.ScheduledAt == default
            || !Enum.IsDefined(task.State))
        {
            throw new InvalidDataException("执行计划文件包含无效的任务数据。");
        }
    }

    public void Save(ExecutionPlanState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(state, Formatting.Indented));
        File.Move(temporaryPath, _path, true);
    }
}