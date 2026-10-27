using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace TaskExecutor.Utils;

public sealed class EmailMessage
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 465;
    public bool UseSsl { get; init; } = true;
    public string SenderEmail { get; init; } = "";
    public string AuthCode { get; init; } = "";
    public string Recipient { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Body { get; init; } = "";
}

public class EmailSender
{
    public static async Task SendAsync(EmailMessage msg, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(msg.SenderEmail, msg.SenderEmail));
        message.To.Add(MailboxAddress.Parse(msg.Recipient));
        message.Subject = msg.Subject;
        message.Body = new TextPart("plain")
        {
            Text = msg.Body
        };

        using var client = new SmtpClient();
        client.Timeout = 60_000;

        var secure = msg.Port == 465 ? SecureSocketOptions.SslOnConnect
            : msg.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;

        await client.ConnectAsync(msg.Host, msg.Port, secure, ct);
        await client.AuthenticateAsync(msg.SenderEmail, msg.AuthCode, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}