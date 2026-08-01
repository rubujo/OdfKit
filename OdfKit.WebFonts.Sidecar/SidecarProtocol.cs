using System.Text;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Sidecar;

internal enum SidecarOperation : byte
{
    Health = 1,
    Generate = 2,
    FilterSupportedSequences = 3
}

internal enum SidecarStatus : byte
{
    Success = 0,
    Unauthorized = 1,
    InvalidRequest = 2,
    Unsupported = 3,
    QueueFull = 4,
    Cancelled = 5,
    ServerError = 6,
    VersionMismatch = 7
}

internal readonly record struct SidecarFrame(
    SidecarOperation Operation,
    SidecarStatus Status,
    byte[] Payload);

internal static class SidecarProtocol
{
    public const ushort Version = 1;
    public const int HeaderLength = 12;
    private const uint RequestMagic = 0x5746444F;
    private const uint ResponseMagic = 0x5246444F;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static byte[] CreateHealthRequest(string token)
    {
        ValidateStringForWire(token, 512);
        return CreatePayload(writer => WriteString(writer, token));
    }

    public static byte[] CreateGenerateRequest(string token, WebFontSubsetRequest request)
    {
        ValidateStringForWire(token, 512);
        ValidateRequestForWire(request);
        return CreatePayload(writer =>
        {
            WriteString(writer, token);
            WriteRequest(writer, request);
        });
    }

    public static byte[] CreateFilterRequest(
        string token,
        WebFontFaceIdentity face,
        IReadOnlyList<WebFontTextSequence> sequences)
    {
        ValidateStringForWire(token, 512);
        ValidateFaceForWire(face);
        ValidateSequencesForWire(sequences);
        return CreatePayload(writer =>
        {
            WriteString(writer, token);
            WriteFace(writer, face);
            WriteSequences(writer, sequences);
        });
    }

    public static string ReadToken(BinaryReader reader)
        => ReadString(reader, 512);

    public static WebFontSubsetRequest ReadRequest(BinaryReader reader)
    {
        WebFontFaceIdentity face = ReadFace(reader);
        string profileId = ReadString(reader, 1024);
        string fontFamily = ReadString(reader, 1024);
        WebFontTextSequence[] sequences = ReadSequences(reader, 4096, 65536);
        WebFontFormat[] formats = ReadEnums<WebFontFormat>(reader, 4);
        WebFontBrowserTarget[] browserTargets = ReadEnums<WebFontBrowserTarget>(reader, 3);
        EnsurePayloadConsumed(reader);
        return new WebFontSubsetRequest
        {
            Face = face,
            ProfileId = profileId,
            FontFamily = fontFamily,
            Sequences = sequences,
            Formats = formats,
            RequiredBrowserTargets = browserTargets
        };
    }

    public static (WebFontFaceIdentity Face, WebFontTextSequence[] Sequences) ReadFilterRequest(
        BinaryReader reader)
    {
        WebFontFaceIdentity face = ReadFace(reader);
        WebFontTextSequence[] sequences = ReadSequences(reader, 4096, 65536);
        EnsurePayloadConsumed(reader);
        return (face, sequences);
    }

    public static byte[] CreateManifestResponse(WebFontManifest manifest)
        => CreatePayload(writer => WriteManifest(writer, manifest));

    public static WebFontManifest ReadManifest(byte[] payload)
    {
        using BinaryReader reader = CreateReader(payload);
        WebFontManifest manifest = ReadManifest(reader);
        EnsurePayloadConsumed(reader);
        return manifest;
    }

    public static byte[] CreateSequencesResponse(IReadOnlyList<WebFontTextSequence> sequences)
        => CreatePayload(writer => WriteSequences(writer, sequences));

    public static IReadOnlyList<WebFontTextSequence> ReadSequencesResponse(byte[] payload)
    {
        using BinaryReader reader = CreateReader(payload);
        WebFontTextSequence[] sequences = ReadSequences(reader, 4096, 65536);
        EnsurePayloadConsumed(reader);
        return sequences;
    }

    public static byte[] CreateHealthResponse(bool isWoff2Available, string runtimeIdentifier)
        => CreatePayload(writer =>
        {
            writer.Write(isWoff2Available);
            WriteString(writer, runtimeIdentifier);
        });

    public static WebFontSidecarHealth ReadHealth(byte[] payload)
    {
        using BinaryReader reader = CreateReader(payload);
        var health = new WebFontSidecarHealth
        {
            ProtocolVersion = Version,
            IsWoff2Available = reader.ReadBoolean(),
            RuntimeIdentifier = ReadString(reader, 256)
        };
        EnsurePayloadConsumed(reader);
        return health;
    }

    public static BinaryReader CreateReader(byte[] payload)
        => new(new MemoryStream(payload, writable: false), StrictUtf8, leaveOpen: false);

    public static async Task WriteRequestFrameAsync(
        Stream stream,
        SidecarOperation operation,
        byte[] payload,
        CancellationToken cancellationToken)
        => await WriteFrameAsync(
            stream,
            RequestMagic,
            operation,
            SidecarStatus.Success,
            payload,
            cancellationToken).ConfigureAwait(false);

    public static async Task WriteResponseFrameAsync(
        Stream stream,
        SidecarOperation operation,
        SidecarStatus status,
        byte[] payload,
        CancellationToken cancellationToken)
        => await WriteFrameAsync(
            stream,
            ResponseMagic,
            operation,
            status,
            payload,
            cancellationToken).ConfigureAwait(false);

    public static Task<SidecarFrame> ReadRequestFrameAsync(
        Stream stream,
        int maximumPayloadBytes,
        CancellationToken cancellationToken)
        => ReadFrameAsync(stream, RequestMagic, maximumPayloadBytes, cancellationToken);

    public static Task<SidecarFrame> ReadResponseFrameAsync(
        Stream stream,
        int maximumPayloadBytes,
        CancellationToken cancellationToken)
        => ReadFrameAsync(stream, ResponseMagic, maximumPayloadBytes, cancellationToken);

    private static async Task WriteFrameAsync(
        Stream stream,
        uint magic,
        SidecarOperation operation,
        SidecarStatus status,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[HeaderLength];
        WriteUInt32(header, 0, magic);
        WriteUInt16(header, 4, Version);
        header[6] = (byte)operation;
        header[7] = (byte)status;
        WriteInt32(header, 8, payload.Length);
        await OdfKit.Internal.OdfStreamHelper.WriteAsync(stream, header, 0, header.Length, cancellationToken).ConfigureAwait(false);
        if (payload.Length > 0)
        {
            await OdfKit.Internal.OdfStreamHelper.WriteAsync(stream, payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SidecarFrame> ReadFrameAsync(
        Stream stream,
        uint expectedMagic,
        int maximumPayloadBytes,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[HeaderLength];
        await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);
        uint magic = ReadUInt32(header, 0);
        ushort version = ReadUInt16(header, 4);
        int payloadLength = ReadInt32(header, 8);
        if (magic != expectedMagic
            || version != Version
            || !OdfKit.Internal.OdfEnumHelper.IsDefined((SidecarOperation)header[6])
            || !OdfKit.Internal.OdfEnumHelper.IsDefined((SidecarStatus)header[7])
            || payloadLength < 0
            || payloadLength > maximumPayloadBytes)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        byte[] payload = new byte[payloadLength];
        if (payloadLength > 0)
        {
            await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        }

        return new SidecarFrame(
            (SidecarOperation)header[6],
            (SidecarStatus)header[7],
            payload);
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await OdfKit.Internal.OdfStreamHelper.ReadAsync(
                stream,
                buffer,
                offset,
                buffer.Length - offset,
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            offset += read;
        }
    }

    private static byte[] CreatePayload(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, StrictUtf8, leaveOpen: true))
        {
            write(writer);
        }

        return stream.ToArray();
    }

    private static void WriteRequest(BinaryWriter writer, WebFontSubsetRequest request)
    {
        WriteFace(writer, request.Face);
        WriteString(writer, request.ProfileId);
        WriteString(writer, request.FontFamily);
        WriteSequences(writer, request.Sequences);
        WriteEnums(writer, request.Formats);
        WriteEnums(writer, request.RequiredBrowserTargets);
    }

    private static void ValidateRequestForWire(WebFontSubsetRequest request)
    {
        if (request is null)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        ValidateFaceForWire(request.Face);
        ValidateStringForWire(request.ProfileId, 1024);
        ValidateStringForWire(request.FontFamily, 1024);
        ValidateSequencesForWire(request.Sequences);
        ValidateEnumsForWire(request.Formats, 4);
        ValidateEnumsForWire(request.RequiredBrowserTargets, 3);
    }

    private static void ValidateFaceForWire(WebFontFaceIdentity face)
    {
        if (face is null || face.FaceIndex < 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        ValidateStringForWire(face.FontSourceId, 1024);
        ValidateStringForWire(face.SourceSha256, 128);
    }

    private static void ValidateSequencesForWire(IReadOnlyList<WebFontTextSequence> sequences)
    {
        if (sequences is null || sequences.Count > 4096)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        long scalarCount = 0;
        foreach (WebFontTextSequence sequence in sequences)
        {
            if (sequence is null)
            {
                throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
            }

            scalarCount += sequence.UnicodeScalars.Count;
            if (scalarCount > 65536)
            {
                throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
            }

            ValidateStringForWire(sequence.Text, 65536 * 4);
        }
    }

    private static void ValidateEnumsForWire<T>(IReadOnlyList<T> values, int maximumCount)
        where T : struct, Enum
    {
        if (values is null || values.Count > maximumCount || values.Any(value => !IsDefinedEnum(value)))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }
    }

    private static bool IsDefinedEnum<T>(T value)
        where T : struct, Enum
    {
#if NET6_0_OR_GREATER
        return Enum.IsDefined(value);
#else
        return Enum.IsDefined(typeof(T), value);
#endif
    }

    private static void ValidateStringForWire(string value, int maximumBytes)
    {
        if (value is null || StrictUtf8.GetByteCount(value) > maximumBytes)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }
    }

    private static void WriteFace(BinaryWriter writer, WebFontFaceIdentity face)
    {
        WriteString(writer, face.FontSourceId);
        WriteString(writer, face.SourceSha256);
        writer.Write(face.FaceIndex);
    }

    private static WebFontFaceIdentity ReadFace(BinaryReader reader)
        => new()
        {
            FontSourceId = ReadString(reader, 1024),
            SourceSha256 = ReadString(reader, 128),
            FaceIndex = reader.ReadInt32()
        };

    private static void WriteSequences(BinaryWriter writer, IReadOnlyList<WebFontTextSequence> sequences)
    {
        writer.Write(sequences.Count);
        foreach (WebFontTextSequence sequence in sequences)
        {
            WriteString(writer, sequence.Text);
        }
    }

    private static WebFontTextSequence[] ReadSequences(
        BinaryReader reader,
        int maximumCount,
        int maximumScalarCount)
    {
        int count = ReadCount(reader, maximumCount);
        var sequences = new WebFontTextSequence[count];
        int scalarCount = 0;
        for (int index = 0; index < count; index++)
        {
            WebFontTextSequence sequence = WebFontTextSequence.Create(
                ReadString(reader, maximumScalarCount * 4));
            scalarCount = checked(scalarCount + sequence.UnicodeScalars.Count);
            if (scalarCount > maximumScalarCount)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
            }

            sequences[index] = sequence;
        }

        return sequences;
    }

    private static void WriteEnums<T>(BinaryWriter writer, IReadOnlyList<T> values)
        where T : struct, Enum
    {
        writer.Write(values.Count);
        foreach (T value in values)
        {
            writer.Write(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static T[] ReadEnums<T>(BinaryReader reader, int maximumCount)
        where T : struct, Enum
    {
        int count = ReadCount(reader, maximumCount);
        var values = new T[count];
        for (int index = 0; index < count; index++)
        {
            int raw = reader.ReadInt32();
            if (!Enum.IsDefined(typeof(T), raw))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
            }

            values[index] = (T)Enum.ToObject(typeof(T), raw);
        }

        return values;
    }

    private static void WriteManifest(BinaryWriter writer, WebFontManifest manifest)
    {
        writer.Write(manifest.SchemaVersion);
        WriteString(writer, manifest.ProfileId);
        writer.Write(manifest.Assets.Count);
        foreach (WebFontAsset asset in manifest.Assets)
        {
            WriteString(writer, asset.FileName);
            WriteString(writer, asset.Sha256);
            writer.Write(asset.ByteLength);
            writer.Write((int)asset.Format);
            WriteString(writer, asset.FontFamily);
            writer.Write(asset.UnicodeRanges.Count);
            foreach (string range in asset.UnicodeRanges)
            {
                WriteString(writer, range);
            }
        }

        WriteNullableString(writer, manifest.StylesheetFileName);
        WriteNullableString(writer, manifest.StylesheetSha256);
    }

    private static WebFontManifest ReadManifest(BinaryReader reader)
    {
        int schemaVersion = reader.ReadInt32();
        string profileId = ReadString(reader, 1024);
        int assetCount = ReadCount(reader, 16);
        var assets = new WebFontAsset[assetCount];
        for (int index = 0; index < assetCount; index++)
        {
            string fileName = ReadString(reader, 1024);
            string sha256 = ReadString(reader, 128);
            long byteLength = reader.ReadInt64();
            int rawFormat = reader.ReadInt32();
            if (!Enum.IsDefined(typeof(WebFontFormat), rawFormat))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            string fontFamily = ReadString(reader, 1024);
            int rangeCount = ReadCount(reader, 4096);
            var ranges = new string[rangeCount];
            for (int rangeIndex = 0; rangeIndex < rangeCount; rangeIndex++)
            {
                ranges[rangeIndex] = ReadString(reader, 256);
            }

            assets[index] = new WebFontAsset
            {
                FileName = fileName,
                Sha256 = sha256,
                ByteLength = byteLength,
                Format = (WebFontFormat)rawFormat,
                FontFamily = fontFamily,
                UnicodeRanges = ranges
            };
        }

        return new WebFontManifest
        {
            SchemaVersion = schemaVersion,
            ProfileId = profileId,
            Assets = assets,
            StylesheetFileName = ReadNullableString(reader, 1024),
            StylesheetSha256 = ReadNullableString(reader, 128)
        };
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = StrictUtf8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader, int maximumBytes)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > maximumBytes)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new EndOfStreamException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return StrictUtf8.GetString(bytes);
    }

    private static void WriteNullableString(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
        {
            WriteString(writer, value);
        }
    }

    private static string? ReadNullableString(BinaryReader reader, int maximumBytes)
        => reader.ReadBoolean() ? ReadString(reader, maximumBytes) : null;

    private static int ReadCount(BinaryReader reader, int maximum)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > maximum)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return count;
    }

    private static void EnsurePayloadConsumed(BinaryReader reader)
    {
        if (reader.BaseStream.Position != reader.BaseStream.Length)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    private static ushort ReadUInt16(byte[] buffer, int offset)
        => (ushort)(buffer[offset] | buffer[offset + 1] << 8);

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static uint ReadUInt32(byte[] buffer, int offset)
        => (uint)(buffer[offset]
            | buffer[offset + 1] << 8
            | buffer[offset + 2] << 16
            | buffer[offset + 3] << 24);

    private static void WriteInt32(byte[] buffer, int offset, int value)
        => WriteUInt32(buffer, offset, unchecked((uint)value));

    private static int ReadInt32(byte[] buffer, int offset)
        => unchecked((int)ReadUInt32(buffer, offset));
}
