using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OdfKit.Export.PdfInternal;

/// <summary>
/// PDF 標準安全處理常式（RC4 40／128 位元、AES-128、AES-256）：以使用者或擁有者密碼取得檔案金鑰，
/// 並逐物件解密／加密字串與串流。
/// The PDF standard security handler (RC4 40/128-bit, AES-128, AES-256): derives the file key from the
/// user or owner password and decrypts/encrypts strings and streams per object.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA5351:Do Not Use Broken Cryptographic Algorithms",
    Justification = "The PDF standard security handler (revisions 2-4) mandates MD5 and RC4 for key derivation and object encryption; this code only reads or rewrites existing encrypted files.")]
internal sealed class PdfDecryptor
{
    private static readonly byte[] PasswordPadding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    private enum Method
    {
        None,
        Rc4,
        AesV2,
        AesV3,
    }

    private readonly byte[] _fileKey;
    private readonly Method _streamMethod;
    private readonly Method _stringMethod;

    private PdfDecryptor(byte[] fileKey, Method streamMethod, Method stringMethod, bool encryptMetadata)
    {
        _fileKey = fileKey;
        _streamMethod = streamMethod;
        _stringMethod = stringMethod;
        EncryptMetadata = encryptMetadata;
    }

    internal bool EncryptMetadata { get; }

    /// <summary>
    /// 建立解密器；密碼不正確或加密方式不支援時擲出 <see cref="CryptographicException"/>。
    /// </summary>
    internal static PdfDecryptor Create(PdfFile file, PdfDict encrypt, string? password)
    {
        long version = Number(file, encrypt, "V", 0);
        long revision = Number(file, encrypt, "R", 2);
        long lengthBits = Number(file, encrypt, "Length", 40);
        byte[] owner = Bytes(file, encrypt, "O");
        byte[] user = Bytes(file, encrypt, "U");
        int permissions = unchecked((int)Number(file, encrypt, "P", 0));
        bool encryptMetadata = !(file.Resolve(encrypt.TryGetValue("EncryptMetadata", out object? em) ? em : null) is bool flag && !flag);
        byte[] documentId = [];
        if (file.Resolve(file.Trailer.TryGetValue("ID", out object? idValue) ? idValue : null) is List<object?> ids && ids.Count > 0
            && file.Resolve(ids[0]) is PdfString idString)
        {
            documentId = idString.Value;
        }

        if (!(file.Resolve(encrypt.TryGetValue("Filter", out object? filter) ? filter : null) is PdfName { Value: "Standard" }))
        {
            throw new CryptographicException("Unsupported PDF security handler.");
        }

        Method streamMethod;
        Method stringMethod;
        if (version is 1 or 2)
        {
            streamMethod = stringMethod = Method.Rc4;
        }
        else if (version is 4 or 5)
        {
            streamMethod = ResolveFilter(file, encrypt, "StmF");
            stringMethod = ResolveFilter(file, encrypt, "StrF");
            if (version == 4)
            {
                // V4 的金鑰長度固定為 128 位元。
                lengthBits = 128;
            }
        }
        else
        {
            throw new CryptographicException("Unsupported PDF encryption version.");
        }

        byte[] passwordBytes = password is null ? [] : Encoding.UTF8.GetBytes(password);
        byte[] key;
        if (revision >= 5)
        {
            key = DeriveKeyR6(revision, encrypt, file, owner, user, passwordBytes);
        }
        else
        {
            int keyLength = revision == 2 ? 5 : (int)Math.Max(5, Math.Min(16, lengthBits / 8));
            byte[]? candidate = TryUserPassword(revision, keyLength, owner, user, permissions, documentId, encryptMetadata, ToLatin1(password));
            if (candidate is null)
            {
                // 以擁有者密碼取回使用者密碼後再試。
                byte[] recovered = RecoverUserPassword(revision, keyLength, owner, ToLatin1(password));
                candidate = TryUserPassword(revision, keyLength, owner, user, permissions, documentId, encryptMetadata, recovered);
            }

            key = candidate ?? throw new CryptographicException("The PDF password is incorrect.");
        }

        return new PdfDecryptor(key, streamMethod, stringMethod, encryptMetadata);
    }

    internal byte[] DecryptString(byte[] data, int objectNumber, int generation) => Transform(data, objectNumber, generation, _stringMethod, encrypt: false);

    internal byte[] DecryptStream(byte[] data, int objectNumber, int generation) => Transform(data, objectNumber, generation, _streamMethod, encrypt: false);

    internal byte[] EncryptString(byte[] data, int objectNumber, int generation) => Transform(data, objectNumber, generation, _stringMethod, encrypt: true);

    internal byte[] EncryptStream(byte[] data, int objectNumber, int generation) => Transform(data, objectNumber, generation, _streamMethod, encrypt: true);

    // ---------- 金鑰推導 ----------

    private static Method ResolveFilter(PdfFile file, PdfDict encrypt, string key)
    {
        if (file.Resolve(encrypt.TryGetValue(key, out object? nameValue) ? nameValue : null) is not PdfName name || name.Value == "Identity")
        {
            return Method.None;
        }

        PdfDict? filters = file.ResolveDict(encrypt.TryGetValue("CF", out object? cf) ? cf : null);
        PdfDict? entry = filters is not null && filters.TryGetValue(name.Value, out object? entryValue) ? file.ResolveDict(entryValue) : null;
        string? method = entry is not null && file.Resolve(entry.TryGetValue("CFM", out object? cfm) ? cfm : null) is PdfName cfmName ? cfmName.Value : null;
        return method switch
        {
            "V2" => Method.Rc4,
            "AESV2" => Method.AesV2,
            "AESV3" => Method.AesV3,
            "None" => Method.None,
            _ => throw new CryptographicException("Unsupported PDF crypt filter."),
        };
    }

    private static long Number(PdfFile file, PdfDict dictionary, string key, long fallback) =>
        file.Resolve(dictionary.TryGetValue(key, out object? value) ? value : null) is long number ? number : fallback;

    private static byte[] Bytes(PdfFile file, PdfDict dictionary, string key) =>
        file.Resolve(dictionary.TryGetValue(key, out object? value) ? value : null) is PdfString text ? text.Value : [];

    private static byte[] ToLatin1(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return [];
        }

        var bytes = new byte[password!.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)password[i];
        }

        return bytes;
    }

    private static byte[] PadPassword(byte[] password)
    {
        var padded = new byte[32];
        int count = Math.Min(32, password.Length);
        Buffer.BlockCopy(password, 0, padded, 0, count);
        Buffer.BlockCopy(PasswordPadding, 0, padded, count, 32 - count);
        return padded;
    }

    private static byte[]? TryUserPassword(long revision, int keyLength, byte[] owner, byte[] user, int permissions, byte[] documentId, bool encryptMetadata, byte[] password)
    {
        byte[] key = ComputeFileKey(revision, keyLength, owner, permissions, documentId, encryptMetadata, password);
        if (revision == 2)
        {
            byte[] expected = Rc4(key, PasswordPadding);
            return user.Length >= 32 && ConstantTimeEquals(expected, 0, user, 0, 32) ? key : null;
        }

        byte[] hash;
        using (var md5 = MD5.Create())
        {
            md5.TransformBlock(PasswordPadding, 0, 32, null, 0);
            md5.TransformFinalBlock(documentId, 0, documentId.Length);
            hash = md5.Hash!;
        }

        byte[] value = Rc4(key, hash);
        for (int round = 1; round <= 19; round++)
        {
            var roundKey = new byte[key.Length];
            for (int i = 0; i < key.Length; i++)
            {
                roundKey[i] = (byte)(key[i] ^ round);
            }

            value = Rc4(roundKey, value);
        }

        return user.Length >= 16 && ConstantTimeEquals(value, 0, user, 0, 16) ? key : null;
    }

    private static byte[] ComputeFileKey(long revision, int keyLength, byte[] owner, int permissions, byte[] documentId, bool encryptMetadata, byte[] password)
    {
        using var md5 = MD5.Create();
        byte[] padded = PadPassword(password);
        md5.TransformBlock(padded, 0, 32, null, 0);
        md5.TransformBlock(owner, 0, Math.Min(32, owner.Length), null, 0);
        byte[] p = [(byte)permissions, (byte)(permissions >> 8), (byte)(permissions >> 16), (byte)(permissions >> 24)];
        md5.TransformBlock(p, 0, 4, null, 0);
        md5.TransformBlock(documentId, 0, documentId.Length, null, 0);
        if (revision >= 4 && !encryptMetadata)
        {
            byte[] marker = [0xFF, 0xFF, 0xFF, 0xFF];
            md5.TransformBlock(marker, 0, 4, null, 0);
        }

        md5.TransformFinalBlock([], 0, 0);
        byte[] hash = md5.Hash!;
        if (revision >= 3)
        {
            for (int i = 0; i < 50; i++)
            {
                hash = Md5(hash, 0, keyLength);
            }
        }

        var key = new byte[keyLength];
        Buffer.BlockCopy(hash, 0, key, 0, keyLength);
        return key;
    }

    private static byte[] Md5(byte[] data, int offset, int count)
    {
#if NETSTANDARD2_0
        using var md5 = MD5.Create();
        return md5.ComputeHash(data, offset, count);
#else
        return MD5.HashData(new ReadOnlySpan<byte>(data, offset, count));
#endif
    }

    private static byte[] Sha256(byte[] data)
    {
#if NETSTANDARD2_0
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(data);
#else
        return SHA256.HashData(data);
#endif
    }

    private static byte[] RecoverUserPassword(long revision, int keyLength, byte[] owner, byte[] ownerPassword)
    {
        byte[] padded = PadPassword(ownerPassword);
        byte[] hash = Md5(padded, 0, padded.Length);
        if (revision >= 3)
        {
            for (int i = 0; i < 50; i++)
            {
                hash = Md5(hash, 0, hash.Length);
            }
        }

        var key = new byte[keyLength];
        Buffer.BlockCopy(hash, 0, key, 0, keyLength);
        byte[] value = new byte[Math.Min(32, owner.Length)];
        Buffer.BlockCopy(owner, 0, value, 0, value.Length);
        if (revision == 2)
        {
            return Rc4(key, value);
        }

        for (int round = 19; round >= 0; round--)
        {
            var roundKey = new byte[key.Length];
            for (int i = 0; i < key.Length; i++)
            {
                roundKey[i] = (byte)(key[i] ^ round);
            }

            value = Rc4(roundKey, value);
        }

        return value;
    }

    // ---------- AES-256（R5／R6） ----------

    private static byte[] DeriveKeyR6(long revision, PdfDict encrypt, PdfFile file, byte[] owner, byte[] user, byte[] password)
    {
        byte[] ue = Bytes(file, encrypt, "UE");
        byte[] oe = Bytes(file, encrypt, "OE");
        if (user.Length < 48 || owner.Length < 48 || ue.Length < 32 || oe.Length < 32)
        {
            throw new CryptographicException("The PDF encryption dictionary is malformed.");
        }

        byte[] truncated = password.Length > 127 ? password.AsSpanToArray(0, 127) : password;
        byte[] userValidationSalt = user.AsSpanToArray(32, 8);
        byte[] userKeySalt = user.AsSpanToArray(40, 8);
        byte[] ownerValidationSalt = owner.AsSpanToArray(32, 8);
        byte[] ownerKeySalt = owner.AsSpanToArray(40, 8);
        byte[] u48 = user.AsSpanToArray(0, 48);

        if (ConstantTimeEquals(Hash(revision, truncated, userValidationSalt, []), 0, user, 0, 32))
        {
            return AesCbcNoPadding(Hash(revision, truncated, userKeySalt, []), ue);
        }

        if (ConstantTimeEquals(Hash(revision, truncated, ownerValidationSalt, u48), 0, owner, 0, 32))
        {
            return AesCbcNoPadding(Hash(revision, truncated, ownerKeySalt, u48), oe);
        }

        throw new CryptographicException("The PDF password is incorrect.");
    }

    // 演算法 2.B（R6）；R5 只用 SHA-256。
    private static byte[] Hash(long revision, byte[] password, byte[] salt, byte[] udata)
    {
        byte[] input = Concat(password, salt, udata);
        byte[] k = Sha256(input);

        if (revision < 6)
        {
            return k;
        }

        for (int round = 0; ; round++)
        {
            byte[] block = Concat(password, k, udata);
            var repeated = new byte[block.Length * 64];
            for (int i = 0; i < 64; i++)
            {
                Buffer.BlockCopy(block, 0, repeated, i * block.Length, block.Length);
            }

            byte[] e;
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;
                aes.Key = k.AsSpanToArray(0, 16);
                aes.IV = k.AsSpanToArray(16, 16);
                using ICryptoTransform encryptor = aes.CreateEncryptor();
                e = encryptor.TransformFinalBlock(repeated, 0, repeated.Length);
            }

            int mod = 0;
            for (int i = 0; i < 16; i++)
            {
                mod += e[i];
            }

            mod %= 3;
            using (HashAlgorithm hash = mod == 0 ? SHA256.Create() : (mod == 1 ? SHA384.Create() : (HashAlgorithm)SHA512.Create()))
            {
                k = hash.ComputeHash(e);
            }

            int completed = round + 1;
            if (completed >= 64 && e[e.Length - 1] <= completed - 32)
            {
                break;
            }
        }

        return k.AsSpanToArray(0, 32);
    }

    private static byte[] AesCbcNoPadding(byte[] key, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        aes.IV = new byte[16];
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(data, 0, 32);
    }

    // ---------- 逐物件加解密 ----------

    private byte[] Transform(byte[] data, int objectNumber, int generation, Method method, bool encrypt)
    {
        switch (method)
        {
            case Method.None:
                return data;
            case Method.Rc4:
                return Rc4(ObjectKey(objectNumber, generation, aes: false), data);
            case Method.AesV2:
                return AesTransform(ObjectKey(objectNumber, generation, aes: true), data, encrypt);
            default:
                return AesTransform(_fileKey, data, encrypt);
        }
    }

    private byte[] ObjectKey(int objectNumber, int generation, bool aes)
    {
        var material = new byte[_fileKey.Length + 5 + (aes ? 4 : 0)];
        Buffer.BlockCopy(_fileKey, 0, material, 0, _fileKey.Length);
        int position = _fileKey.Length;
        material[position++] = (byte)objectNumber;
        material[position++] = (byte)(objectNumber >> 8);
        material[position++] = (byte)(objectNumber >> 16);
        material[position++] = (byte)generation;
        material[position++] = (byte)(generation >> 8);
        if (aes)
        {
            material[position++] = 0x73;
            material[position++] = 0x41;
            material[position++] = 0x6C;
            material[position] = 0x54;
        }

        byte[] hash = Md5(material, 0, material.Length);
        int length = Math.Min(_fileKey.Length + 5, 16);
        return hash.AsSpanToArray(0, length);
    }

    private static byte[] AesTransform(byte[] key, byte[] data, bool encrypt)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Key = key;
        if (encrypt)
        {
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();
            using ICryptoTransform encryptor = aes.CreateEncryptor();
            byte[] body = encryptor.TransformFinalBlock(data, 0, data.Length);
            return Concat(aes.IV, body, []);
        }

        if (data.Length < 32 || (data.Length % 16) != 0)
        {
            // 長度不合法的資料視為空內容，避免例外中斷讀取。
            return data.Length < 16 ? [] : data.AsSpanToArray(16, data.Length - 16 - ((data.Length - 16) % 16));
        }

        aes.Padding = PaddingMode.PKCS7;
        aes.IV = data.AsSpanToArray(0, 16);
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        try
        {
            return decryptor.TransformFinalBlock(data, 16, data.Length - 16);
        }
        catch (CryptographicException)
        {
            // 填充不合法時回傳原始區塊，讓上層解碼時自然失敗而不是整份文件失敗。
            aes.Padding = PaddingMode.None;
            using ICryptoTransform raw = aes.CreateDecryptor();
            return raw.TransformFinalBlock(data, 16, data.Length - 16);
        }
    }

    private static byte[] Rc4(byte[] key, byte[] data)
    {
        var s = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            s[i] = (byte)i;
        }

        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xFF;
            (s[i], s[j]) = (s[j], s[i]);
        }

        var output = new byte[data.Length];
        int a = 0;
        int b = 0;
        for (int n = 0; n < data.Length; n++)
        {
            a = (a + 1) & 0xFF;
            b = (b + s[a]) & 0xFF;
            (s[a], s[b]) = (s[b], s[a]);
            output[n] = (byte)(data[n] ^ s[(s[a] + s[b]) & 0xFF]);
        }

        return output;
    }

    private static bool ConstantTimeEquals(byte[] a, int aOffset, byte[] b, int bOffset, int length)
    {
        if (aOffset + length > a.Length || bOffset + length > b.Length)
        {
            return false;
        }

        int difference = 0;
        for (int i = 0; i < length; i++)
        {
            difference |= a[aOffset + i] ^ b[bOffset + i];
        }

        return difference == 0;
    }

    private static byte[] Concat(byte[] a, byte[] b, byte[] c)
    {
        var result = new byte[a.Length + b.Length + c.Length];
        Buffer.BlockCopy(a, 0, result, 0, a.Length);
        Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
        Buffer.BlockCopy(c, 0, result, a.Length + b.Length, c.Length);
        return result;
    }
}

internal static class PdfByteExtensions
{
    internal static byte[] AsSpanToArray(this byte[] source, int offset, int length)
    {
        var copy = new byte[length];
        Buffer.BlockCopy(source, offset, copy, 0, length);
        return copy;
    }
}
