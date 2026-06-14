using System;
using System.IO;
using System.Security.Cryptography;
using OdfKit.Core;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Xunit;

namespace OdfKit.Tests;

public class OdfBouncyCastleOpenPgpProviderTests
{
    private static readonly SecureRandom s_rng = new();

    // ── 測試輔助方法 ─────────────────────────────────────────────────────────

    private static (byte[] publicKeyBytes, byte[] secretKeyRingBytes) GenerateRsaKeyRing(
        char[]? passphrase = null)
    {
        var rsaParams = new RsaKeyGenerationParameters(
            BigInteger.ValueOf(65537), s_rng, 2048, 80);
        var kpGen = new RsaKeyPairGenerator();
        kpGen.Init(rsaParams);
        var keyPair = kpGen.GenerateKeyPair();

        var pgpKeyPair = new PgpKeyPair(
            PublicKeyAlgorithmTag.RsaGeneral,
            keyPair,
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var keyRingGen = new PgpKeyRingGenerator(
            PgpSignature.DefaultCertification,
            pgpKeyPair,
            "test@odfkit.example",
            SymmetricKeyAlgorithmTag.Aes256,
            passphrase ?? Array.Empty<char>(),
            true,
            null, null,
            s_rng);

        var pubRing = keyRingGen.GeneratePublicKeyRing();
        var secRing = keyRingGen.GenerateSecretKeyRing();

        using var pubMs = new MemoryStream();
        pubRing.Encode(pubMs);

        using var secMs = new MemoryStream();
        secRing.Encode(secMs);

        return (pubMs.ToArray(), secMs.ToArray());
    }

    private static byte[] RandomSessionKey()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    // ── 測試案例 ─────────────────────────────────────────────────────────────

    [Fact]
    public void RsaRoundTrip_SessionKey_EncryptThenDecrypt_Succeeds()
    {
        var (pubKeyBytes, secKeyRingBytes) = GenerateRsaKeyRing();

        var provider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => Array.Empty<char>());

        var recipient = new OdfOpenPgpRecipient
        {
            PublicKey = pubKeyBytes,
            KeyId = "test",
            Recipient = "test@odfkit.example"
        };

        byte[] originalKey = RandomSessionKey();

        byte[] encryptedPacket = provider.EncryptSessionKey(originalKey, recipient);
        byte[] decryptedKey = provider.DecryptSessionKey(encryptedPacket, recipient.KeyId);

        Assert.Equal(originalKey, decryptedKey);
    }

    [Fact]
    public void RsaRoundTrip_MultipleSessionKeys_AllDecryptCorrectly()
    {
        var (pubKeyBytes, secKeyRingBytes) = GenerateRsaKeyRing();

        var provider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => Array.Empty<char>());

        var recipient = new OdfOpenPgpRecipient { PublicKey = pubKeyBytes, KeyId = "test" };

        for (int i = 0; i < 5; i++)
        {
            byte[] originalKey = RandomSessionKey();
            byte[] packet = provider.EncryptSessionKey(originalKey, recipient);
            byte[] recovered = provider.DecryptSessionKey(packet, recipient.KeyId);
            Assert.Equal(originalKey, recovered);
        }
    }

    [Fact]
    public void EncryptionOnly_DecryptSessionKey_Throws_InvalidOperationException()
    {
        var provider = new OdfBouncyCastleOpenPgpProvider();
        byte[] dummyPacket = new byte[64];

        Assert.Throws<InvalidOperationException>(
            () => provider.DecryptSessionKey(dummyPacket, "test"));
    }

    [Fact]
    public void WrongPassphrase_DecryptSessionKey_Throws_CryptographicException()
    {
        var (pubKeyBytes, secKeyRingBytes) = GenerateRsaKeyRing(
            passphrase: "correct-passphrase".ToCharArray());

        var encryptProvider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => "correct-passphrase".ToCharArray());

        var recipient = new OdfOpenPgpRecipient { PublicKey = pubKeyBytes, KeyId = "test" };
        byte[] originalKey = RandomSessionKey();
        byte[] packet = encryptProvider.EncryptSessionKey(originalKey, recipient);

        var wrongPassProvider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => "wrong-passphrase".ToCharArray());

        Assert.Throws<CryptographicException>(
            () => wrongPassProvider.DecryptSessionKey(packet, recipient.KeyId));
    }

    [Fact]
    public void EncryptSessionKey_NullSessionKey_Throws_ArgumentNullException()
    {
        var (pubKeyBytes, _) = GenerateRsaKeyRing();
        var provider = new OdfBouncyCastleOpenPgpProvider();
        var recipient = new OdfOpenPgpRecipient { PublicKey = pubKeyBytes };

        Assert.Throws<ArgumentNullException>(
            () => provider.EncryptSessionKey(null!, recipient));
    }

    [Fact]
    public void EncryptSessionKey_NullRecipient_Throws_ArgumentNullException()
    {
        var provider = new OdfBouncyCastleOpenPgpProvider();
        byte[] sessionKey = RandomSessionKey();

        Assert.Throws<ArgumentNullException>(
            () => provider.EncryptSessionKey(sessionKey, null!));
    }

    [Fact]
    public void EncryptSessionKey_EmptyPublicKey_Throws_ArgumentException()
    {
        var provider = new OdfBouncyCastleOpenPgpProvider();
        byte[] sessionKey = RandomSessionKey();
        var recipient = new OdfOpenPgpRecipient { PublicKey = [], KeyId = "test" };

        Assert.Throws<ArgumentException>(
            () => provider.EncryptSessionKey(sessionKey, recipient));
    }

    [Fact]
    public void DecryptSessionKey_InvalidPacket_Throws_CryptographicException()
    {
        var (_, secKeyRingBytes) = GenerateRsaKeyRing();
        var provider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => Array.Empty<char>());

        byte[] garbage = new byte[32];
        RandomNumberGenerator.Fill(garbage);
        garbage[0] = 0x80; // 設定 PGP 封包標頭起始位元，但內容無效

        Assert.Throws<CryptographicException>(
            () => provider.DecryptSessionKey(garbage, "test"));
    }
}
