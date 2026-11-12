using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TaskExecutor.Model;

namespace TaskExecutor.Services;

/// <summary>
/// 运行日志持久化：按天存 JSONL（一行一条），保留 7 天。
/// 落盘失败只丢日志，不影响调度主流程。
/// </summary>
public class LogStoreService : ILogStoreService
{
    private const int RetentionDays = 7;

    private static readonly string
        DataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static readonly string LogDir = Path.Combine(DataFolder, "TaskExecutor", "logs");

    private static readonly object Lock = new();

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.None,
        Converters = { new StringEnumConverter() }
    };

    public event Action<RuntimeLog>? Appended;

    public LogStoreService() => CleanupExpired();

    public void Append(RuntimeLog log)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(GetPath(DateOnly.FromDateTime(DateTime.Now)),
                    JsonConvert.SerializeObject(log, JsonSettings) + Environment.NewLine);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"写入运行日志失败：{e.Message}");
        }

        Appended?.Invoke(log);
    }

    /// <summary>
    /// 按文件行序返回（时间升序），界面按 Insert(0) 展示即为最新在上。
    /// </summary>
    public IReadOnlyList<RuntimeLog> LoadByDate(DateOnly date)
    {
        var file = GetPath(date);
        if (!File.Exists(file)) return [];

        try
        {
            lock (Lock)
            {
                return File.ReadAllLines(file)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => JsonConvert.DeserializeObject<RuntimeLog>(line, JsonSettings))
                    .Where(log => log != null)
                    .Select(log => log!)
                    .ToList();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"读取运行日志失败：{e.Message}");
            return [];
        }
    }

    private static string GetPath(DateOnly date) => Path.Combine(LogDir, $"{date:yyyy-MM-dd}.jsonl");

    private static void CleanupExpired()
    {
        try
        {
            if (!Directory.Exists(LogDir)) return;

            var deadline = DateOnly.FromDateTime(DateTime.Now).AddDays(-RetentionDays);
            foreach (var file in Directory.EnumerateFiles(LogDir, "*.jsonl"))
            {
                if (!DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    continue;
                }

                if (date <= deadline) File.Delete(file);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"清理过期运行日志失败：{e.Message}");
        }
    }
}