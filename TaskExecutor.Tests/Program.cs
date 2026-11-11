using System.IO;
using Newtonsoft.Json.Linq;
using TaskExecutor.Cache;
using TaskExecutor.Services;

var day = new DateTime(2026, 11, 11);
var now = day.AddHours(8);
var schedule = new TaskScheduleConfig { IsNeedRandomTime = true, IsResetDailyTask = true };
var range = new TimeRangeConfig { MinLimitTime = 0, MaxLimitTime = 30 };
var reset = new ResetTimeConfig { Time = "00:00" };
var config = new TaskConfig
{
    Tasks =
    [
        new TaskRow { Name = "A", PlannedAt = "09:00", IsEnabled = true },
        new TaskRow { Name = "B", PlannedAt = "09:10", IsEnabled = true,
            TimeRange = new TimeRangeConfig { MinLimitTime = 2, MaxLimitTime = 2 } }
    ]
};
var draws = 0;
var plan = new ExecutionPlan(new ExecutionPlanState(), (min, max) => { draws++; return max; });
void Reconcile(DateTime at) => plan.Reconcile(at, config, schedule, range, reset);
PlannedTask Item(string name, DateTime date) =>
    plan.State.Tasks.Single(p => p.TaskName == name && p.CycleDate == date);
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var displayTask = new TaskRow { ExecuteTime = day.AddHours(9).AddMinutes(2).AddSeconds(7) };
Check(displayTask.ExecuteTimeDisplay == "09:02", "execution time format");
displayTask.ExecuteTime = null;
Check(displayTask.ExecuteTimeDisplay == "—", "missing execution time");
var serializedTask = JObject.FromObject(new TaskRow
    { PlannedAt = "09:00", ExecuteTime = day.AddHours(9).AddMinutes(30) });
Check((string?)serializedTask["PlannedAt"] == "09:00" && serializedTask["ExecuteTime"] == null,
    "persist planned time without runtime execution time");

Reconcile(now);
Check(Item("A", day).ScheduledAt == day.AddHours(9).AddMinutes(30), "A random time");
Check(Item("B", day).ScheduledAt == day.AddHours(9).AddMinutes(12), "B random time");
var enabledNames = new HashSet<string> { "A", "B" };
Check(plan.FindNextDue(day.AddHours(9).AddMinutes(31), enabledNames)?.TaskName == "B",
    "execute by planned time, not base time");
enabledNames.Remove("B");
Check(plan.FindNextDue(day.AddHours(9).AddMinutes(31), enabledNames)?.TaskName == "A",
    "disabled task must not be due");
Reconcile(now.AddMinutes(1));
Check(draws == 2, "refresh must not redraw");

var dir = Path.Combine(Path.GetTempPath(), "TaskExecutorPlanTests-" + Guid.NewGuid());
var path = Path.Combine(dir, "plan.json");
try
{
    var store = new ExecutionPlanStore(path);
    Item("B", day).State = PlannedTaskState.Triggered;
    store.Save(plan.State);
    var restored = new ExecutionPlan(store.Load(), (_, _) => throw new Exception("unexpected redraw"));
    restored.Reconcile(now, config, schedule, range, reset);
    Check(restored.State.Tasks.Single(p => p.TaskName == "A").ScheduledAt == day.AddHours(9).AddMinutes(30),
        "restart must retain planned time");
    Check(restored.State.Tasks.Single(p => p.TaskName == "B").State == PlannedTaskState.Triggered,
        "restart must retain triggered state");
    var savedPlan = JObject.Parse(File.ReadAllText(path));
    Check(savedPlan["Tasks"]!.All(t => t["PlannedAt"] != null && t["ExecuteTime"] == null),
        "persist plan using new field name");
    File.WriteAllText(path, "{}");
    var rejected = false;
    try { store.Load(); }
    catch (InvalidDataException) { rejected = true; }
    Check(rejected, "corrupt plan must not silently regenerate");
}
finally
{
    if (File.Exists(path)) File.Delete(path);
    if (Directory.Exists(dir)) Directory.Delete(dir);
}

config.Tasks[0].PlannedAt = "07:00";
Reconcile(now);
Check(Item("A", day).State == PlannedTaskState.Skipped, "edited past task must be skipped");
config.Tasks[0].PlannedAt = "10:00";
Reconcile(now);
Check(Item("A", day).State == PlannedTaskState.Skipped, "skip must last through cycle");
Item("B", day).State = PlannedTaskState.Triggered;
config.Tasks[1].PlannedAt = "11:00";
Reconcile(now);
Check(Item("B", day).State == PlannedTaskState.Triggered, "editing triggered task must not run again");
Check(Item("B", day).ScheduledAt == day.AddHours(9).AddMinutes(12), "retain triggered time");

Reconcile(day.AddDays(1).AddMinutes(1));
Check(Item("A", day.AddDays(1)).State == PlannedTaskState.Pending, "new day resets skip");
Check(Item("B", day.AddDays(1)).State == PlannedTaskState.Pending, "new day resets trigger");

var crossDay = day.AddDays(2);
config.Tasks[0].PlannedAt = "23:50";
config.Tasks[1].IsEnabled = false;
Reconcile(crossDay.AddHours(23));
var old = Item("A", crossDay);
Check(old.ScheduledAt == crossDay.AddDays(1).AddMinutes(20), "cross-midnight timestamp");
Reconcile(crossDay.AddDays(1).AddMinutes(1));
Check(plan.State.Tasks.Contains(old) && old.State == PlannedTaskState.Pending, "retain cross-midnight task");
config.Tasks[0].PlannedAt = "22:00";
Reconcile(crossDay.AddDays(1).AddMinutes(2));
Check(old.State == PlannedTaskState.Skipped, "editing must invalidate carried plan");

var resetPlan = new ExecutionPlan(new ExecutionPlanState(), (min, _) => min);
var resetConfig = new TaskConfig
{
    Tasks = [new TaskRow { Name = "C", PlannedAt = "09:00", IsEnabled = true }]
};
var laterReset = new ResetTimeConfig { Time = "12:00" };
resetPlan.Reconcile(now, resetConfig, schedule, range, laterReset);
resetPlan.State.Tasks.Single().State = PlannedTaskState.Triggered;
resetPlan.Reconcile(day.AddHours(12), resetConfig, schedule, range, laterReset);
Check(resetPlan.State.Tasks.Single().State == PlannedTaskState.Pending, "daily reset regenerates plan");
resetPlan.State.Tasks.Single().State = PlannedTaskState.Triggered;
resetPlan.Reconcile(day.AddHours(13), resetConfig, schedule, range, laterReset);
Check(resetPlan.State.Tasks.Single().State == PlannedTaskState.Triggered, "daily reset only once");

var noReset = new ExecutionPlan(new ExecutionPlanState(), (min, _) => min);
schedule.IsResetDailyTask = false;
noReset.Reconcile(now, resetConfig, schedule, range, laterReset);
noReset.State.Tasks.Single().State = PlannedTaskState.Triggered;
noReset.Reconcile(day.AddHours(12), resetConfig, schedule, range, laterReset);
Check(noReset.State.Tasks.Single().State == PlannedTaskState.Triggered, "disabled reset retains state");

Console.WriteLine("Execution plan checks passed.");
