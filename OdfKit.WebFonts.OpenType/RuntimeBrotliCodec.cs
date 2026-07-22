using System.IO.Compression;
using System.Reflection;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.OpenType;

/// <summary>
/// 依目前執行期提供 Brotli 壓縮與解壓縮，而非只依編譯目標判斷能力。
/// </summary>
internal static class RuntimeBrotliCodec
{
#if !NET10_0_OR_GREATER
    private const string BrotliStreamTypeName =
        "System.IO.Compression.BrotliStream, System.IO.Compression.Brotli";

    private static readonly Type? BrotliStreamType = Type.GetType(BrotliStreamTypeName, throwOnError: false);
#endif

    internal static bool IsAvailable
    {
        get
        {
#if NET10_0_OR_GREATER
            return true;
#else
            return BrotliStreamType is not null
                && typeof(Stream).IsAssignableFrom(BrotliStreamType)
                && FindConstructor(typeof(CompressionLevel)) is not null
                && FindConstructor(typeof(CompressionMode)) is not null;
#endif
        }
    }

    internal static byte[] Compress(ReadOnlySpan<byte> input)
    {
#if NET10_0_OR_GREATER
        int maximumLength = BrotliEncoder.GetMaxCompressedLength(input.Length);
        var output = new byte[maximumLength];
        if (!BrotliEncoder.TryCompress(input, output, out int written, quality: 11, window: 22))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return output.AsSpan(0, written).ToArray();
#else
        using var destination = new MemoryStream();
        using (Stream compressor = CreateStream(destination, CompressionLevel.Optimal))
        {
            byte[] buffer = input.ToArray();
            compressor.Write(buffer, 0, buffer.Length);
        }

        return destination.ToArray();
#endif
    }

    internal static bool TryDecompress(
        ReadOnlySpan<byte> input,
        Span<byte> output,
        out int written)
    {
#if NET10_0_OR_GREATER
        return BrotliDecoder.TryDecompress(input, output, out written);
#else
        written = 0;
        try
        {
            byte[] compressed = input.ToArray();
            using var source = new MemoryStream(compressed, writable: false);
            using Stream decompressor = CreateStream(source, CompressionMode.Decompress);
            var buffer = new byte[Math.Min(81920, Math.Max(1, output.Length))];
            while (written < output.Length)
            {
                int count = decompressor.Read(buffer, 0, Math.Min(buffer.Length, output.Length - written));
                if (count == 0)
                {
                    return false;
                }

                buffer.AsSpan(0, count).CopyTo(output.Slice(written));
                written += count;
            }

            return decompressor.ReadByte() == -1;
        }
        catch (InvalidDataException)
        {
            written = 0;
            return false;
        }
#endif
    }

#if !NET10_0_OR_GREATER
    private static ConstructorInfo? FindConstructor(Type modeType)
        => BrotliStreamType?.GetConstructor([typeof(Stream), modeType, typeof(bool)]);

    private static Stream CreateStream(Stream stream, object mode)
    {
        ConstructorInfo? constructor = FindConstructor(mode.GetType());
        if (constructor?.Invoke([stream, mode, true]) is not Stream result)
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return result;
    }
#endif
}
