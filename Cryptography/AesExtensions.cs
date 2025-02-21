using System.Security.Cryptography;

namespace RizzziGit.Commons.Cryptography;

public struct AesParameters
{
    public required byte[]? Key;
    public required byte[]? IV;

    public required int BlockSize;
    public required int FeedbackSize;
    public required int KeySize;
    public required CipherMode Mode;
}

public static class AesExtensions
{
    public static byte[] Encrypt(this Aes aes, byte[] bytes)
    {
        using ICryptoTransform transform = aes.CreateEncryptor();

        return transform.TransformFinalBlock(bytes, 0, bytes.Length);
    }

    public static byte[] Decrypt(this Aes aes, byte[] bytes)
    {
        using ICryptoTransform transform = aes.CreateDecryptor();

        return transform.TransformFinalBlock(bytes, 0, bytes.Length);
    }

    public static AesParameters ExportParameters(this Aes aes, bool exportIv, bool exportKey) =>
        new()
        {
            Key = exportKey ? aes.Key : null,
            IV = exportIv ? aes.IV : null,

            BlockSize = aes.BlockSize,
            FeedbackSize = aes.FeedbackSize,
            KeySize = aes.KeySize,
            Mode = aes.Mode,
        };

    public static void ImportParameters(
        this Aes aes,
        AesParameters parameters,
        byte[]? key = null,
        byte[]? iv = null
    )
    {
        aes.KeySize = parameters.KeySize;
        aes.BlockSize = parameters.BlockSize;
        aes.FeedbackSize = parameters.FeedbackSize;
        aes.Mode = parameters.Mode;

        if (key != null)
        {
            aes.Key = key;
        }
        else if (parameters.Key != null)
        {
            aes.Key = parameters.Key;
        }

        if (iv != null)
        {
            aes.IV = iv;
        }
        else if (parameters.IV != null)
        {
            aes.IV = parameters.IV;
        }
    }
}
