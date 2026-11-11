using System.Globalization;
using TaskExecutor.Cache;
using TaskExecutor.Enums;

namespace TaskExecutor.Utils;

public sealed class PlannedTask
{
    public string TaskName { get; set; } = "";
    public DateTime CycleDate { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string PlannedAt { get; set; } = "";
    public bool RandomEnabled { get; set; }
    public int MinOffset { get; set; }
    public int MaxOffset { get; set; }
    public PlannedTaskState State { get; set; }
}

public sealed class ExecutionPlanState
{
    public DateTime CycleDate { get; set; }
    public DateTime? ResetAppliedDate { get; set; }
    public List<PlannedTask> Tasks { get; set; } = [];
}

public sealed class ExecutionPlan
{
    private readonly Func<int, int, int> _random;

    public ExecutionPlan(ExecutionPlanState state, Func<int, int, int>? random = null)
    {
        State = state;
        _random = random ?? ((min, max) => (int)Random.Shared.NextInt64(min, (long)max + 1));
    }

    public ExecutionPlanState State { get; }

    public PlannedTask? FindNextDue(DateTime now, IReadOnlySet<string> enabledNames) => State.Tasks.Where(task =>
            task.State == PlannedTaskState.Pending && task.ScheduledAt <= now
                                                   && enabledNames.Contains(task.TaskName))
        .OrderBy(task => task.ScheduledAt)
        .ThenBy(task => task.TaskName, StringComparer.Ordinal)
        .FirstOrDefault();

    public bool Reconcile(DateTime now, TaskConfig config, TaskScheduleConfig schedule, TimeRangeConfig globalRange,
        ResetTimeConfig reset)
    {
        var today = now.Date;
        var changed = false;
        var fresh = State.CycleDate == default;

        if (fresh || State.CycleDate.Date != today)
        {
            State.Tasks.RemoveAll(task => task.CycleDate.Date != today
                                          && (task.State != PlannedTaskState.Pending || task.ScheduledAt < today));
            State.CycleDate = today;
            State.ResetAppliedDate = null;
            changed = true;
        }

        if (TimeSpan.TryParseExact(reset.Time, @"hh\:mm", CultureInfo.InvariantCulture, out var resetTime) &&
            resetTime < TimeSpan.FromDays(1) && now.TimeOfDay >= resetTime && State.ResetAppliedDate?.Date != today)
        {
            State.ResetAppliedDate = today;
            if (!fresh && schedule.IsResetDailyTask)
            {
                State.Tasks.RemoveAll(task => task.CycleDate.Date == today);
            }

            changed = true;
        }

        var names = config.Tasks.Select(task => task.Name).ToHashSet(StringComparer.Ordinal);
        changed |= State.Tasks.RemoveAll(task => !names.Contains(task.TaskName)) > 0;

        foreach (var task in config.Tasks)
        {
            var entry = State.Tasks.FirstOrDefault(entry => entry.TaskName == task.Name
                                                            && entry.CycleDate.Date == today);
            if (!task.IsEnabled) continue;

            if (!TimeSpan.TryParseExact(task.PlannedAt, @"hh\:mm", CultureInfo.InvariantCulture, out var time) ||
                time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
            {
                foreach (var pending in State.Tasks.Where(p => p.TaskName == task.Name
                                                               && p.State == PlannedTaskState.Pending))
                {
                    pending.State = PlannedTaskState.Skipped;
                    changed = true;
                }

                Console.WriteLine($"任务「{task.Name}」的计划时间无效：{task.PlannedAt}");
                continue;
            }

            var range = task.TimeRange ?? globalRange;
            var min = schedule.IsNeedRandomTime ? Math.Max(0, range.MinLimitTime) : 0;
            var max = schedule.IsNeedRandomTime ? Math.Max(min, range.MaxLimitTime) : 0;
            foreach (var pending in State.Tasks.Where(p => p.TaskName == task.Name
                                                           && p.CycleDate.Date != today
                                                           && p.State == PlannedTaskState.Pending
                                                           && (p.PlannedAt != task.PlannedAt ||
                                                               p.RandomEnabled != schedule.IsNeedRandomTime ||
                                                               p.MinOffset != min || p.MaxOffset != max)))
            {
                pending.State = PlannedTaskState.Skipped;
                changed = true;
            }

            var differs = entry != null && (entry.PlannedAt != task.PlannedAt ||
                                            entry.RandomEnabled != schedule.IsNeedRandomTime ||
                                            entry.MinOffset != min || entry.MaxOffset != max);
            if (entry != null && !differs) continue;

            var previousState = entry?.State;
            var scheduledAt = previousState == PlannedTaskState.Triggered
                ? entry!.ScheduledAt
                : today.Add(time).AddMinutes(schedule.IsNeedRandomTime ? _random(min, max) : 0);
            var state = previousState is PlannedTaskState.Triggered or PlannedTaskState.Skipped ? previousState.Value :
                differs && scheduledAt <= now ? PlannedTaskState.Skipped : PlannedTaskState.Pending;
            var replacement = new PlannedTask
            {
                TaskName = task.Name,
                CycleDate = today,
                ScheduledAt = scheduledAt,
                PlannedAt = task.PlannedAt,
                RandomEnabled = schedule.IsNeedRandomTime,
                MinOffset = min,
                MaxOffset = max,
                State = state
            };
            if (entry != null) State.Tasks.Remove(entry);
            State.Tasks.Add(replacement);
            changed = true;
        }

        return changed;
    }
}