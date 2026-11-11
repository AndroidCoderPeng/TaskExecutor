namespace TaskExecutor.Services;

/// <summary>
/// adb 连接心跳：周期性探测设备是否在线，掉线时尝试自动重连。
/// </summary>
public interface IAdbHeartbeatService
{
    /// <summary>心跳日志内容前缀：调度器恢复统计时据此排除非任务日志。</summary>
    public const string LogPrefix = "ADB「心跳」";

    /// <summary>
    /// 心跳结果：IsAlive = false 表示判定掉线，true 表示已恢复
    /// </summary>
    /// <param name="IsAlive"></param>
    /// <param name="Serial"></param>
    /// <param name="Message"></param>
    public sealed record AdbHeartbeatResult(bool IsAlive, string Serial, string Message);

    /// <summary>
    /// 仅在连接状态翻转（在线 ↔ 掉线）时触发，且在 UI 线程
    /// </summary>
    event Action<AdbHeartbeatResult>? Beat;

    /// <summary>
    /// 心跳时间刷新：Start / 每次探测成功 / Stop 时触发（UI 线程），
    /// 参数为最新的 LastSuccessTime（Stop 时为 null）。
    /// </summary>
    event Action<DateTime?>? HeartbeatUpdated;

    bool IsRunning { get; }

    /// <summary>
    /// 连接成功后开始心跳
    /// </summary>
    /// <param name="serial"></param>
    void Start(string serial);

    /// <summary>
    /// 用户主动断开时停止心跳，避免被自动重连
    /// </summary>
    void Stop();

    /// <summary>
    /// 立即探测一次设备是否处于 device 状态（供调度器执行前复用）
    /// </summary>
    /// <param name="serial"></param>
    /// <returns></returns>
    Task<bool> ProbeAsync(string serial);

    /// <summary>
    /// 最近一次探测成功的时间；未启动或已停止时为 null
    /// </summary>
    DateTime? LastSuccessTime { get; }
}