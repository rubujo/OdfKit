using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OdfKit.WebFonts;
using OdfKit.WebFonts.Build;
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
        if (args.Length == 4 && args[0] == "woff2-corpus")
        {
            await VerifyWoff2CorpusAsync(args[1], args[2], args[3]).ConfigureAwait(false);
            return 0;
        }
        if (args.Length == 4 && args[0] == "woff2-production")
        {
            await VerifyProductionWoff2Async(args[1], args[2], args[3]).ConfigureAwait(false);
            return 0;
        }
        if (args.Length == 4 && args[0] == "woff2-collection-corpus")
        {
            await VerifyWoff2CollectionCorpusAsync(args[1], args[2], args[3]).ConfigureAwait(false);
            return 0;
        }

        if (args.Length != 18)
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
        string nameCffOpenTypePath = Path.GetFullPath(args[8]);
        string arabicStaticPath = Path.GetFullPath(args[9]);
        string devanagariStaticPath = Path.GetFullPath(args[10]);
        string arabicVariablePath = Path.GetFullPath(args[11]);
        string devanagariVariablePath = Path.GetFullPath(args[12]);
        string cff2VariablePath = Path.GetFullPath(args[13]);
        string colorEmojiPath = Path.GetFullPath(args[14]);
        string colorEmojiColrV1Path = Path.GetFullPath(args[15]);
        string colorSbixPath = Path.GetFullPath(args[16]);
        string colorSvgPath = Path.GetFullPath(args[17]);
        Directory.CreateDirectory(outputRoot);

        string trueTypeCollectionPath = Path.Combine(outputRoot, "cns-managed-real-faces.ttc");
        await File.WriteAllBytesAsync(
            trueTypeCollectionPath,
            CreateOpenTypeCollection(
                await File.ReadAllBytesAsync(extBPath).ConfigureAwait(false),
                await File.ReadAllBytesAsync(plusPath).ConfigureAwait(false))).ConfigureAwait(false);
        string woff2CollectionPath = Path.Combine(outputRoot, "cns-managed-real-faces.woff2");
        await File.WriteAllBytesAsync(
            woff2CollectionPath,
            CreateWoff2Collection(
                await File.ReadAllBytesAsync(extBPath).ConfigureAwait(false),
                await File.ReadAllBytesAsync(plusPath).ConfigureAwait(false))).ConfigureAwait(false);
        string cff2CollectionPath = Path.Combine(outputRoot, "source-han-cff2-variable.otc");
        await File.WriteAllBytesAsync(
            cff2CollectionPath,
            CreateOpenTypeCollection(
                await File.ReadAllBytesAsync(cff2VariablePath).ConfigureAwait(false))).ConfigureAwait(false);
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
        WebFontAsset woffInput = extBManifest.Assets.Single(asset => asset.Format == WebFontFormat.Woff);
        await VerifySuccessAsync(
            results,
            "woff-input",
            Path.Combine(outputRoot, "cns-ext-b-ttf", "first", woffInput.Sha256, woffInput.FileName),
            faceIndex: 0,
            ExtBText,
            outputRoot).ConfigureAwait(false);
        WebFontAsset woff2Input = extBManifest.Assets.Single(asset => asset.Format == WebFontFormat.Woff2);
        await VerifySuccessAsync(
            results,
            "woff2-input-null-transform",
            Path.Combine(outputRoot, "cns-ext-b-ttf", "first", woff2Input.Sha256, woff2Input.FileName),
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
        await VerifySuccessAsync(
            results,
            "managed-woff2-collection-face-0",
            woff2CollectionPath,
            faceIndex: 0,
            ExtBText,
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "managed-woff2-collection-face-1",
            woff2CollectionPath,
            faceIndex: 1,
            PuaText,
            outputRoot).ConfigureAwait(false);

        await VerifySuccessAsync(
            results,
            "cff-otc",
            cffCollectionPath,
            faceIndex: 4,
            "香港邨裏𠮷",
            outputRoot,
            [WebFontFormat.OpenType, WebFontFormat.Woff, WebFontFormat.Woff2]).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "cff-otc-face-0",
            cffCollectionPath,
            faceIndex: 0,
            "香港邨裏𠮷",
            outputRoot,
            [WebFontFormat.OpenType, WebFontFormat.Woff, WebFontFormat.Woff2]).ConfigureAwait(false);
        await VerifyRejectedAsync(
            results,
            "cff-otf-truetype-output",
            cffOpenTypePath,
            faceIndex: 0,
            "香港邨裏𠮷",
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "cff-otf",
            cffOpenTypePath,
            faceIndex: 0,
            "香港邨裏𠮷 全字庫難字顯示 繁體中文測試",
            outputRoot,
            [WebFontFormat.OpenType, WebFontFormat.Woff, WebFontFormat.Woff2]).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "cff-name-otf",
            nameCffOpenTypePath,
            faceIndex: 0,
            "OdfKit café fi ffi 0123456789",
            outputRoot,
            [WebFontFormat.OpenType, WebFontFormat.Woff, WebFontFormat.Woff2]).ConfigureAwait(false);
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
        await VerifySuccessAsync(
            results,
            "cff2-variable",
            cff2VariablePath,
            faceIndex: 0,
            "繁體字 香港邨裏",
            outputRoot,
            [WebFontFormat.OpenType, WebFontFormat.Woff, WebFontFormat.Woff2]).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "cff2-otc-variable",
            cff2CollectionPath,
            faceIndex: 0,
            "繁體字 香港邨裏",
            outputRoot,
            [WebFontFormat.OpenType, WebFontFormat.Woff, WebFontFormat.Woff2]).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "color-bitmap",
            colorEmojiPath,
            faceIndex: 0,
            "😀",
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "color-colrv1",
            colorEmojiColrV1Path,
            faceIndex: 0,
            "😀",
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "color-sbix",
            colorSbixPath,
            faceIndex: 0,
            "simple_linear",
            outputRoot).ConfigureAwait(false);
        await VerifySuccessAsync(
            results,
            "color-svg",
            colorSvgPath,
            faceIndex: 0,
            "simple_linear",
            outputRoot).ConfigureAwait(false);

        var robustnessResults = new List<MutationRobustnessResult>(RunDeterministicMutationRobustnessChecks(
            Path.Combine(outputRoot, "cns-ext-b-ttf", "first"),
            extBManifest))
        {
            RunSourceMutationRobustnessChecks(extBPath, WebFontFormat.TrueType, "TrueTypeSource"),
            RunSourceMutationRobustnessChecks(cffOpenTypePath, WebFontFormat.OpenType, "CffSource"),
            RunSourceMutationRobustnessChecks(nameCffOpenTypePath, WebFontFormat.OpenType, "CffNameSource"),
            RunCffTableMutationRobustnessChecks(cffOpenTypePath),
            RunSourceMutationRobustnessChecks(cff2VariablePath, WebFontFormat.OpenType, "Cff2Source"),
            RunCff2TableMutationRobustnessChecks(cff2VariablePath)
        };

        LargeCnsDeliveryEvidence largeCnsDelivery = await VerifyLargeCnsDeliveryAsync(
            extBPath,
            outputRoot).ConfigureAwait(false);

        string evidencePath = Path.Combine(outputRoot, "format-matrix.json");
        await File.WriteAllTextAsync(
            evidencePath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                results,
                verifiedSourceCache = true,
                deterministicMutationRobustness = robustnessResults,
                largeCnsDelivery
            }, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        Console.WriteLine($"PASS: {results.Count} real managed format cases. Evidence: {evidencePath}");
        return 0;
    }

    private static async Task<LargeCnsDeliveryEvidence> VerifyLargeCnsDeliveryAsync(
        string sourcePath,
        string outputRoot)
    {
        const int targetScalarCount = 2048;
        const int unicodeRangeSliceSize = 256;
        byte[] sourceBytes = await File.ReadAllBytesAsync(sourcePath).ConfigureAwait(false);
        SfntFont source = SfntFont.Parse(sourceBytes, 0, 256, validateChecksums: true);
        int[] scalars = source.UnicodeScalars
            .Where(scalar => scalar is >= 0x10000 and <= 0x10FFFF)
            .OrderBy(scalar => scalar)
            .Take(targetScalarCount)
            .ToArray();
        if (scalars.Length != targetScalarCount)
        {
            throw new InvalidDataException("The official CNS font does not contain the required large corpus.");
        }

        var corpus = new StringBuilder(targetScalarCount * 2);
        foreach (int scalar in scalars)
        {
            corpus.Append(char.ConvertFromUtf32(scalar));
        }

        string benchmarkRoot = Path.Combine(outputRoot, "large-cns-delivery");
        RecreateDirectory(benchmarkRoot);
        string corpusPath = Path.Combine(benchmarkRoot, "cns-ext-b-2048.txt");
        await File.WriteAllTextAsync(
            corpusPath,
            corpus.ToString(),
            new UTF8Encoding(false)).ConfigureAwait(false);

        string firstRoot = Path.Combine(benchmarkRoot, "first");
        string secondRoot = Path.Combine(benchmarkRoot, "second");
        var builder = new WebFontAssetBuilder();
        WebFontBuildOptions CreateOptions(string destination) => new()
        {
            FontPath = sourcePath,
            FontSourceId = "cns-official-ext-b-large",
            TextPath = corpusPath,
            OutputDirectory = destination,
            ProfileId = "cns11643-large-delivery-v1",
            FontFamily = "OdfKit CNS Large Delivery",
            Formats = [WebFontFormat.Woff2],
            UnicodeRangeSliceSize = unicodeRangeSliceSize,
            MaxSliceCount = 512,
            MaxUniqueUnicodeScalars = targetScalarCount,
            MaxSourceBytes = 128L * 1024 * 1024,
            MaxOutputBytes = 64L * 1024 * 1024
        };

        using Process process = Process.GetCurrentProcess();
        long initialAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        WebFontManifest first = await builder.BuildAsync(CreateOptions(firstRoot)).ConfigureAwait(false);
        WebFontManifest second = await builder.BuildAsync(CreateOptions(secondRoot)).ConfigureAwait(false);
        stopwatch.Stop();
        process.Refresh();

        string[] firstHashes = first.Assets.Select(asset => asset.Sha256).OrderBy(value => value).ToArray();
        string[] secondHashes = second.Assets.Select(asset => asset.Sha256).OrderBy(value => value).ToArray();
        string stylesheetFileName = first.StylesheetFileName
            ?? throw new InvalidDataException("The large CNS delivery build emitted no stylesheet name.");
        if (first.Assets.Count < 2
            || first.Assets.Any(asset => asset.Format != WebFontFormat.Woff2)
            || !firstHashes.SequenceEqual(secondHashes, StringComparer.Ordinal)
            || string.IsNullOrWhiteSpace(stylesheetFileName)
            || !string.Equals(first.StylesheetSha256, second.StylesheetSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The large CNS delivery build was incomplete or non-deterministic.");
        }

        long fontPayloadBytes = first.Assets.Sum(asset => asset.ByteLength);
        long cssBytes = new FileInfo(Path.Combine(firstRoot, stylesheetFileName)).Length;
        long manifestBytes = new FileInfo(Path.Combine(firstRoot, "webfonts.json")).Length;
        long coldPayloadBytes = checked(fontPayloadBytes + cssBytes + manifestBytes);
        long allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - initialAllocatedBytes;
        if (cssBytes > 256L * 1024
            || manifestBytes > 1024L * 1024
            || coldPayloadBytes > 256L * 1024 * 1024
            || allocatedBytes > 8L * 1024 * 1024 * 1024
            || stopwatch.Elapsed > TimeSpan.FromMinutes(10))
        {
            throw new InvalidDataException("The large CNS delivery build exceeded its reproducible resource budget.");
        }

        return new LargeCnsDeliveryEvidence(
            Path.GetFileName(sourcePath),
            Convert.ToHexStringLower(SHA256.HashData(sourceBytes)),
            sourceBytes.LongLength,
            targetScalarCount,
            unicodeRangeSliceSize,
            first.Assets.Count,
            fontPayloadBytes,
            cssBytes,
            manifestBytes,
            coldPayloadBytes,
            stopwatch.ElapsedMilliseconds,
            process.WorkingSet64,
            allocatedBytes,
            true);
    }

    private static async Task VerifyWoff2CorpusAsync(
        string woff2Path,
        string referencePath,
        string evidencePath)
    {
        woff2Path = Path.GetFullPath(woff2Path);
        referencePath = Path.GetFullPath(referencePath);
        evidencePath = Path.GetFullPath(evidencePath);
        byte[] woff2 = await File.ReadAllBytesAsync(woff2Path).ConfigureAwait(false);
        byte[] reference = await File.ReadAllBytesAsync(referencePath).ConfigureAwait(false);
        byte[] decoded = ManagedOpenTypeWebFontVerifier.DecodeWoff2(
            woff2,
            maximumExpandedBytes: 32 * 1024 * 1024);
        string decodedPath = Path.ChangeExtension(evidencePath, ".decoded.ttf");
        string? decodedDirectory = Path.GetDirectoryName(decodedPath);
        if (!string.IsNullOrEmpty(decodedDirectory))
        {
            Directory.CreateDirectory(decodedDirectory);
        }
        await File.WriteAllBytesAsync(decodedPath, decoded).ConfigureAwait(false);
        using (var stream = new MemoryStream(decoded, writable: false))
        {
            ManagedOpenTypeWebFontVerifier.Verify(stream, WebFontFormat.TrueType);
        }

        SfntFont decodedFont = SfntFont.Parse(decoded, 0, 256, validateChecksums: true);
        SfntFont referenceFont = SfntFont.Parse(reference, 0, 256, validateChecksums: true);
        if (decodedFont.GlyphCount != referenceFont.GlyphCount)
        {
            throw new InvalidDataException("WOFF2 corpus glyph count differs from its W3C reference.");
        }

        string[] comparedTables = ReadTableTags(reference)
            .Where(tag => tag is not ("glyf" or "loca" or "head" or "hmtx"))
            .ToArray();
        foreach (string tag in comparedTables)
        {
            bool hasDecoded = decodedFont.TryGetTable(tag, out ReadOnlyMemory<byte> decodedTable);
            bool hasReference = referenceFont.TryGetTable(tag, out ReadOnlyMemory<byte> referenceTable);
            if (!hasDecoded || !hasReference || !decodedTable.Span.SequenceEqual(referenceTable.Span))
            {
                string decodedHash = Convert.ToHexStringLower(SHA256.HashData(decodedTable.Span));
                string referenceHash = Convert.ToHexStringLower(SHA256.HashData(referenceTable.Span));
                throw new InvalidDataException(
                    $"WOFF2 corpus table differs from its W3C reference: {tag} "
                    + $"({decodedHash} != {referenceHash}).");
            }
        }

        string? directory = Path.GetDirectoryName(evidencePath);
        directory = string.IsNullOrEmpty(directory) ? Directory.GetCurrentDirectory() : directory;
        Directory.CreateDirectory(directory);

        var evidence = new
        {
            schemaVersion = "1.0",
            status = "passed",
            source = "w3c/woff2-compiled-tests",
            woff2Sha256 = Convert.ToHexStringLower(SHA256.HashData(woff2)),
            referenceSha256 = Convert.ToHexStringLower(SHA256.HashData(reference)),
            decodedSha256 = Convert.ToHexStringLower(SHA256.HashData(decoded)),
            glyphCount = decodedFont.GlyphCount,
            comparedTables
        };
        await File.WriteAllTextAsync(
            evidencePath,
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
    }

    private static async Task VerifyProductionWoff2Async(
        string woff2Path,
        string text,
        string evidencePath)
    {
        woff2Path = Path.GetFullPath(woff2Path);
        evidencePath = Path.GetFullPath(evidencePath);
        byte[] woff2 = await File.ReadAllBytesAsync(woff2Path).ConfigureAwait(false);
        using (var stream = new MemoryStream(woff2, writable: false))
        {
            ManagedOpenTypeWebFontVerifier.VerifyContainsSequences(
                stream,
                WebFontFormat.Woff2,
                [WebFontTextSequence.Create(text)]);
        }

        byte[] decoded = ManagedOpenTypeWebFontVerifier.DecodeWoff2(
            woff2,
            maximumExpandedBytes: 32 * 1024 * 1024);
        SfntFont font = SfntFont.Parse(decoded, 0, 256, validateChecksums: true);
        string[] transforms = ReadWoff2Transforms(woff2);
        (int mutationCount, int acceptedMutations) = RunWoff2MutationRobustnessChecks(woff2);
        string? directory = Path.GetDirectoryName(evidencePath);
        directory = string.IsNullOrEmpty(directory) ? Directory.GetCurrentDirectory() : directory;
        Directory.CreateDirectory(directory);

        string sourceSha256 = Convert.ToHexStringLower(SHA256.HashData(woff2));
        string scenario = Path.GetFileNameWithoutExtension(woff2Path);
        string generatedRoot = Path.Combine(directory, $"{scenario}-generated");
        RecreateDirectory(generatedRoot);
        WebFontManifest manifest = await GenerateAsync(
            scenario,
            woff2Path,
            sourceSha256,
            faceIndex: 0,
            WebFontTextSequence.Create(text),
            generatedRoot,
            [WebFontFormat.Woff2]).ConfigureAwait(false);
        WebFontAsset generated = manifest.Assets.Single();
        string generatedPath = Path.Combine(generatedRoot, generated.Sha256, generated.FileName);
        await using (FileStream stream = File.OpenRead(generatedPath))
        {
            ManagedOpenTypeWebFontVerifier.VerifyContainsSequences(
                stream,
                WebFontFormat.Woff2,
                [WebFontTextSequence.Create(text)]);
        }

        var evidence = new
        {
            schemaVersion = "1.0",
            status = "passed",
            source = "fonts.googleapis.com/fonts.gstatic.com",
            fileName = Path.GetFileName(woff2Path),
            sha256 = sourceSha256,
            decodedSha256 = Convert.ToHexStringLower(SHA256.HashData(decoded)),
            generatedSha256 = generated.Sha256,
            generatedBytes = new FileInfo(generatedPath).Length,
            glyphCount = font.GlyphCount,
            requestedText = text,
            transforms,
            mutationCount,
            acceptedMutations,
            hasGsub = font.TryGetTable("GSUB", out _),
            hasGpos = font.TryGetTable("GPOS", out _)
        };
        await File.WriteAllTextAsync(
            evidencePath,
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
    }

    private static async Task VerifyWoff2CollectionCorpusAsync(
        string woff2Path,
        string referencePath,
        string evidencePath)
    {
        woff2Path = Path.GetFullPath(woff2Path);
        referencePath = Path.GetFullPath(referencePath);
        evidencePath = Path.GetFullPath(evidencePath);
        byte[] woff2 = await File.ReadAllBytesAsync(woff2Path).ConfigureAwait(false);
        byte[] reference = await File.ReadAllBytesAsync(referencePath).ConfigureAwait(false);
        int faceCount = ReadCollectionFaceCount(reference);
        bool referenceHasDsig = ReadCollectionHasDsig(reference, faceCount);
        var faces = new List<object>(faceCount);
        for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
        {
            byte[] decoded = ManagedOpenTypeWebFontVerifier.DecodeWoff2(
                woff2,
                maximumExpandedBytes: 32 * 1024 * 1024,
                faceIndex);
            using (var stream = new MemoryStream(decoded, writable: false))
            {
                ManagedOpenTypeWebFontVerifier.Verify(stream, WebFontFormat.TrueType);
            }

            SfntFont decodedFont = SfntFont.Parse(decoded, 0, 256, validateChecksums: true);
            SfntFont referenceFont = SfntFont.Parse(
                reference,
                faceIndex,
                256,
                validateChecksums: true);
            if (decodedFont.GlyphCount != referenceFont.GlyphCount)
            {
                throw new InvalidDataException(
                    $"WOFF2 collection face {faceIndex} glyph count differs from its W3C reference.");
            }

            string[] referenceTableTags = ReadCollectionFaceTableTags(reference, faceIndex);
            string[] comparedTables = referenceTableTags
                .Where(tag => tag is not ("DSIG" or "glyf" or "loca" or "head" or "hmtx"))
                .ToArray();
            foreach (string tag in comparedTables)
            {
                bool hasDecoded = decodedFont.TryGetTable(tag, out ReadOnlyMemory<byte> decodedTable);
                bool hasReference = referenceFont.TryGetTable(tag, out ReadOnlyMemory<byte> referenceTable);
                if (!hasDecoded || !hasReference || !decodedTable.Span.SequenceEqual(referenceTable.Span))
                {
                    throw new InvalidDataException(
                        $"WOFF2 collection face {faceIndex} table differs from its W3C reference: {tag}.");
                }
            }

            faces.Add(new
            {
                faceIndex,
                decodedSha256 = Convert.ToHexStringLower(SHA256.HashData(decoded)),
                glyphCount = decodedFont.GlyphCount,
                comparedTables
            });
        }

        bool rejectedOutOfRangeFace = false;
        try
        {
            _ = ManagedOpenTypeWebFontVerifier.DecodeWoff2(
                woff2,
                maximumExpandedBytes: 32 * 1024 * 1024,
                faceCount);
        }
        catch (InvalidDataException)
        {
            rejectedOutOfRangeFace = true;
        }
        if (!rejectedOutOfRangeFace)
        {
            throw new InvalidDataException("WOFF2 collection accepted an out-of-range face index.");
        }

        string? directory = Path.GetDirectoryName(evidencePath);
        directory = string.IsNullOrEmpty(directory) ? Directory.GetCurrentDirectory() : directory;
        Directory.CreateDirectory(directory);
        var evidence = new
        {
            schemaVersion = "1.0",
            status = "passed",
            source = "w3c/woff2-compiled-tests",
            woff2Sha256 = Convert.ToHexStringLower(SHA256.HashData(woff2)),
            referenceSha256 = Convert.ToHexStringLower(SHA256.HashData(reference)),
            faceCount,
            referenceHasDsig,
            transforms = ReadWoff2Transforms(woff2),
            rejectedOutOfRangeFace,
            faces
        };
        await File.WriteAllTextAsync(
            evidencePath,
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
    }

    private static (int Count, int Accepted) RunWoff2MutationRobustnessChecks(byte[] source)
    {
        const int mutationCount = 64;
        int accepted = 0;
        for (int index = 0; index < mutationCount; index++)
        {
            byte[] mutation = (byte[])source.Clone();
            int position = 48 + ((index * 7919) % (mutation.Length - 48));
            mutation[position] ^= checked((byte)(1 << (index & 7)));
            try
            {
                using var stream = new MemoryStream(mutation, writable: false);
                ManagedOpenTypeWebFontVerifier.Verify(stream, WebFontFormat.Woff2);
                accepted++;
            }
            catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
            {
                // 預期的有界拒絕。
            }
        }

        return (mutationCount, accepted);
    }

    private static string[] ReadWoff2Transforms(ReadOnlySpan<byte> woff2)
    {
        string[] knownTags =
        [
            "cmap", "head", "hhea", "hmtx", "maxp", "name", "OS/2", "post",
            "cvt ", "fpgm", "glyf", "loca", "prep", "CFF ", "VORG", "EBDT",
            "EBLC", "gasp", "hdmx", "kern", "LTSH", "PCLT", "VDMX", "vhea",
            "vmtx", "BASE", "GDEF", "GPOS", "GSUB", "EBSC", "JSTF", "MATH",
            "CBDT", "CBLC", "COLR", "CPAL", "SVG ", "sbix", "acnt", "avar",
            "bdat", "bloc", "bsln", "cvar", "fdsc", "feat", "fmtx", "fvar",
            "gvar", "hsty", "just", "lcar", "mort", "morx", "opbd", "prop",
            "trak", "Zapf", "Silf", "Glat", "Gloc", "Feat", "Sill"
        ];
        SfntFont.EnsureRange(woff2, 0, 48, "WOFF2-corpus-header");
        ushort count = SfntFont.ReadUInt16(woff2, 12, "WOFF2-corpus-count");
        int position = 48;
        var transforms = new List<string>();
        for (int index = 0; index < count; index++)
        {
            SfntFont.EnsureRange(woff2, position, 1, "WOFF2-corpus-flags");
            byte flags = woff2[position++];
            int tagIndex = flags & 0x3F;
            int version = flags >> 6;
            string tag;
            if (tagIndex == 63)
            {
                SfntFont.EnsureRange(woff2, position, 4, "WOFF2-corpus-tag");
                tag = System.Text.Encoding.ASCII.GetString(woff2.Slice(position, 4));
                position += 4;
            }
            else
            {
                tag = knownTags[tagIndex];
            }

            _ = ReadCorpusUIntBase128(woff2, ref position);
            bool transformed = tag is "glyf" or "loca" ? version != 3 : version != 0;
            if (transformed)
            {
                _ = ReadCorpusUIntBase128(woff2, ref position);
                transforms.Add($"{tag}:v{version}");
            }
        }

        return transforms.ToArray();
    }

    private static uint ReadCorpusUIntBase128(ReadOnlySpan<byte> data, ref int position)
    {
        uint value = 0;
        for (int index = 0; index < 5; index++)
        {
            SfntFont.EnsureRange(data, position, 1, "WOFF2-corpus-base128");
            byte current = data[position++];
            if ((value & 0xFE000000) != 0)
            {
                throw new InvalidDataException("WOFF2 corpus UIntBase128 overflowed.");
            }
            value = (value << 7) | (uint)(current & 0x7F);
            if ((current & 0x80) == 0)
            {
                return value;
            }
        }

        throw new InvalidDataException("WOFF2 corpus UIntBase128 is too long.");
    }

    private static string[] ReadTableTags(ReadOnlySpan<byte> sfnt)
    {
        SfntFont.EnsureRange(sfnt, 0, 12, "WOFF2-corpus-header");
        ushort tableCount = SfntFont.ReadUInt16(sfnt, 4, "WOFF2-corpus-count");
        SfntFont.EnsureRange(sfnt, 12, checked(tableCount * 16), "WOFF2-corpus-directory");
        var tags = new string[tableCount];
        for (int index = 0; index < tableCount; index++)
        {
            tags[index] = System.Text.Encoding.ASCII.GetString(sfnt.Slice(12 + (index * 16), 4));
        }

        return tags;
    }

    private static int ReadCollectionFaceCount(ReadOnlySpan<byte> collection)
    {
        SfntFont.EnsureRange(collection, 0, 12, "WOFF2-corpus-collection-header");
        if (!collection[..4].SequenceEqual("ttcf"u8))
        {
            throw new InvalidDataException("WOFF2 corpus reference is not a font collection.");
        }

        uint count = SfntFont.ReadUInt32(collection, 8, "WOFF2-corpus-collection-count");
        if (count is 0 or > 256)
        {
            throw new InvalidDataException("WOFF2 corpus reference has an invalid face count.");
        }

        return checked((int)count);
    }

    private static string[] ReadCollectionFaceTableTags(ReadOnlySpan<byte> collection, int faceIndex)
    {
        int faceCount = ReadCollectionFaceCount(collection);
        if (faceIndex < 0 || faceIndex >= faceCount)
        {
            throw new InvalidDataException("WOFF2 corpus reference face index is invalid.");
        }

        int offsetPosition = checked(12 + (faceIndex * 4));
        int faceOffset = checked((int)SfntFont.ReadUInt32(
            collection,
            offsetPosition,
            "WOFF2-corpus-collection-offset"));
        return ReadTableTags(collection[faceOffset..]);
    }

    private static bool ReadCollectionHasDsig(ReadOnlySpan<byte> collection, int faceCount)
    {
        uint version = SfntFont.ReadUInt32(collection, 4, "WOFF2-corpus-collection-version");
        if (version == 0x00010000)
        {
            return false;
        }
        if (version != 0x00020000)
        {
            throw new InvalidDataException("WOFF2 corpus reference has an invalid collection version.");
        }

        int dsigPosition = checked(12 + (faceCount * 4));
        SfntFont.EnsureRange(collection, dsigPosition, 12, "WOFF2-corpus-collection-dsig");
        uint tag = SfntFont.ReadUInt32(collection, dsigPosition, "WOFF2-corpus-collection-dsig-tag");
        uint length = SfntFont.ReadUInt32(collection, dsigPosition + 4, "WOFF2-corpus-collection-dsig-length");
        uint offset = SfntFont.ReadUInt32(collection, dsigPosition + 8, "WOFF2-corpus-collection-dsig-offset");
        if (tag == 0 && length == 0 && offset == 0)
        {
            return false;
        }
        if (tag != 0x44534947 || length == 0)
        {
            throw new InvalidDataException("WOFF2 corpus reference has an invalid DSIG record.");
        }

        SfntFont.EnsureRange(
            collection,
            checked((int)offset),
            checked((int)length),
            "WOFF2-corpus-collection-dsig-data");
        return true;
    }

    private static IReadOnlyList<MutationRobustnessResult> RunDeterministicMutationRobustnessChecks(
        string assetRoot,
        WebFontManifest manifest)
    {
        var results = new List<MutationRobustnessResult>();
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
                    ManagedOpenTypeWebFontVerifier.VerifyStructure(stream, format);
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
                throw new InvalidDataException($"The {format} mutation robustness checks did not exercise a rejection path.");
            }

            results.Add(new MutationRobustnessResult(format.ToString(), 128, accepted, rejected));
        }

        return results;
    }

    private static MutationRobustnessResult RunSourceMutationRobustnessChecks(
        string sourcePath,
        WebFontFormat format,
        string resultName)
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
                ManagedOpenTypeWebFontVerifier.VerifyStructure(stream, format);
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
            throw new InvalidDataException("The source-font mutation robustness checks did not exercise a rejection path.");
        }

        return new MutationRobustnessResult(resultName, 64, accepted, rejected);
    }

    private static MutationRobustnessResult RunCffTableMutationRobustnessChecks(string sourcePath)
    {
        byte[] source = File.ReadAllBytes(sourcePath);
        SfntFont font = SfntFont.Parse(source, 0, 256, validateChecksums: true);
        if (!font.TryGetTable("CFF ", out ReadOnlyMemory<byte> table))
        {
            throw new InvalidDataException("The CFF mutation source has no CFF table.");
        }

        byte[] valid = table.ToArray();
        int accepted = 0;
        int rejected = 0;
        uint state = 0x51775176u;
        for (int iteration = 0; iteration < 64; iteration++)
        {
            byte[] mutated;
            if (iteration < 3)
            {
                mutated = (byte[])valid.Clone();
                mutated[new[] { 0, 2, 3 }[iteration]] = 0;
            }
            else if ((iteration & 1) == 0)
            {
                state = NextState(state);
                int length = (int)(state % (uint)valid.Length);
                mutated = valid.AsSpan(0, length).ToArray();
            }
            else
            {
                mutated = (byte[])valid.Clone();
                state = NextState(state);
                int mutationWindow = Math.Min(mutated.Length, 64 * 1024);
                int offset = (int)(state % (uint)mutationWindow);
                state = NextState(state);
                mutated[offset] ^= (byte)(1 << (int)(state & 7));
            }

            try
            {
                CffSubsetter.Validate(mutated, font.GlyphCount, new HashSet<ushort>());
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

        if (rejected < 3)
        {
            throw new InvalidDataException("The direct CFF table mutations did not exercise structural rejection paths.");
        }

        return new MutationRobustnessResult("CffTable", 64, accepted, rejected);
    }

    private static MutationRobustnessResult RunCff2TableMutationRobustnessChecks(string sourcePath)
    {
        byte[] source = File.ReadAllBytes(sourcePath);
        SfntFont font = SfntFont.Parse(source, 0, 256, validateChecksums: true);
        if (!font.TryGetTable("CFF2", out ReadOnlyMemory<byte> table)
            || !font.TryGetTable("fvar", out ReadOnlyMemory<byte> fvar))
        {
            throw new InvalidDataException("The CFF2 mutation source is incomplete.");
        }

        byte[] valid = table.ToArray();
        int accepted = 0;
        int rejected = 0;
        uint state = 0x43464632u;
        for (int iteration = 0; iteration < 32; iteration++)
        {
            byte[] mutated;
            if (iteration < 3)
            {
                mutated = (byte[])valid.Clone();
                int offset = iteration switch { 0 => 0, 1 => 2, _ => 4 };
                mutated[offset] = 0;
            }
            else if ((iteration & 1) == 0)
            {
                state = NextState(state);
                int length = (int)(state % (uint)valid.Length);
                mutated = valid.AsSpan(0, length).ToArray();
            }
            else
            {
                mutated = (byte[])valid.Clone();
                state = NextState(state);
                int mutationWindow = Math.Min(mutated.Length, 64 * 1024);
                int offset = (int)(state % (uint)mutationWindow);
                state = NextState(state);
                mutated[offset] ^= (byte)(1 << (int)(state & 7));
            }

            try
            {
                Cff2Subsetter.Validate(mutated, fvar.ToArray(), font.GlyphCount, new HashSet<ushort>());
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

        if (rejected < 3)
        {
            throw new InvalidDataException("The direct CFF2 table mutations did not exercise structural rejection paths.");
        }

        return new MutationRobustnessResult("Cff2Table", 32, accepted, rejected);
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
        return await VerifySuccessAsync(
            results,
            id,
            sourcePath,
            faceIndex,
            text,
            outputRoot,
            [WebFontFormat.TrueType, WebFontFormat.Woff, WebFontFormat.Woff2]).ConfigureAwait(false);
    }

    private static async Task<WebFontManifest> VerifySuccessAsync(
        ICollection<MatrixResult> results,
        string id,
        string sourcePath,
        int faceIndex,
        string text,
        string outputRoot,
        IReadOnlyList<WebFontFormat> formats)
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
            firstOutput,
            formats).ConfigureAwait(false);
        WebFontManifest second = await GenerateAsync(
            id,
            sourcePath,
            sourceSha256,
            faceIndex,
            sequence,
            secondOutput,
            formats).ConfigureAwait(false);
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
        return GenerateAsync(
            id,
            sourcePath,
            sourceSha256,
            faceIndex,
            sequence,
            destination,
            [WebFontFormat.TrueType, WebFontFormat.Woff, WebFontFormat.Woff2]);
    }

    private static Task<WebFontManifest> GenerateAsync(
        string id,
        string sourcePath,
        string sourceSha256,
        int faceIndex,
        WebFontTextSequence sequence,
        string destination,
        IReadOnlyList<WebFontFormat> formats)
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
                Formats = formats
            },
            destination);
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static byte[] CreateOpenTypeCollection(params byte[][] fonts)
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

    private static byte[] CreateWoff2Collection(params byte[][] fonts)
    {
        FaceSource[] faces = fonts.Select(ParseFace).ToArray();
        int globalTableCount = faces.Sum(face => face.Tables.Length);
        if (faces.Length == 0 || faces.Length > 256 || globalTableCount is 0 or > 256)
        {
            throw new InvalidDataException("The WOFF2 collection fixture exceeds the managed test bounds.");
        }

        using var directory = new MemoryStream();
        using var tableData = new MemoryStream();
        foreach (FaceSource face in faces)
        {
            foreach (TableSource table in face.Tables)
            {
                string tag = System.Text.Encoding.ASCII.GetString(table.Tag);
                int transformVersion = tag is "glyf" or "loca" ? 3 : 0;
                directory.WriteByte(checked((byte)((transformVersion << 6) | 63)));
                directory.Write(table.Tag);
                WriteUIntBase128(directory, checked((uint)table.Data.Length));
                tableData.Write(table.Data);
            }
        }

        WriteUInt32(directory, 0x00010000);
        Write255UInt16(directory, checked((ushort)faces.Length));
        int globalIndex = 0;
        foreach (FaceSource face in faces)
        {
            Write255UInt16(directory, checked((ushort)face.Tables.Length));
            WriteUInt32(directory, BinaryPrimitives.ReadUInt32BigEndian(face.Header));
            for (int table = 0; table < face.Tables.Length; table++)
            {
                Write255UInt16(directory, checked((ushort)globalIndex++));
            }
        }

        byte[] uncompressed = tableData.ToArray();
        var compressed = new byte[BrotliEncoder.GetMaxCompressedLength(uncompressed.Length)];
        if (!BrotliEncoder.TryCompress(
                uncompressed,
                compressed,
                out int compressedLength,
                quality: 11,
                window: 22))
        {
            throw new InvalidDataException("The WOFF2 collection fixture could not be compressed.");
        }

        byte[] directoryBytes = directory.ToArray();
        int contentEnd = checked(48 + directoryBytes.Length + compressedLength);
        var output = new byte[Align4(contentEnd)];
        "wOF2"u8.CopyTo(output);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(4, 4), 0x74746366);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(8, 4), checked((uint)output.Length));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(12, 2), checked((ushort)globalTableCount));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(16, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(20, 4), checked((uint)compressedLength));
        directoryBytes.CopyTo(output, 48);
        compressed.AsSpan(0, compressedLength).CopyTo(output.AsSpan(48 + directoryBytes.Length));
        return output;
    }

    private static void WriteUIntBase128(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[5];
        int index = bytes.Length;
        bytes[--index] = checked((byte)(value & 0x7F));
        while ((value >>= 7) != 0)
        {
            bytes[--index] = checked((byte)((value & 0x7F) | 0x80));
        }

        stream.Write(bytes[index..]);
    }

    private static void Write255UInt16(Stream stream, ushort value)
    {
        if (value < 253)
        {
            stream.WriteByte(checked((byte)value));
            return;
        }

        stream.WriteByte(253);
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static FaceSource ParseFace(byte[] font)
    {
        if (font.Length < 12
            || BinaryPrimitives.ReadUInt32BigEndian(font) is not (0x00010000 or 0x74727565 or 0x4F54544F))
        {
            throw new InvalidDataException("The collection fixture source is not an OpenType sfnt.");
        }

        ushort tableCount = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4, 2));
        int directoryEnd = checked(12 + (tableCount * 16));
        if (tableCount == 0 || directoryEnd > font.Length)
        {
            throw new InvalidDataException("The collection fixture source directory is invalid.");
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
                throw new InvalidDataException("The collection fixture source table is outside the file.");
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

    private sealed record MutationRobustnessResult(
        string Format,
        int Cases,
        int Accepted,
        int Rejected);

    private sealed record LargeCnsDeliveryEvidence(
        string SourceFile,
        string SourceSha256,
        long SourceBytes,
        int ScalarCount,
        int UnicodeRangeSliceSize,
        int AssetCount,
        long FontPayloadBytes,
        long CssBytes,
        long ManifestBytes,
        long ColdPayloadBytes,
        long ElapsedMilliseconds,
        long WorkingSetBytes,
        long AllocatedBytes,
        bool Reproducible);
}
