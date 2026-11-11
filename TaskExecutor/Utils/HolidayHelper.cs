using Newtonsoft.Json.Linq;
using TaskExecutor.Cache;

namespace TaskExecutor.Utils;

/// <summary>
/// 休息日判断：法定节假日、周末、调休上班三类规则分开表达，便于后续拆分策略。
/// </summary>
public class HolidayHelper
{
    /// <summary>
    /// 是否为休息日（当前策略）：调休上班日 → 否；法定节假日或周末 → 是。
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static bool IsRestDay(DateOnly date)
    {
        var holiday = SettingsStore.Load<ChinaHolidayConfig>(ConfigSections.FileName, ConfigSections.ChinaHoliday);

        // 未同步当年数据时不跳过，避免任务永远不执行
        if (holiday.Year != date.Year || string.IsNullOrWhiteSpace(holiday.Data))
        {
            return false;
        }

        try
        {
            var root = JObject.Parse(holiday.Data);
            var key = date.ToString("yyyy-MM-dd");

            if (IsWorkday(root, key)) return false;
            return IsHoliday(root, key) || IsWeekend(date);
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
    public static bool IsHoliday(JObject root, string key) => root["holidays"]?[key] != null;

    /// <summary>
    /// 调休上班日（chinese-days workdays）
    /// </summary>
    /// <param name="root"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static bool IsWorkday(JObject root, string key) => root["workdays"]?[key] != null;

    /// <summary>
    /// 自然周末
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static bool IsWeekend(DateOnly date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}