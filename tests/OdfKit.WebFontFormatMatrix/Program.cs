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
        try
        {
            return await RunAsync(args).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 14)
        {
            return 2;
        }

        string outputRoot = Path.GetFullPath(args[0]);
        string extBPath = Path.GetFullPath(args[1]);
        string plusPath = Path.GetFullPath(args[2]);
        string kaiExtBPath = Path.GetFullPath(args[3]);
        string kaiPlusPath = Path.GetFullPath(args[4]);
        string ipamjPath = Path.GetFullPath(args[5]);
        string cffCollectionPath = Path.GetFullPath(args[6]);
        string cffOpenTypePath = Path.GetFullPath(args[7]);
        string arabicStaticPath = Path.GetFullPath(args[8]);
        string devanagariStaticPath = Path.GetFullPath(args[9]);
        string arabicVariablePath = Path.GetFullPath(args[10]);
        string devanagariVariablePath = Path.GetFullPath(args[11]);
        string cff2VariablePath = Path.GetFullPath(args[12]);
        string colorEmojiPath = Path.GetFullPath(args[13]);
        Directory.CreateDirectory(outputRoot);

        string trueTypeCollectionPath = Path.Combine(outputRoot, "cns-managed-real-faces.ttc");
        await File.WriteAllBytesAsync(
            trueTypeCollectionPath,
            CreateTrueTypeCollection(
                await File.ReadAllBytesAsync(extBPath).ConfigureAwait(false),
                await File.ReadAllBytesAsync(plusPath).ConfigureAwait(false))).ConfigureAwait(false);
        string eudcTtePath = Path.Combine(outputRoot, "EUDC.TTE");
        File.Copy(extBPath, eudcTtePath, overwrite: true);

        var results = new List<MatrixResult>();
        WebFontManifest extBManifest = await VerifySuccessAsync(
            results,
            "cns-ext-b-ttf",
            extBPath,
            faceIndex: 0,
            ExtBText,
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "windows-eudc-tte",
            eudcTtePath,
            faceIndex: 0,
            ExtBText,
            outputRoot).ConfigureAwait(false);
        await VerifySourceCacheAsync(eudcTtePath, outputRoot).ConfigureAwait(false);
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
            "cns-kai-ext-b",
            kaiExtBPath,
            faceIndex: 0,
            ExtBText,
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "cns-kai-pua",
            kaiPlusPath,
            faceIndex: 0,
            PuaText,
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "arabic-static-layout",
            arabicStaticPath,
            faceIndex: 0,
            "السَّلَامُ عَلَيْكُمْ لا إله إلا الله",
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "devanagari-static-layout",
            devanagariStaticPath,
            faceIndex: 0,
            "क्षेत्रज्ञ भारत शृंखला हिन्दी",
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
        await VerifySuccessAsync(
            results,
            "arabic-variable",
            arabicVariablePath,
            faceIndex: 0,
            "السَّلَامُ عَلَيْكُمْ",
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
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
            Path.Combine(outputRoot, "cns-ext-b-ttf", "first"),
            extBManifest))
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
                verifiedSourceCache = true,
                deterministicMutationFuzz = fuzzResults
            }, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        Console.WriteLine($"PASS: {results.Count} real managed format cases. Evidence: {evidencePath}");
        return 0;
    }

    private static IReadOnlyList<FuzzResult> RunDeterministicMutationFuzz(
        string assetRoot,
        WebFontManifest manifest)
    {
        var results = new List<FuzzResult>();
        foreach ((string extension, WebFontFormat format) in new[]
                 {
                     (".ttf", WebFontFormat.TrueType),
                     (".woff", WebFontFormat.Woff),
                     (".woff2", WebFontFormat.Woff2)
                 })
        {
            WebFontAsset asset = manifest.Assets.Single(item => item.Format == format);
            string path = Path.Combine(assetRoot, asset.Sha256, asset.FileName);
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

    private static async Task<WebFontManifest> VerifySuccessAsync(
        ICollection<MatrixResult> results,
        string id,
        string sourcePath,
        int faceIndex,
        string text,
        string outputRoot)
    {
        string sourceSha256 = await ComputeSha256Async(sourcePath).ConfigureAwait(false);
        WebFontTextSequence sequence = WebFontTextSequence.Create(text);
        string firstOutput = Path.Combine(outputRoot, id, "first");
        string secondOutput = Path.Combine(outputRoot, id, "second");
        RecreateDirectory(firstOutput);
        RecreateDirectory(secondOutput);
        WebFontManifest first = await GenerateAsync(
            id,
            sourcePath,
            sourceSha256,
            faceIndex,
            sequence,
            firstOutput).ConfigureAwait(false);
        WebFontManifest second = await GenerateAsync(
            id,
            sourcePath,
            sourceSha256,
            faceIndex,
            sequence,
            secondOutput).ConfigureAwait(false);
        string[] firstHashes = first.Assets.OrderBy(asset => asset.Format).Select(asset => asset.Sha256).ToArray();
        string[] secondHashes = second.Assets.OrderBy(asset => asset.Format).Select(asset => asset.Sha256).ToArray();
        if (!firstHashes.SequenceEqual(secondHashes, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{id} was not byte deterministic.");
        }

        byte[] sourceBytes = await File.ReadAllBytesAsync(sourcePath).ConfigureAwait(false);
        foreach (WebFontAsset asset in first.Assets)
        {
            string assetPath = Path.Combine(firstOutput, asset.Sha256, asset.FileName);
            await using FileStream stream = File.OpenRead(assetPath);
            ManagedOpenTypeWebFontVerifier.VerifyContainsSequences(stream, asset.Format, [sequence]);
            stream.Position = 0;
            ManagedOpenTypeWebFontVerifier.VerifyRetainsGlyphIds(
                sourceBytes,
                faceIndex,
                stream,
                asset.Format,
                sequence.UnicodeScalars.Where(scalar => scalar is not (>= 0xFE00 and <= 0xFE0F)
                    and not (>= 0xE0100 and <= 0xE01EF)));
            stream.Position = 0;
            ManagedOpenTypeWebFontVerifier.VerifyRetainsLayoutTables(
                sourceBytes,
                faceIndex,
                stream,
                asset.Format);
            if (asset.Format == WebFontFormat.Woff2)
            {
                VerifyRejectsInvalidWoff2Padding(await File.ReadAllBytesAsync(assetPath).ConfigureAwait(false));
            }
        }

        results.Add(new MatrixResult(
            id,
            "generated",
            Path.GetFileName(sourcePath),
            sourceSha256,
            faceIndex,
            firstHashes));
        return first;
    }

    private static void VerifyRejectsInvalidWoff2Padding(byte[] valid)
    {
        byte[] excessivePadding = new byte[checked(valid.Length + 4)];
        valid.CopyTo(excessivePadding, 0);
        BinaryPrimitives.WriteUInt32BigEndian(excessivePadding.AsSpan(8, 4), (uint)excessivePadding.Length);
        RequireWoff2Rejection(excessivePadding, "excessive padding");

        int position = 48;
        ushort tableCount = BinaryPrimitives.ReadUInt16BigEndian(valid.AsSpan(12, 2));
        for (int index = 0; index < tableCount; index++)
        {
            byte flags = valid[position++];
            if ((flags & 0x3F) == 63)
            {
                position += 4;
            }

            ReadUIntBase128(valid, ref position);
            int tagIndex = flags & 0x3F;
            int transformVersion = flags >> 6;
            bool transformed = tagIndex is 10 or 11
                ? transformVersion == 0
                : transformVersion != 0;
            if (transformed)
            {
                ReadUIntBase128(valid, ref position);
            }
        }

        int compressedLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(valid.AsSpan(20, 4)));
        int compressedEnd = checked(position + compressedLength);
        if (compressedEnd < valid.Length)
        {
            byte[] nonzeroPadding = (byte[])valid.Clone();
            nonzeroPadding[compressedEnd] = 1;
            RequireWoff2Rejection(nonzeroPadding, "nonzero padding");
        }
    }

    private static uint ReadUIntBase128(ReadOnlySpan<byte> data, ref int position)
    {
        uint value = 0;
        byte current;
        do
        {
            current = data[position++];
            value = checked((value << 7) | (uint)(current & 0x7F));
        }
        while ((current & 0x80) != 0);

        return value;
    }

    private static void RequireWoff2Rejection(byte[] bytes, string scenario)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            ManagedOpenTypeWebFontVerifier.Verify(stream, WebFontFormat.Woff2);
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidDataException($"Managed verifier accepted WOFF2 {scenario}.");
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static async Task VerifySourceCacheAsync(string sourcePath, string outputRoot)
    {
        string sourceSha256 = await ComputeSha256Async(sourcePath).ConfigureAwait(false);
        var options = new ManagedOpenTypeWebFontEngineOptions
        {
            MaxCachedSourceBytes = 128L * 1024 * 1024,
            MaxCachedSourceEntries = 1,
            MaxUnicodeScalars = 16
        };
        options.FontSources["eudc-cache"] = sourcePath;
        var engine = new ManagedOpenTypeWebFontSubsetEngine(options);
        var request = new WebFontSubsetRequest
        {
            Face = new WebFontFaceIdentity
            {
                FontSourceId = "eudc-cache",
                SourceSha256 = sourceSha256
            },
            ProfileId = "eudc-cache-v1",
            FontFamily = "OdfKit EUDC Cache",
            Sequences = [WebFontTextSequence.Create(ExtBText)],
            Formats = [WebFontFormat.TrueType]
        };
        await engine.GenerateAsync(
            request,
            Path.Combine(outputRoot, "eudc-cache", "first")).ConfigureAwait(false);

        long length = new FileInfo(sourcePath).Length;
        await File.WriteAllBytesAsync(sourcePath, new byte[checked((int)length)]).ConfigureAwait(false);
        await engine.GenerateAsync(
            request,
            Path.Combine(outputRoot, "eudc-cache", "second")).ConfigureAwait(false);
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
