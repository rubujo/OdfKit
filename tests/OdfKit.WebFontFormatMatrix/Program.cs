using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OdfKit.WebFonts;
using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFontFormatMatrix;

internal static class Program
{
    private const string ExtBText = "A𠆩";
    private const string IvsText = "邉󠄐";
    private const string PuaText = "󰀀󰖇󿸹";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 10)
        {
            return 2;
        }

        string outputRoot = Path.GetFullPath(args[0]);
        string extBPath = Path.GetFullPath(args[1]);
        string plusPath = Path.GetFullPath(args[2]);
        string ipamjPath = Path.GetFullPath(args[3]);
        string cffCollectionPath = Path.GetFullPath(args[4]);
        string cffOpenTypePath = Path.GetFullPath(args[5]);
        string arabicVariablePath = Path.GetFullPath(args[6]);
        string devanagariVariablePath = Path.GetFullPath(args[7]);
        string cff2VariablePath = Path.GetFullPath(args[8]);
        string colorEmojiPath = Path.GetFullPath(args[9]);
        Directory.CreateDirectory(outputRoot);

        string trueTypeCollectionPath = Path.Combine(outputRoot, "cns-managed-real-faces.ttc");
        await File.WriteAllBytesAsync(
            trueTypeCollectionPath,
            CreateTrueTypeCollection(
                await File.ReadAllBytesAsync(extBPath).ConfigureAwait(false),
                await File.ReadAllBytesAsync(plusPath).ConfigureAwait(false))).ConfigureAwait(false);

        var results = new List<MatrixResult>();
        await VerifySuccessAsync(
            results,
            "cns-ext-b-ttf",
            extBPath,
            faceIndex: 0,
            ExtBText,
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "ipamj-ivs",
            ipamjPath,
            faceIndex: 0,
            IvsText,
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "cns-pua",
            plusPath,
            faceIndex: 0,
            PuaText,
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "managed-ttc-face-0",
            trueTypeCollectionPath,
            faceIndex: 0,
            ExtBText,
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "managed-ttc-face-1",
            trueTypeCollectionPath,
            faceIndex: 1,
            PuaText,
            outputRoot).ConfigureAwait(false);

        await VerifyRejectedAsync(
            results,
            "cff-otc",
            cffCollectionPath,
            faceIndex: 4,
            "香港邨裏𠮷",
            outputRoot).ConfigureAwait(false);
        await VerifyRejectedAsync(
            results,
            "cff-otf",
            cffOpenTypePath,
            faceIndex: 0,
            "香港邨裏𠮷",
            outputRoot).ConfigureAwait(false);
        await VerifyRejectedAsync(
            results,
            "arabic-variable",
            arabicVariablePath,
            faceIndex: 0,
            "السَّلَامُ عَلَيْكُمْ",
            outputRoot).ConfigureAwait(false);
        await VerifyRejectedAsync(
            results,
            "devanagari-variable",
            devanagariVariablePath,
            faceIndex: 0,
            "क्षेत्रज्ञ भारत",
            outputRoot).ConfigureAwait(false);
        await VerifyRejectedAsync(
            results,
            "cff2-variable",
            cff2VariablePath,
            faceIndex: 0,
            "繁體字",
            outputRoot).ConfigureAwait(false);
        await VerifyRejectedAsync(
            results,
            "color-bitmap",
            colorEmojiPath,
            faceIndex: 0,
            "😀",
            outputRoot).ConfigureAwait(false);

        var fuzzResults = new List<FuzzResult>(RunDeterministicMutationFuzz(
            Path.Combine(outputRoot, "cns-ext-b-ttf", "first")))
        {
            RunSourceMutationFuzz(extBPath)
        };

        string evidencePath = Path.Combine(outputRoot, "format-matrix.json");
        await File.WriteAllTextAsync(
            evidencePath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                results,
                deterministicMutationFuzz = fuzzResults
            }, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        Console.WriteLine($"PASS: {results.Count} real managed format cases. Evidence: {evidencePath}");
        return 0;
    }

    private static IReadOnlyList<FuzzResult> RunDeterministicMutationFuzz(string assetRoot)
    {
        var results = new List<FuzzResult>();
        foreach ((string extension, WebFontFormat format) in new[]
                 {
                     (".ttf", WebFontFormat.TrueType),
                     (".woff", WebFontFormat.Woff),
                     (".woff2", WebFontFormat.Woff2)
                 })
        {
            string path = Directory.GetFiles(assetRoot, $"*{extension}", SearchOption.AllDirectories).Single();
            byte[] valid = File.ReadAllBytes(path);
            int accepted = 0;
            int rejected = 0;
            uint state = 0xA341316Cu ^ (uint)format;
            for (int iteration = 0; iteration < 128; iteration++)
            {
                byte[] mutated;
                if ((iteration & 3) == 0)
                {
                    state = NextState(state);
                    int length = 1 + (int)(state % (uint)(valid.Length - 1));
                    mutated = valid.AsSpan(0, length).ToArray();
                }
                else
                {
                    mutated = (byte[])valid.Clone();
                    int mutationCount = 1 + (iteration % 4);
                    for (int mutation = 0; mutation < mutationCount; mutation++)
                    {
                        state = NextState(state);
                        int offset = (int)(state % (uint)mutated.Length);
                        state = NextState(state);
                        mutated[offset] ^= (byte)(1 << (int)(state & 7));
                    }
                }

                try
                {
                    using var stream = new MemoryStream(mutated, writable: false);
                    ManagedOpenTypeWebFontVerifier.Verify(stream, format);
                    accepted++;
                }
                catch (InvalidDataException)
                {
                    rejected++;
                }
                catch (NotSupportedException)
                {
                    rejected++;
                }
            }

            if (rejected == 0)
            {
                throw new InvalidDataException($"The {format} mutation fuzz did not exercise a rejection path.");
            }

            results.Add(new FuzzResult(format.ToString(), 128, accepted, rejected));
        }

        return results;
    }

    private static FuzzResult RunSourceMutationFuzz(string sourcePath)
    {
        byte[] valid = File.ReadAllBytes(sourcePath);
        int accepted = 0;
        int rejected = 0;
        uint state = 0xC8013EA4u;
        for (int iteration = 0; iteration < 64; iteration++)
        {
            byte[] mutated = (byte[])valid.Clone();
            int mutationCount = 1 + (iteration % 4);
            for (int mutation = 0; mutation < mutationCount; mutation++)
            {
                state = NextState(state);
                int mutationWindow = Math.Min(mutated.Length, 16 * 1024);
                int offset = (int)(state % (uint)mutationWindow);
                state = NextState(state);
                mutated[offset] ^= (byte)(1 << (int)(state & 7));
            }

            try
            {
                using var stream = new MemoryStream(mutated, writable: false);
                ManagedOpenTypeWebFontVerifier.Verify(stream, WebFontFormat.TrueType);
                accepted++;
            }
            catch (InvalidDataException)
            {
                rejected++;
            }
            catch (NotSupportedException)
            {
                rejected++;
            }
        }

        if (rejected == 0)
        {
            throw new InvalidDataException("The source-font mutation fuzz did not exercise a rejection path.");
        }

        return new FuzzResult("TrueTypeSource", 64, accepted, rejected);
    }

    private static uint NextState(uint value)
    {
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        return value;
    }

    private static async Task VerifySuccessAsync(
        ICollection<MatrixResult> results,
        string id,
        string sourcePath,
        int faceIndex,
        string text,
        string outputRoot)
    {
        string sourceSha256 = await ComputeSha256Async(sourcePath).ConfigureAwait(false);
        WebFontTextSequence sequence = WebFontTextSequence.Create(text);
        WebFontManifest first = await GenerateAsync(
            id,
            sourcePath,
            sourceSha256,
            faceIndex,
            sequence,
            Path.Combine(outputRoot, id, "first")).ConfigureAwait(false);
        WebFontManifest second = await GenerateAsync(
            id,
            sourcePath,
            sourceSha256,
            faceIndex,
            sequence,
            Path.Combine(outputRoot, id, "second")).ConfigureAwait(false);
        string[] firstHashes = first.Assets.OrderBy(asset => asset.Format).Select(asset => asset.Sha256).ToArray();
        string[] secondHashes = second.Assets.OrderBy(asset => asset.Format).Select(asset => asset.Sha256).ToArray();
        if (!firstHashes.SequenceEqual(secondHashes, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{id} was not byte deterministic.");
        }

        foreach (WebFontAsset asset in first.Assets)
        {
            string assetPath = Path.Combine(outputRoot, id, "first", asset.Sha256, asset.FileName);
            await using FileStream stream = File.OpenRead(assetPath);
            ManagedOpenTypeWebFontVerifier.VerifyContainsSequences(stream, asset.Format, [sequence]);
        }

        results.Add(new MatrixResult(
            id,
            "generated",
            Path.GetFileName(sourcePath),
            sourceSha256,
            faceIndex,
            firstHashes));
    }

    private static async Task VerifyRejectedAsync(
        ICollection<MatrixResult> results,
        string id,
        string sourcePath,
        int faceIndex,
        string text,
        string outputRoot)
    {
        string sourceSha256 = await ComputeSha256Async(sourcePath).ConfigureAwait(false);
        try
        {
            await GenerateAsync(
                id,
                sourcePath,
                sourceSha256,
                faceIndex,
                WebFontTextSequence.Create(text),
                Path.Combine(outputRoot, id)).ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            results.Add(new MatrixResult(
                id,
                "rejected-not-supported",
                Path.GetFileName(sourcePath),
                sourceSha256,
                faceIndex,
                []));
            return;
        }

        throw new InvalidDataException($"{id} was silently accepted instead of being rejected.");
    }

    private static Task<WebFontManifest> GenerateAsync(
        string id,
        string sourcePath,
        string sourceSha256,
        int faceIndex,
        WebFontTextSequence sequence,
        string destination)
    {
        var options = new ManagedOpenTypeWebFontEngineOptions
        {
            MaxSourceBytes = 256L * 1024 * 1024,
            MaxOutputBytes = 64L * 1024 * 1024,
            MaxUnicodeScalars = 1024
        };
        options.FontSources[id] = sourcePath;
        var engine = new ManagedOpenTypeWebFontSubsetEngine(options);
        return engine.GenerateAsync(
            new WebFontSubsetRequest
            {
                Face = new WebFontFaceIdentity
                {
                    FontSourceId = id,
                    FaceIndex = faceIndex,
                    SourceSha256 = sourceSha256
                },
                ProfileId = $"{id}-v1",
                FontFamily = $"OdfKit {id}",
                Sequences = [sequence],
                Formats = [WebFontFormat.TrueType, WebFontFormat.Woff, WebFontFormat.Woff2]
            },
            destination);
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static byte[] CreateTrueTypeCollection(params byte[][] fonts)
    {
        FaceSource[] faces = fonts.Select(ParseFace).ToArray();
        int headerLength = checked(12 + (faces.Length * 4));
        int cursor = Align4(headerLength);
        foreach (FaceSource face in faces)
        {
            face.FaceOffset = cursor;
            cursor = Align4(checked(cursor + 12 + (face.Tables.Length * 16)));
        }

        foreach (FaceSource face in faces)
        {
            foreach (TableSource table in face.Tables)
            {
                table.CollectionOffset = cursor;
                cursor = Align4(checked(cursor + table.Data.Length));
            }
        }

        var output = new byte[cursor];
        "ttcf"u8.CopyTo(output);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(4, 4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(8, 4), checked((uint)faces.Length));
        for (int index = 0; index < faces.Length; index++)
        {
            FaceSource face = faces[index];
            BinaryPrimitives.WriteUInt32BigEndian(
                output.AsSpan(12 + (index * 4), 4),
                checked((uint)face.FaceOffset));
            face.Header.CopyTo(output, face.FaceOffset);
            for (int tableIndex = 0; tableIndex < face.Tables.Length; tableIndex++)
            {
                TableSource table = face.Tables[tableIndex];
                int recordOffset = face.FaceOffset + 12 + (tableIndex * 16);
                table.Tag.CopyTo(output, recordOffset);
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(recordOffset + 4, 4), table.Checksum);
                BinaryPrimitives.WriteUInt32BigEndian(
                    output.AsSpan(recordOffset + 8, 4),
                    checked((uint)table.CollectionOffset));
                BinaryPrimitives.WriteUInt32BigEndian(
                    output.AsSpan(recordOffset + 12, 4),
                    checked((uint)table.Data.Length));
                table.Data.CopyTo(output, table.CollectionOffset);
            }
        }

        return output;
    }

    private static FaceSource ParseFace(byte[] font)
    {
        if (font.Length < 12 || BinaryPrimitives.ReadUInt32BigEndian(font) is not (0x00010000 or 0x74727565))
        {
            throw new InvalidDataException("The TTC fixture source is not a TrueType sfnt.");
        }

        ushort tableCount = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4, 2));
        int directoryEnd = checked(12 + (tableCount * 16));
        if (tableCount == 0 || directoryEnd > font.Length)
        {
            throw new InvalidDataException("The TTC fixture source directory is invalid.");
        }

        var tables = new TableSource[tableCount];
        for (int index = 0; index < tableCount; index++)
        {
            int recordOffset = 12 + (index * 16);
            uint sourceOffset = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(recordOffset + 8, 4));
            uint sourceLength = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(recordOffset + 12, 4));
            if (sourceOffset > int.MaxValue
                || sourceLength > int.MaxValue
                || (ulong)sourceOffset + sourceLength > (ulong)font.Length)
            {
                throw new InvalidDataException("The TTC fixture source table is outside the file.");
            }

            tables[index] = new TableSource(
                font.AsSpan(recordOffset, 4).ToArray(),
                BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(recordOffset + 4, 4)),
                font.AsSpan((int)sourceOffset, (int)sourceLength).ToArray());
        }

        return new FaceSource(font.AsSpan(0, 12).ToArray(), tables);
    }

    private static int Align4(int value)
        => checked((value + 3) & ~3);

    private sealed class FaceSource(byte[] header, TableSource[] tables)
    {
        internal byte[] Header { get; } = header;

        internal TableSource[] Tables { get; } = tables;

        internal int FaceOffset { get; set; }
    }

    private sealed class TableSource(byte[] tag, uint checksum, byte[] data)
    {
        internal byte[] Tag { get; } = tag;

        internal uint Checksum { get; } = checksum;

        internal byte[] Data { get; } = data;

        internal int CollectionOffset { get; set; }
    }

    private sealed record MatrixResult(
        string Id,
        string Outcome,
        string SourceFile,
        string SourceSha256,
        int FaceIndex,
        IReadOnlyList<string> OutputSha256);

    private sealed record FuzzResult(
        string Format,
        int Cases,
        int Accepted,
        int Rejected);
}
