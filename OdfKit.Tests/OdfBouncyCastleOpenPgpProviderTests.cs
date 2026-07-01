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

    [Fact]
    public void DecryptSessionKey_TruncatedFiveByteLengthHeader_Throws_CryptographicException()
    {
        var (_, secKeyRingBytes) = GenerateRsaKeyRing();
        var provider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => Array.Empty<char>());

        // 新格式封包標頭（0xC1 = tag 1，PKESK），以 b1=255 觸發 5 位元組長度前綴分支，
        // 宣告 bodyLen=5，但陣列僅 12 位元組——扣除 6 位元組表頭後，剩餘 6 位元組不足以容納
        // version(1) + keyId(8) + algorithm(1) 共 10 位元組的固定尾端結構。
        byte[] packet = new byte[12];
        packet[0] = 0xC1;
        packet[1] = 255;
        packet[2] = 0x00;
        packet[3] = 0x00;
        packet[4] = 0x00;
        packet[5] = 0x05;

        Assert.Throws<CryptographicException>(
            () => provider.DecryptSessionKey(packet, "test"));
    }

    [Fact]
    public void DecryptSessionKey_HighBitBodyLength_Throws_CryptographicException()
    {
        var (_, secKeyRingBytes) = GenerateRsaKeyRing();
        var provider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => Array.Empty<char>());

        // 5 位元組長度前綴的第一個位元組設定最高位元（0x80...），若以有號 int 直接左移會使
        // bodyLen 變成負數；驗證修正後改以 uint 運算並正確拒絕此封包，而非讓負值繞過邊界檢查。
        byte[] packet = new byte[16];
        packet[0] = 0xC1;
        packet[1] = 255;
        packet[2] = 0x80;
        packet[3] = 0x00;
        packet[4] = 0x00;
        packet[5] = 0x00;

        Assert.Throws<CryptographicException>(
            () => provider.DecryptSessionKey(packet, "test"));
    }

    [Fact]
    public void DecryptSessionKey_NullPacket_Throws_ArgumentNullException()
    {
        var (_, secKeyRingBytes) = GenerateRsaKeyRing();
        var provider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => Array.Empty<char>());

        Assert.Throws<ArgumentNullException>(
            () => provider.DecryptSessionKey(null!, "test"));
    }

    [Fact]
    public void DecryptSessionKey_NullPassphraseFromProvider_Throws_ArgumentException()
    {
        var (pubKeyBytes, secKeyRingBytes) = GenerateRsaKeyRing();

        var encProvider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => Array.Empty<char>());

        var recipient = new OdfOpenPgpRecipient { PublicKey = pubKeyBytes, KeyId = "test" };
        byte[] packet = encProvider.EncryptSessionKey(RandomSessionKey(), recipient);

        var nullPassProvider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => null!);

        Assert.Throws<ArgumentException>(
            () => nullPassProvider.DecryptSessionKey(packet, recipient.KeyId));
    }

    [Fact]
    public void OpenPgpDecrypt_WrongKey_ThrowsCryptographicException_NotSwallowed()
    {
        // 產生用金鑰 A 加密的封包
        var (pubA, secA) = GenerateRsaKeyRing();
        var (pubB, secB) = GenerateRsaKeyRing();

        var provA = new OdfBouncyCastleOpenPgpProvider(
            secA,
            _ => Array.Empty<char>());

        var wrongProv = new OdfBouncyCastleOpenPgpProvider(
            secB,
            _ => Array.Empty<char>());

        var recipient = new OdfOpenPgpRecipient { PublicKey = pubA, KeyId = "a" };
        byte[] packet = provA.EncryptSessionKey(RandomSessionKey(), recipient);

        var pgpProvider = new OdfOpenPgpCryptographyProvider(wrongProv);
        var info = new OdfEncryptionInfo
        {
            AlgorithmName = OdfEncryption.OpenPgpAlgorithmUri,
            InitialisationVector = new byte[16],
        };
        info.OpenPgpEncryptedKeys.Add(new OdfOpenPgpEncryptedKeyInfo
        {
            KeyId = "a",
            KeyPacket = packet
        });

        Assert.Throws<CryptographicException>(
            () => pgpProvider.Decrypt(new byte[32], info, new OdfLoadOptions()));
    }

    // ── X25519 測試輔助方法與 Fact ──────────────────────────────────────────────

    private static (byte[] publicKeyBytes, byte[] secretKeyRingBytes) GenerateX25519KeyRing()
    {
        // 主金鑰：RSA（用於驗證/簽章），加密子金鑰：X25519 ECDH
        var rsaParams = new RsaKeyGenerationParameters(BigInteger.ValueOf(65537), s_rng, 2048, 80);
        var rsaKpGen = new RsaKeyPairGenerator();
        rsaKpGen.Init(rsaParams);
        var masterPgpKp = new PgpKeyPair(
            PublicKeyAlgorithmTag.RsaGeneral,
            rsaKpGen.GenerateKeyPair(),
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var x25519Gen = new X25519KeyPairGenerator();
        x25519Gen.Init(new X25519KeyGenerationParameters(s_rng));
        var ecdhPgpKp = new PgpKeyPair(
            PublicKeyAlgorithmTag.ECDH,
            x25519Gen.GenerateKeyPair(),
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var keyRingGen = new PgpKeyRingGenerator(
            PgpSignature.DefaultCertification,
            masterPgpKp,
            "ecdh@odfkit.example",
            SymmetricKeyAlgorithmTag.Aes256,
            Array.Empty<char>(),
            true,
            null, null,
            s_rng);
        keyRingGen.AddSubKey(ecdhPgpKp);

        var pubRing = keyRingGen.GeneratePublicKeyRing();
        var secRing = keyRingGen.GenerateSecretKeyRing();

        using var pubMs = new MemoryStream();
        pubRing.Encode(pubMs);
        using var secMs = new MemoryStream();
        secRing.Encode(secMs);
        return (pubMs.ToArray(), secMs.ToArray());
    }

    [Fact]
    public void EcdhX25519RoundTrip_SessionKey_EncryptThenDecrypt_Succeeds()
    {
        var (pubKeyBytes, secKeyRingBytes) = GenerateX25519KeyRing();
        var provider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => Array.Empty<char>());
        var recipient = new OdfOpenPgpRecipient
        {
            PublicKey = pubKeyBytes,
            KeyId = "ecdh-test",
            Recipient = "ecdh@odfkit.example",
        };
        byte[] originalKey = RandomSessionKey();
        byte[] packet = provider.EncryptSessionKey(originalKey, recipient);
        byte[] recovered = provider.DecryptSessionKey(packet, recipient.KeyId);
        Assert.Equal(originalKey, recovered);
    }

    [Fact]
    public void EcdhX25519RoundTrip_MultipleSessionKeys_AllDecryptCorrectly()
    {
        var (pubKeyBytes, secKeyRingBytes) = GenerateX25519KeyRing();
        var provider = new OdfBouncyCastleOpenPgpProvider(
            secKeyRingBytes,
            _ => Array.Empty<char>());
        var recipient = new OdfOpenPgpRecipient { PublicKey = pubKeyBytes, KeyId = "ecdh-test" };
        for (int i = 0; i < 5; i++)
        {
            byte[] originalKey = RandomSessionKey();
            byte[] packet = provider.EncryptSessionKey(originalKey, recipient);
            byte[] recovered = provider.DecryptSessionKey(packet, recipient.KeyId);
            Assert.Equal(originalKey, recovered);
        }
    }
}
