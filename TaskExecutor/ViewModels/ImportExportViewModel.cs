using System.Globalization;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaskExecutor.Cache;
using TaskExecutor.Enums;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class ImportExportViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private string _exportFileName = string.Empty;
    [ObservableProperty] private string _selectedFilePath = "点击右侧按钮选择备份文件";

    #endregion

    private readonly DispatcherTimer _dateRefreshTimer = new();
    private readonly IToastService _toastService;
    private const string EncPrefix = "enc:v1:";

    private static readonly Dictionary<string, string[]> SensitiveMap = new()
    {
        [ConfigSections.Email] = ["SenderAuthCode"],
        [ConfigSections.Wechat] = ["WebhookKey"],
    };

    public ImportExportViewModel(IToastService toastService)
    {
        _toastService = toastService;

        RefreshExportFileName();
        _dateRefreshTimer.Tick += OnDateRefreshTimerTick;
    }

    private void OnDateRefreshTimerTick(object? sender, EventArgs e)
    {
        _dateRefreshTimer.Stop();
        RefreshExportFileName();
        ScheduleNextMidnight();
    }

    private void RefreshExportFileName()
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ExportFileName = $"TaskExecutor_备份_{date}.json";
    }

    private void ScheduleNextMidnight()
    {
        _dateRefreshTimer.Stop();
        var now = DateTime.Now;
        _dateRefreshTimer.Interval = now.Date.AddDays(1) - now;
        _dateRefreshTimer.Start();
    }

    public void StartDateRefresh()
    {
        RefreshExportFileName();
        ScheduleNextMidnight();
    }

    public void StopDateRefresh()
    {
        _dateRefreshTimer.Stop();
    }

    [RelayCommand]
    private void ExportConfig()
    {
        // 选择文件保存位置
        var saveFileDialog = new SaveFileDialog
        {
            FileName = ExportFileName,
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*"
        };
        if (saveFileDialog.ShowDialog() != true) return;

        try
        {
            var data = SettingsStore.ExportAll();
            ProtectSensitive(data);

            var backup = new JObject
            {
                ["App"] = "TaskExecutor",
                ["Version"] = 1,
                ["ExportedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                ["Data"] = data
            };

            File.WriteAllText(saveFileDialog.FileName, backup.ToString(Formatting.Indented));
            _toastService.Show("备份已导出", ToastType.Success);
        }
        catch (Exception e)
        {
            _toastService.Show($"导出失败：{e.Message}", ToastType.Error);
        }
    }

    /// <summary>
    /// 导出前把敏感字段加密（带标记前缀，避免误把普通值当密文）。
    /// </summary>
    private static void ProtectSensitive(JObject data)
    {
        foreach (var (section, keys) in SensitiveMap)
        {
            if (data[section] is not JObject obj) continue;
            foreach (var key in keys)
            {
                var value = (string?)obj[key];
                if (string.IsNullOrEmpty(value)) continue;
                obj[key] = EncPrefix + BackupCrypto.Encrypt(value);
            }
        }
    }

    [RelayCommand]
    private void ImportConfig()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() != true) return;

        try
        {
            var backup = JObject.Parse(File.ReadAllText(openFileDialog.FileName));

            // 标准备份格式取 Data 节点；直接拿 app-config.json 也能导入（自动识别，无需区分）
            var data = backup["Data"] as JObject ?? backup;

            var decryptFailed = UnprotectSensitive(data);
            SettingsStore.ImportAll(data);
            SelectedFilePath = Path.GetFileName(openFileDialog.FileName);
            if (decryptFailed)
            {
                _toastService.Show("备份已导入，但授权码/Webhook Key 解密失败，请重新填写", ToastType.Warning);
            }
            else
            {
                _toastService.Show("备份已导入，切换页面后即可看到恢复的数据", ToastType.Success);
            }
        }
        catch (Exception e)
        {
            _toastService.Show($"导入失败：{e.Message}", ToastType.Error);
        }
    }

    /// <summary>
    /// 导入时解密敏感字段；返回是否有解密失败项（提示用户重新填写）。
    /// 旧版明文备份（无前缀）原样写入，兼容不报错。
    /// </summary>
    private static bool UnprotectSensitive(JObject data)
    {
        var failed = false;
        foreach (var (section, keys) in SensitiveMap)
        {
            if (data[section] is not JObject obj) continue;
            foreach (var key in keys)
            {
                var value = (string?)obj[key];
                if (string.IsNullOrEmpty(value) || !value.StartsWith(EncPrefix, StringComparison.Ordinal)) continue;

                try
                {
                    obj[key] = BackupCrypto.Decrypt(value[EncPrefix.Length..]);
                }
                catch
                {
                    obj[key] = "";
                    failed = true;
                }
            }
        }

        return failed;
    }
}