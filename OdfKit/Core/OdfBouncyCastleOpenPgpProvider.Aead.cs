using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// Adds LibrePGP AEAD message decryption to the BouncyCastle-backed provider.
/// 為 BouncyCastle 提供者加入 LibrePGP AEAD 訊息解密。
/// </summary>
public sealed partial class OdfBouncyCastleOpenPgpProvider
{
    private const int OpenPgpAeadPacketTag = 20;
    private const int OpenPgpAeadTagLength = 16;

    private byte[]? TryDecryptAeadMessage(byte[] encryptedMessage)
    {
        var packets = new List<OpenPgpPacket>();
        int offset = 0;
        while (offset < encryptedMessage.Length)
        {
            packets.Add(ReadPacket(encryptedMessage, ref offset));
        }

        OpenPgpPacket? aeadPacket = null;
        foreach (OpenPgpPacket packet in packets)
        {
            if (packet.Tag == OpenPgpAeadPacketTag)
            {
                aeadPacket = packet;
                break;
            }
        }

        if (aeadPacket is null)
        {
            return null;
        }

        foreach (OpenPgpPacket packet in packets)
        {
            if (packet.Tag != 1)
            {
                continue;
            }

            try
            {
                byte[] sessionKey = DecryptPkeskSessionKey(packet.Encoded);
                try
                {
                    byte[] clearPacketBytes = DecryptAeadPacket(aeadPacket.Value.Body, sessionKey);
                    return ReadLiteralMessage(clearPacketBytes);
                }
                finally
                {
                    Array.Clear(sessionKey, 0, sessionKey.Length);
                }
            }
            catch (InvalidOperationException)
            {
                // 此 PKESK 不是提供者持有的私鑰；繼續嘗試下一位收件者。
            }
        }

        return null;
    }

    private static byte[] DecryptAeadPacket(byte[] body, byte[] sessionKey)
    {
        if (body.Length < 4 + 15 + OpenPgpAeadTagLength ||
            body[0] != 1 ||
            body[1] is not ((byte)SymmetricKeyAlgorithmTag.Aes128)
                and not ((byte)SymmetricKeyAlgorithmTag.Aes192)
                and not ((byte)SymmetricKeyAlgorithmTag.Aes256) ||
            body[2] != (byte)AeadAlgorithmTag.Ocb)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        int requiredKeyLength = body[1] switch
        {
            (byte)SymmetricKeyAlgorithmTag.Aes128 => 16,
            (byte)SymmetricKeyAlgorithmTag.Aes192 => 24,
            _ => 32
        };
        if (sessionKey.Length != requiredKeyLength)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        int chunkSizeOctet = body[3];
        if (chunkSizeOctet > 30)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        long chunkSizeLong = 1L << (chunkSizeOctet + 6);
        int chunkSize = chunkSizeLong > int.MaxValue ? int.MaxValue : (int)chunkSizeLong;
        byte[] initialNonce = new byte[15];
        Buffer.BlockCopy(body, 4, initialNonce, 0, initialNonce.Length);

        int position = 4 + initialNonce.Length;
        long chunkIndex = 0;
        long totalPlaintextLength = 0;
        using var plaintext = new MemoryStream();

        while (body.Length - position > OpenPgpAeadTagLength)
        {
            int remaining = body.Length - position;
            int encryptedChunkLength = remaining > chunkSize + (2 * OpenPgpAeadTagLength)
                ? chunkSize + OpenPgpAeadTagLength
                : remaining - OpenPgpAeadTagLength;
            if (encryptedChunkLength <= OpenPgpAeadTagLength)
            {
                throw new CryptographicException(
                    OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
            }

            byte[] additionalData = CreateAeadAdditionalData(
                body[1],
                body[2],
                body[3],
                chunkIndex,
                totalPlaintextLength: null);
            byte[] nonce = CreateAeadNonce(initialNonce, chunkIndex);
            byte[] clearChunk = ProcessOcb(
                sessionKey,
                nonce,
                additionalData,
                body,
                position,
                encryptedChunkLength);
            plaintext.Write(clearChunk, 0, clearChunk.Length);
            totalPlaintextLength += clearChunk.Length;
            Array.Clear(clearChunk, 0, clearChunk.Length);
            position += encryptedChunkLength;
            chunkIndex++;
        }

        if (body.Length - position != OpenPgpAeadTagLength)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        byte[] finalAdditionalData = CreateAeadAdditionalData(
            body[1],
            body[2],
            body[3],
            chunkIndex,
            totalPlaintextLength);
        byte[] finalNonce = CreateAeadNonce(initialNonce, chunkIndex);
        byte[] finalClear = ProcessOcb(
            sessionKey,
            finalNonce,
            finalAdditionalData,
            body,
            position,
            OpenPgpAeadTagLength);
        if (finalClear.Length != 0)
        {
            Array.Clear(finalClear, 0, finalClear.Length);
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        return plaintext.ToArray();
    }

    private static byte[] ProcessOcb(
        byte[] key,
        byte[] nonce,
        byte[] additionalData,
        byte[] input,
        int offset,
        int count)
    {
#pragma warning disable CS0618 // BouncyCastle 2.6.2 尚未提供 OCB/AES 的 NewInstance 工廠。
        var cipher = new OcbBlockCipher(new AesEngine(), new AesEngine());
#pragma warning restore CS0618
        cipher.Init(
            forEncryption: false,
            new AeadParameters(new KeyParameter(key), OpenPgpAeadTagLength * 8, nonce, additionalData));
        byte[] output = new byte[cipher.GetOutputSize(count)];
        try
        {
            int outputLength = cipher.ProcessBytes(input, offset, count, output, 0);
            outputLength += cipher.DoFinal(output, outputLength);
            if (outputLength == output.Length)
            {
                return output;
            }

            byte[] exactOutput = new byte[outputLength];
            Buffer.BlockCopy(output, 0, exactOutput, 0, outputLength);
            Array.Clear(output, 0, output.Length);
            return exactOutput;
        }
        catch (InvalidCipherTextException ex)
        {
            Array.Clear(output, 0, output.Length);
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"),
                ex);
        }
    }

    private static byte[] CreateAeadAdditionalData(
        byte symmetricAlgorithm,
        byte aeadAlgorithm,
        byte chunkSize,
        long chunkIndex,
        long? totalPlaintextLength)
    {
        byte[] additionalData = new byte[totalPlaintextLength.HasValue ? 21 : 13];
        additionalData[0] = 0xD4;
        additionalData[1] = 1;
        additionalData[2] = symmetricAlgorithm;
        additionalData[3] = aeadAlgorithm;
        additionalData[4] = chunkSize;
        WriteUInt64BigEndian(additionalData, 5, (ulong)chunkIndex);
        if (totalPlaintextLength.HasValue)
        {
            WriteUInt64BigEndian(additionalData, 13, (ulong)totalPlaintextLength.Value);
        }

        return additionalData;
    }

    private static byte[] CreateAeadNonce(byte[] initialNonce, long chunkIndex)
    {
        byte[] nonce = (byte[])initialNonce.Clone();
        ulong index = (ulong)chunkIndex;
        for (int i = 0; i < sizeof(ulong); i++)
        {
            nonce[nonce.Length - 1 - i] ^= (byte)index;
            index >>= 8;
        }

        return nonce;
    }

    private static void WriteUInt64BigEndian(byte[] output, int offset, ulong value)
    {
        for (int i = sizeof(ulong) - 1; i >= 0; i--)
        {
            output[offset + i] = (byte)value;
            value >>= 8;
        }
    }

    private static byte[] ReadLiteralMessage(byte[] clearPacketBytes)
    {
        using var input = new MemoryStream(clearPacketBytes, writable: false);
        var factory = new Org.BouncyCastle.Bcpg.OpenPgp.PgpObjectFactory(input);
        object? message = factory.NextPgpObject();
        if (message is Org.BouncyCastle.Bcpg.OpenPgp.PgpCompressedData compressedData)
        {
            using Stream compressedStream = compressedData.GetDataStream();
            message = new Org.BouncyCastle.Bcpg.OpenPgp.PgpObjectFactory(compressedStream).NextPgpObject();
        }

        if (message is not Org.BouncyCastle.Bcpg.OpenPgp.PgpLiteralData literalData)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        using Stream literalStream = literalData.GetInputStream();
        using var output = new MemoryStream();
        literalStream.CopyTo(output);
        return output.ToArray();
    }

    private static OpenPgpPacket ReadPacket(byte[] message, ref int offset)
    {
        int packetStart = offset;
        if (offset >= message.Length)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        int header = message[offset++];
        if ((header & 0x80) == 0)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        using var body = new MemoryStream();
        if ((header & 0x40) == 0)
        {
            int legacyTag = (header >> 2) & 0x0F;
            int legacyLength = ReadLegacyPacketLength(message, ref offset, header & 0x03);
            if (legacyLength < 0 || legacyLength > message.Length - offset)
            {
                throw new CryptographicException(
                    OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
            }

            body.Write(message, offset, legacyLength);
            offset += legacyLength;
            byte[] legacyEncoded = new byte[offset - packetStart];
            Buffer.BlockCopy(message, packetStart, legacyEncoded, 0, legacyEncoded.Length);
            return new OpenPgpPacket(legacyTag, legacyEncoded, body.ToArray());
        }

        int tag = header & 0x3F;
        bool partial;
        do
        {
            int length = ReadNewPacketLength(message, ref offset, out partial);
            if (length < 0 || length > message.Length - offset)
            {
                throw new CryptographicException(
                    OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
            }

            body.Write(message, offset, length);
            offset += length;
        }
        while (partial);

        byte[] encoded = new byte[offset - packetStart];
        Buffer.BlockCopy(message, packetStart, encoded, 0, encoded.Length);
        return new OpenPgpPacket(tag, encoded, body.ToArray());
    }

    private static int ReadLegacyPacketLength(byte[] message, ref int offset, int lengthType)
    {
        int bytesToRead = lengthType switch
        {
            0 => 1,
            1 => 2,
            2 => 4,
            _ => 0
        };
        if (bytesToRead == 0)
        {
            return message.Length - offset;
        }

        if (message.Length - offset < bytesToRead)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        uint length = 0;
        for (int i = 0; i < bytesToRead; i++)
        {
            length = (length << 8) | message[offset++];
        }

        if (length > int.MaxValue)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        return (int)length;
    }

    private static int ReadNewPacketLength(byte[] message, ref int offset, out bool partial)
    {
        if (offset >= message.Length)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        int first = message[offset++];
        partial = false;
        if (first < 192)
        {
            return first;
        }

        if (first <= 223)
        {
            if (offset >= message.Length)
            {
                throw new CryptographicException(
                    OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
            }

            return ((first - 192) << 8) + message[offset++] + 192;
        }

        if (first < 255)
        {
            partial = true;
            return 1 << (first & 0x1F);
        }

        if (message.Length - offset < 4)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        uint length =
            ((uint)message[offset] << 24) |
            ((uint)message[offset + 1] << 16) |
            ((uint)message[offset + 2] << 8) |
            message[offset + 3];
        offset += 4;
        if (length > int.MaxValue)
        {
            throw new CryptographicException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));
        }

        return (int)length;
    }

    private readonly struct OpenPgpPacket(int tag, byte[] encoded, byte[] body)
    {
        public int Tag { get; } = tag;

        public byte[] Encoded { get; } = encoded;

        public byte[] Body { get; } = body;
    }
}
