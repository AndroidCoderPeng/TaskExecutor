using System.Diagnostics;
using System.Text;

namespace TaskExecutor.Utils;

public class CommandExecutor
{
    public event Action<string>? OnStandardOutput;
    public event Action<string>? OnStandardError;

    private readonly string _arguments;

    public CommandExecutor(string? arguments)
    {
        _arguments = arguments ?? string.Empty;
    }

    /// <summary>
    /// 同步执行命令并阻塞直到退出，返回进程退出码（0 通常表示成功）。
    /// </summary>
    public int Execute(string executor)
    {
        using var process = Start(executor);
        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>
    /// 带超时的同步执行；超时或取消时杀掉进程并返回 -1。
    /// </summary>
    public int Execute(string executor, int timeoutMs)
    {
        using var process = Start(executor);
        if (!process.WaitForExit(timeoutMs))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // 进程恰好在此期间退出了，忽略
            }

            return -1;
        }

        return process.ExitCode;
    }

    /// <summary>
    /// 在后台线程执行命令，返回一个在进程退出时完成的 Task（含退出码）。
    /// </summary>
    public Task<int> ExecuteAsync(string executor)
    {
        return Task.Run(() => Execute(executor));
    }

    /// <summary>
    /// 非阻塞启动进程，返回 Process 供调用方自行轮询进度。
    /// 调用方需在结束后自行释放（Dispose / Close）。
    /// </summary>
    public Process StartNonBlocking(string executor)
    {
        return Start(executor);
    }

    private Process Start(string executor)
    {
        if (string.IsNullOrWhiteSpace(executor))
        {
            throw new ArgumentException("可执行文件路径不能为空。", nameof(executor));
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executor,
                Arguments = _arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                OnStandardOutput?.Invoke(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                OnStandardError?.Invoke(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }
}