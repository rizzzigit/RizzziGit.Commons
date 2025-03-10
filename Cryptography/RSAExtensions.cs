using System.Security.Cryptography;

namespace RizzziGit.Commons.Cryptography;

public sealed record RSAEncrypted
{
    public static RSAEncrypted Encrypt(
        RSAParameters parameters,
        byte[] bytes,
        RSAEncryptionPadding padding,
        bool exportPrivateParameters = false
    )
    {
        using RSA rsa = RSA.Create(parameters);

        return Encrypt(rsa, bytes, padding, exportPrivateParameters);
    }

    public static RSAEncrypted Encrypt(
        RSA rsa,
        byte[] bytes,
        RSAEncryptionPadding padding,
        bool exportPrivateParameters = false
    ) =>
        new()
        {
            Parameters = rsa.ExportParameters(exportPrivateParameters),
            EncryptedBytes = rsa.Encrypt(bytes, padding),
        };

    public required RSAParameters Parameters;

    public required byte[] EncryptedBytes;

    public byte[] Decrypt(RSAParameters parameters, RSAEncryptionPadding padding)
    {
        using RSA rsa = RSA.Create(parameters);

        return Decrypt(rsa, padding);
    }

    public byte[] Decrypt(RSA rsa, RSAEncryptionPadding padding) =>
        rsa.Decrypt(EncryptedBytes, padding);
}

public static class RSAExtensions
{
    public static RSA CreateRSA(this RSAParameters parameters)
    {
        RSA rsa = RSA.Create();
        rsa.ImportParameters(parameters);

        return rsa;
    }

    public static RSAEncrypted SerializedEncrypt(
        this RSA rsa,
        byte[] bytes,
        RSAEncryptionPadding padding,
        bool exportPrivateParameters = false
    ) => RSAEncrypted.Encrypt(rsa, bytes, padding, exportPrivateParameters);

    public static RSAEncrypted SerializedEncrypt(
        this RSAParameters parameters,
        byte[] bytes,
        RSAEncryptionPadding padding,
        bool exportPrivateParameters = false
    ) => RSAEncrypted.Encrypt(parameters, bytes, padding, exportPrivateParameters);
}
