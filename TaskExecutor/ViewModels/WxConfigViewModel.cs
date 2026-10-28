using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class WxConfigViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private string _webhookKey = string.Empty;
    [ObservableProperty] private string _messageTitle = string.Empty;
    [ObservableProperty] private bool _attachScreenshot;

    #endregion

    private readonly IToastService _toastService;
    private readonly WechatConfig _config;

    public WxConfigViewModel(IToastService toastService)
    {
        _toastService = toastService;

        _config = SettingsStore.Load<WechatConfig>(ConfigSections.FileName, ConfigSections.Wechat);
        WebhookKey = _config.WebhookKey;
        MessageTitle = _config.MessageTitle;
        AttachScreenshot = _config.AttachScreenshot;
    }

    [RelayCommand]
    private async Task TestWebhookNotificationAsync()
    {
        if (!CheckWechatConfig()) return;
        
        // 实现企业微信通知发送
        try
        {
            await WechatSender.SendTextAsync(new WechatTextMessage
            {
                WebhookKey = WebhookKey.Trim(),
                Content = $"【{MessageTitle.Trim()}】\n\n" +
                          "这是一条来自「任务执行器」的测试消息。\n" +
                          $"发送时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                          "收到本消息说明企业微信配置正确，可以正常接收任务通知。"
            });

            _toastService.Show("测试消息已发送，请在企业微信群中查收", ToastLevel.Success);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            _toastService.Show($"企业微信通知发送失败：{ex.Message}", ToastLevel.Error);
        }
    }

    [RelayCommand]
    private void SaveWechatConfig()
    {
        if (!CheckWechatConfig()) return;

        _config.WebhookKey = WebhookKey.Trim();
        _config.MessageTitle = MessageTitle.Trim();
        _config.AttachScreenshot = AttachScreenshot;

        SettingsStore.Save(ConfigSections.FileName, ConfigSections.Wechat, _config);
        _toastService.Show("企业微信配置已保存", ToastLevel.Success);
    }

    private bool CheckWechatConfig()
    {
        if (string.IsNullOrEmpty(WebhookKey) || string.IsNullOrEmpty(MessageTitle))
        {
            _toastService.Show("请填写有效的企业微信配置", ToastLevel.Warning);
            return false;
        }

        return true;
    }
}