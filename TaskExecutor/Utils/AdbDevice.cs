using System.Globalization;
using System.IO;

namespace TaskExecutor.Utils;

/// <summary>
/// 一次 adb 命令的执行结果
/// </summary>
/// <param name="Code"></param>
/// <param name="Output"></param>
public sealed record AdbResult(int Code, IReadOnlyList<string> Output)
{
    /// <summary>
    /// 退出码为 0 且输出无 Error 视为成功（与调度器既有判断口径一致）
    /// </summary>
    public bool IsSuccess => Code == 0
                             && !Output.Any(line => line.Contains("Error", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// 单台设备的 adb 高层封装：屏蔽具体命令细节，向上层暴露语义化动作。
/// 串口随设备 IP 变化，不做单例，由调用方在执行时构造。
/// </summary>
public sealed class AdbDevice
{
    private readonly string _adbPath;
    private readonly string _serial;

    public AdbDevice(string adbPath, string serial)
    {
        _adbPath = adbPath;
        _serial = serial;
    }

    /// <summary>
    /// 执行任意 adb 子命令，返回完整结果
    /// </summary>
    public async Task<AdbResult> RunAsync(params string[] arguments)
    {
        var creator = new ArgumentCreator().Append("-s").Append(_serial);
        foreach (var argument in arguments)
        {
            creator.Append(argument);
        }

        var (code, output) = await new CommandExecutor(creator.ToCommandLine()).ExecuteWithOutputAsync(_adbPath);
        Console.WriteLine(string.Join(Environment.NewLine, output));
        return new AdbResult(code, output);
    }

    /// <summary>
    /// 执行并只关心成败 
    /// </summary>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public async Task<bool> TryRunAsync(params string[] arguments) => (await RunAsync(arguments)).IsSuccess;

    /// <summary>
    /// 获取应用的启动 Activity（Launcher Activity）
    /// </summary>
    /// <returns></returns>
    public async Task<string> GetLauncherActivityAsync(string packageName)
    {
        var result = await RunAsync("shell", "cmd", "package", "resolve-activity", "--brief", packageName);
        if (!result.IsSuccess) return string.Empty;

        var activityLine = result.Output.FirstOrDefault(line => line.Contains('/'));
        if (activityLine is null) return string.Empty;

        // com.pengxh.daily.app/.ui.MainActivity
        var separator = activityLine.IndexOf('/');
        var componentPackage = activityLine[..separator];
        var activity = activityLine[(separator + 1)..];

        return activity.StartsWith('.') ? componentPackage + activity : activity;
    }

    /// <summary>
    /// 唤醒后按屏幕尺寸上滑解锁
    /// </summary>
    /// <returns></returns>
    public async Task<bool> ScreenOnAsync()
    {
        if (!await TryRunAsync("shell", "input", "keyevent", "KEYCODE_WAKEUP")) return false;
        await Task.Delay(500);

        var size = await RunAsync("shell", "wm", "size");
        if (!size.IsSuccess || !TryParseScreenSize(size.Output, out var width, out var height)) return false;

        var x = (width / 2).ToString(CultureInfo.InvariantCulture);
        var startY = ((int)(height * 0.8)).ToString(CultureInfo.InvariantCulture);
        var endY = ((int)(height * 0.2)).ToString(CultureInfo.InvariantCulture);
        return await TryRunAsync("shell", "input", "swipe", x, startY, x, endY, "300");
    }

    public Task<bool> ScreenOffAsync() => TryRunAsync("shell", "input", "keyevent", "KEYCODE_SLEEP");

    public Task<bool> OpenAppAsync(string packageName, string activity)
    {
        return TryRunAsync("shell", "am", "start", "-n", $"{packageName}/{activity}");
    }

    public Task<bool> KillAppAsync(string packageName) => TryRunAsync("shell", "am", "force-stop", packageName);

    /// <summary>
    /// 截屏：设备端 screencap 后 pull 到本地目录，返回本地路径；失败返回 null。
    /// </summary>
    public async Task<string?> ScreencapAsync(string localDirectory)
    {
        Directory.CreateDirectory(localDirectory);
        var localPath = Path.Combine(localDirectory, $"screencap_{DateTime.Now:yyyyMMdd_HHmmssfff}.png");
        const string remotePath = "/sdcard/screencap_tmp.png";

        if (!await TryRunAsync("shell", "screencap", "-p", remotePath)) return null;
        if (!await TryRunAsync("pull", remotePath, localPath))
        {
            await TryRunAsync("shell", "rm", remotePath);
            return null;
        }

        await TryRunAsync("shell", "rm", remotePath); // 清理设备端临时文件
        return localPath;
    }

    /// <summary>
    /// 解析 wm size 输出：优先 Override size（设置过覆盖尺寸时），否则 Physical size。
    /// </summary>
    private static bool TryParseScreenSize(IReadOnlyList<string> output, out int width, out int height)
    {
        width = height = 0;
        var sizeLine = output.FirstOrDefault(l => l.Contains("Override size:", StringComparison.OrdinalIgnoreCase))
                       ?? output.FirstOrDefault(l => l.Contains("Physical size:", StringComparison.OrdinalIgnoreCase));
        if (sizeLine == null) return false;

        var dims = sizeLine[(sizeLine.LastIndexOf(':') + 1)..].Split('x');
        return dims.Length == 2
               && int.TryParse(dims[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
               && int.TryParse(dims[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height)
               && width >= 2 && height >= 2;
    }
}