using System.Globalization;
using Newtonsoft.Json.Linq;
using TaskExecutor.Cache;

namespace TaskExecutor.Utils;

/// <summary>
/// 休息日判断：法定节假日、周末、调休上班三类规则分开表达，便于后续拆分策略。
/// </summary>
public class HolidayHelper
{
    private static readonly Dictionary<DayOfWeek, string> WeekdayNames = new()
    {
        [DayOfWeek.Monday] = "星期一",
        [DayOfWeek.Tuesday] = "星期二",
        [DayOfWeek.Wednesday] = "星期三",
        [DayOfWeek.Thursday] = "星期四",
        [DayOfWeek.Friday] = "星期五",
        [DayOfWeek.Saturday] = "星期六",
        [DayOfWeek.Sunday] = "星期日"
    };

    /// <summary>
    /// 是否为休息日（当前策略）：未勾选的自定义星期 → 是；法定节假日 → 是；调休上班日 → 否。
    /// 仅在「跳过节假日」开启时由调度器调用，自定义工作日随该开关一起生效。
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static bool IsRestDay(DateOnly date)
    {
        // 自定义工作日优先：按星期循环生效，未勾选的星期视为休息日（全部取消则每天都跳过）
        var workday = SettingsStore.Load<WorkdayConfig>(ConfigSections.FileName, ConfigSections.Workday);
        if (!workday.Weekdays.Contains(WeekdayNames[date.DayOfWeek]))
        {
            return true;
        }

        var holiday = SettingsStore.Load<ChinaHolidayConfig>(ConfigSections.FileName, ConfigSections.ChinaHoliday);

        // 未同步当年数据时不跳过，避免任务永远不执行
        if (holiday.Year != date.Year || string.IsNullOrWhiteSpace(holiday.Data))
        {
            return false;
        }

        try
        {
            var root = JObject.Parse(holiday.Data);
            var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            if (IsWorkday(root, key))
            {
                return false;
            }

            return IsHoliday(root, key);
        }
        catch
        {
            return false; // 数据解析失败按非节假日处理
        }
    }

    /// <summary>
    /// 法定节假日（chinese-days holidays）
    /// </summary>
    /// <param name="root"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    private static bool IsHoliday(JObject root, string key) => root["holidays"]?[key] != null;

    /// <summary>
    /// 调休上班日（chinese-days workdays）
    /// </summary>
    /// <param name="root"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    private static bool IsWorkday(JObject root, string key) => root["workdays"]?[key] != null;
}