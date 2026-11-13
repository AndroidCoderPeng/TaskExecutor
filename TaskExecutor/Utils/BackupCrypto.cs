using System.Security.Cryptography;
using System.Text;

namespace TaskExecutor.Utils;

public static class BackupCrypto
{
    private static readonly byte[] Key = SHA256.HashData("TaskExecutor.Backup.Key.v1"u8.ToArray());

    public static string Encrypt(string plain)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plain);
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // IV 不保密，直接拼在密文前面
        var result = new byte[aes.IV.Length + cipher.Length];
        aes.IV.CopyTo(result, 0);
        cipher.CopyTo(result, aes.IV.Length);

        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string cipherBase64)
    {
        var bytes = Convert.FromBase64String(cipherBase64);
        if (bytes.Length <= 16) throw new CryptographicException("密文格式不正确");

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = bytes[..16];

        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(bytes, 16, bytes.Length - 16);
        return Encoding.UTF8.GetString(plain);
    }
}