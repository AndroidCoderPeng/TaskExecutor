using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskExecutor.Cache;
using TaskExecutor.Services;
using TaskExecutor.Utils;

namespace TaskExecutor.ViewModels;

public partial class EmailConfigViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private string _smtpServer = string.Empty;
    [ObservableProperty] private int _smtpPort;
    [ObservableProperty] private string _senderEmail = string.Empty;
    [ObservableProperty] private string _authorizationCode = string.Empty;
    [ObservableProperty] private string _recipientEmail = string.Empty;

    #endregion

    private readonly IToastService _toastService;
    private readonly EmailConfig _config;

    public EmailConfigViewModel(IToastService toastService)
    {
        _toastService = toastService;

        _config = SettingsStore.Load<EmailConfig>(ConfigSections.FileName, ConfigSections.Email);
        SmtpServer = _config.SmtpServer == "" ? "smtp.qq.com" : _config.SmtpServer;
        SmtpPort = _config.SmtpPort == 0 ? 465 : _config.SmtpPort;
        SenderEmail = _config.SenderEmail;
        AuthorizationCode = _config.SenderAuthCode;
        RecipientEmail = _config.RecipientEmail;
    }

    [RelayCommand]
    private async Task TestEmailNotificationAsync()
    {
        if (!CheckEmailConfig()) return;

        if (!SenderEmail.EndsWith("@qq.com"))
        {
            _toastService.Show("发送邮箱必须是 QQ 邮箱", ToastLevel.Warning);
            return;
        }

        // 实现邮件发送
        try
        {
            await EmailSender.SendAsync(new EmailMessage
            {
                Host = SmtpServer.Trim(),
                Port = SmtpPort,
                UseSsl = true,
                SenderEmail = SenderEmail.Trim(),
                AuthCode = AuthorizationCode,
                Recipient = RecipientEmail.Trim(),
                Subject = "【任务执行器】邮箱通知测试",
                Body = $"这是一封来自「任务执行器」的测试邮件。\r\n\r\n" +
                       $"发送时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
                       "收到本邮件说明邮箱配置正确，可以正常接收任务通知。"
            });

            _toastService.Show($"测试邮件已发送至 {RecipientEmail.Trim()}", ToastLevel.Success);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            _toastService.Show($"邮件发送失败：{ex.Message}", ToastLevel.Error);
        }
    }

    [RelayCommand]
    private void SaveEmailConfig()
    {
        if (!CheckEmailConfig()) return;

        _config.SmtpServer = SmtpServer.Trim();
        _config.SmtpPort = SmtpPort;
        _config.SenderEmail = SenderEmail.Trim();
        _config.SenderAuthCode = AuthorizationCode;
        _config.RecipientEmail = RecipientEmail.Trim();

        SettingsStore.Save(ConfigSections.FileName, ConfigSections.Email, _config);
        _toastService.Show("邮箱配置已保存", ToastLevel.Success);
    }

    private bool CheckEmailConfig()
    {
        if (string.IsNullOrEmpty(SmtpServer) ||
            SmtpPort is <= 0 or > 65535 ||
            string.IsNullOrEmpty(SenderEmail) ||
            string.IsNullOrEmpty(AuthorizationCode) ||
            string.IsNullOrEmpty(RecipientEmail))
        {
            _toastService.Show("请填写有效的邮箱配置和端口", ToastLevel.Warning);
            return false;
        }

        if (!IsValidEmail(SenderEmail) || !IsValidEmail(RecipientEmail))
        {
            _toastService.Show("请填写有效的邮箱地址", ToastLevel.Warning);
            return false;
        }

        return true;
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}