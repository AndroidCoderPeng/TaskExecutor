using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaskExecutor.Utils;

public sealed class WechatTextMessage
{
    public string WebhookKey { get; init; } = "";
    public string Content { get; init; } = "";
}

public sealed class WechatImageMessage
{
    public string WebhookKey { get; init; } = "";
    public string ImagePath { get; init; } = "";
}

public class WechatSender
{
    private const string Endpoint = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send";
    private const int MaxImageBytes = 2 * 1024 * 1024; // 群机器人图片上限 2MB
    private const int MaxContentBytes = 2048; // 文本消息上限 2048 字节

    // 复用同一个 HttpClient，避免每次发送都新建连接（端口耗尽）
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
    
    public static Task SendTextAsync(WechatTextMessage msg, CancellationToken ct = default)
    {
        var body = new JObject
        {
            ["msgtype"] = "text",
            ["text"] = new JObject { ["content"] = TruncateUtf8(msg.Content, MaxContentBytes) }
        };

        return PostAsync(msg.WebhookKey, body, ct);
    }
    
    public static async Task SendImageAsync(WechatImageMessage msg, CancellationToken ct = default)
    {
        var bytes = await File.ReadAllBytesAsync(msg.ImagePath, ct);
        if (bytes.Length > MaxImageBytes)
        {
            throw new InvalidOperationException($"图片超过 2MB 限制（当前 {bytes.Length / 1024}KB）");
        }
        var hash = Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();

        var body = new JObject
        {
            ["msgtype"] = "image",
            ["image"] = new JObject
            {
                ["base64"] = Convert.ToBase64String(bytes),
                ["md5"] = hash
            }
        };

        await PostAsync(msg.WebhookKey, body, ct);
    }

    private static async Task PostAsync(string webhookKey, JObject body, CancellationToken ct)
    {
        var url = $"{Endpoint}?key={Uri.EscapeDataString(NormalizeKey(webhookKey))}";

        using var content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync(url, content, ct);
        
        var raw = await response.Content.ReadAsStringAsync(ct);
        
        // HTTP 200 也可能是业务失败：必须解析 errcode
        var errCode = -1;
        var errMsg = raw;
        try
        {
            var json = JObject.Parse(raw);
            errCode = json["errcode"]?.Value<int>() ?? -1;
            errMsg = json["errmsg"]?.Value<string>() ?? raw;
        }
        catch
        {
            // 解析失败时保留原文，交由下面统一抛出
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}：{raw}");
        }

        if (errCode != 0)
        {
            throw new InvalidOperationException(MapError(errCode, errMsg));
        }
    }
    
    /// <summary>
    /// 容错：用户可能直接粘贴完整 Webhook 地址
    /// </summary>
    /// <param name="keyOrUrl"></param>
    /// <returns></returns>
    private static string NormalizeKey(string keyOrUrl)
    {
        var s = keyOrUrl.Trim();
        var idx = s.IndexOf("key=", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            s = s[(idx + 4)..];
            var amp = s.IndexOf('&');
            if (amp >= 0) s = s[..amp];
        }

        return s;
    }
    
    private static string MapError(int code, string msg) => code switch
    {
        93000 => "Webhook Key 无效或已失效（错误码 93000）",
        45009 => "发送过于频繁，群机器人限制每分钟 20 条（错误码 45009）",
        40058 => "请求参数不合法，请检查消息内容（错误码 40058）",
        _ => $"发送失败：{msg}（错误码 {code}）"
    };
    
    /// <summary>
    /// 按 UTF-8 字节截断，避免超过 2048 字节限制
    /// </summary>
    /// <param name="text"></param>
    /// <param name="maxBytes"></param>
    /// <returns></returns>
    private static string TruncateUtf8(string text, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes) return text;

        var sb = new StringBuilder();
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var len = Encoding.UTF8.GetByteCount(rune.ToString());
            if (count + len > maxBytes) break;
            sb.Append(rune);
            count += len;
        }

        return sb.ToString();
    }
}