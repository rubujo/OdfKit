using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Playwright;

namespace OdfKit.WebFontBrowserSmoke;

internal static class LayoutBrowserSmoke
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string[] s_arabicTexts =
    [
        "السَّلَامُ عَلَيْكُمْ",
        "لا إله إلا الله",
        "بِسْمِ اللَّهِ الرَّحْمَنِ الرَّحِيمِ"
    ];

    private static readonly string[] s_devanagariTexts =
    [
        "क्षेत्रज्ञ भारत",
        "शृंखला हिन्दी",
        "कर्मण्येवाधिकारस्ते"
    ];

    private static readonly string[] s_cffTexts =
    [
        "香港邨裏𠮷",
        "全字庫難字顯示",
        "繁體中文測試"
    ];

    /// <summary>
    /// 依名稱取出必要路徑；缺漏時以明確訊息失敗，而非沿用錯誤的位置引數。
    /// </summary>
    private static string RequirePath(IReadOnlyDictionary<string, string> named, string name)
        => named.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? Path.GetFullPath(value)
            : throw new ArgumentException($"Missing layout argument: {name}");

    internal static async Task<int> RunAsync(string[] args)
    {
        // 具名而非位置式引數：先前為 37 個位置引數，PowerShell 端的追加順序必須與
        // 此處的讀取順序精確對齊，任一方漏改會造成靜默錯配（載入到別的字型）而非
        // 編譯或執行錯誤。改為 name=path 後，缺漏或拼錯會立即以明確訊息失敗，
        // 新增 script 也不再需要維護跨語言的位置契約。
        if (args.Length < 2 || args[0] is not ("chromium" or "firefox" or "webkit"))
        {
            Console.Error.WriteLine(
                "Usage: layout <chromium|firefox|webkit> <name>=<path> …\n"
                + "Required names: arabic-source, arabic-subset, devanagari-source, "
                + "devanagari-subset, cff-source, cff-subset, name-cff-source, name-cff-subset, "
                + "seac-cff-source, seac-cff-subset, static-cff2-source, static-cff2-subset, "
                + "arabic-variable-source, arabic-variable-subset, devanagari-variable-source, "
                + "devanagari-variable-subset, bengali-source, bengali-subset, khmer-source, "
                + "khmer-subset, thai-source, thai-subset, cff2-variable-source, "
                + "cff2-variable-subset, cff-collection-source, cff-collection-subset, "
                + "cff2-collection-source, cff2-collection-subset, color-colrv1-source, "
                + "color-colrv1-subset, color-sbix-source, color-sbix-subset, color-svg-source, "
                + "color-svg-subset, screenshot, evidence");
            return 2;
        }

        string browserName = args[0];
        var named = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string argument in args.Skip(1))
        {
            int separator = argument.IndexOf('=');
            if (separator <= 0)
            {
                Console.Error.WriteLine($"Layout argument must be name=path: {argument}");
                return 2;
            }

            if (!named.TryAdd(argument[..separator], argument[(separator + 1)..]))
            {
                Console.Error.WriteLine($"Duplicate layout argument: {argument[..separator]}");
                return 2;
            }
        }

        string arabicSourcePath = RequirePath(named, "arabic-source");
        string arabicSubsetPath = RequirePath(named, "arabic-subset");
        string devanagariSourcePath = RequirePath(named, "devanagari-source");
        string devanagariSubsetPath = RequirePath(named, "devanagari-subset");
        string cffSourcePath = RequirePath(named, "cff-source");
        string cffSubsetPath = RequirePath(named, "cff-subset");
        string nameCffSourcePath = RequirePath(named, "name-cff-source");
        string nameCffSubsetPath = RequirePath(named, "name-cff-subset");
        string seacCffSourcePath = RequirePath(named, "seac-cff-source");
        string seacCffSubsetPath = RequirePath(named, "seac-cff-subset");
        string staticCff2SourcePath = RequirePath(named, "static-cff2-source");
        string staticCff2SubsetPath = RequirePath(named, "static-cff2-subset");
        string arabicVariableSourcePath = RequirePath(named, "arabic-variable-source");
        string arabicVariableSubsetPath = RequirePath(named, "arabic-variable-subset");
        string devanagariVariableSourcePath = RequirePath(named, "devanagari-variable-source");
        string devanagariVariableSubsetPath = RequirePath(named, "devanagari-variable-subset");
        string bengaliSourcePath = RequirePath(named, "bengali-source");
        string bengaliSubsetPath = RequirePath(named, "bengali-subset");
        string khmerSourcePath = RequirePath(named, "khmer-source");
        string khmerSubsetPath = RequirePath(named, "khmer-subset");
        string thaiSourcePath = RequirePath(named, "thai-source");
        string thaiSubsetPath = RequirePath(named, "thai-subset");
        string cff2VariableSourcePath = RequirePath(named, "cff2-variable-source");
        string cff2VariableSubsetPath = RequirePath(named, "cff2-variable-subset");
        string cffCollectionSourcePath = RequirePath(named, "cff-collection-source");
        string cffCollectionSubsetPath = RequirePath(named, "cff-collection-subset");
        string cff2CollectionSourcePath = RequirePath(named, "cff2-collection-source");
        string cff2CollectionSubsetPath = RequirePath(named, "cff2-collection-subset");
        string colorColrV1SourcePath = RequirePath(named, "color-colrv1-source");
        string colorColrV1SubsetPath = RequirePath(named, "color-colrv1-subset");
        string colorSbixSourcePath = RequirePath(named, "color-sbix-source");
        string colorSbixSubsetPath = RequirePath(named, "color-sbix-subset");
        string colorSvgSourcePath = RequirePath(named, "color-svg-source");
        string colorSvgSubsetPath = RequirePath(named, "color-svg-subset");
        string screenshotPath = RequirePath(named, "screenshot");
        string evidencePath = RequirePath(named, "evidence");
        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);

        byte[] arabicSource = await File.ReadAllBytesAsync(arabicSourcePath).ConfigureAwait(false);
        byte[] arabicSubset = await File.ReadAllBytesAsync(arabicSubsetPath).ConfigureAwait(false);
        byte[] devanagariSource = await File.ReadAllBytesAsync(devanagariSourcePath).ConfigureAwait(false);
        byte[] devanagariSubset = await File.ReadAllBytesAsync(devanagariSubsetPath).ConfigureAwait(false);
        byte[] cffSource = await File.ReadAllBytesAsync(cffSourcePath).ConfigureAwait(false);
        byte[] cffSubset = await File.ReadAllBytesAsync(cffSubsetPath).ConfigureAwait(false);
        byte[] nameCffSource = await File.ReadAllBytesAsync(nameCffSourcePath).ConfigureAwait(false);
        byte[] nameCffSubset = await File.ReadAllBytesAsync(nameCffSubsetPath).ConfigureAwait(false);
        byte[] seacCffSource = await File.ReadAllBytesAsync(seacCffSourcePath).ConfigureAwait(false);
        byte[] seacCffSubset = await File.ReadAllBytesAsync(seacCffSubsetPath).ConfigureAwait(false);
        byte[] staticCff2Source = await File.ReadAllBytesAsync(staticCff2SourcePath).ConfigureAwait(false);
        byte[] staticCff2Subset = await File.ReadAllBytesAsync(staticCff2SubsetPath).ConfigureAwait(false);
        byte[] arabicVariableSource = await File.ReadAllBytesAsync(arabicVariableSourcePath).ConfigureAwait(false);
        byte[] arabicVariableSubset = await File.ReadAllBytesAsync(arabicVariableSubsetPath).ConfigureAwait(false);
        byte[] devanagariVariableSource = await File.ReadAllBytesAsync(devanagariVariableSourcePath)
            .ConfigureAwait(false);
        byte[] devanagariVariableSubset = await File.ReadAllBytesAsync(devanagariVariableSubsetPath)
            .ConfigureAwait(false);
        byte[] bengaliSource = await File.ReadAllBytesAsync(bengaliSourcePath).ConfigureAwait(false);
        byte[] bengaliSubset = await File.ReadAllBytesAsync(bengaliSubsetPath).ConfigureAwait(false);
        byte[] khmerSource = await File.ReadAllBytesAsync(khmerSourcePath).ConfigureAwait(false);
        byte[] khmerSubset = await File.ReadAllBytesAsync(khmerSubsetPath).ConfigureAwait(false);
        byte[] thaiSource = await File.ReadAllBytesAsync(thaiSourcePath).ConfigureAwait(false);
        byte[] thaiSubset = await File.ReadAllBytesAsync(thaiSubsetPath).ConfigureAwait(false);
        byte[] cff2VariableSource = await File.ReadAllBytesAsync(cff2VariableSourcePath).ConfigureAwait(false);
        byte[] cff2VariableSubset = await File.ReadAllBytesAsync(cff2VariableSubsetPath).ConfigureAwait(false);
        byte[] cffCollectionSource = await File.ReadAllBytesAsync(cffCollectionSourcePath).ConfigureAwait(false);
        byte[] cffCollectionSubset = await File.ReadAllBytesAsync(cffCollectionSubsetPath).ConfigureAwait(false);
        byte[] cff2CollectionSource = await File.ReadAllBytesAsync(cff2CollectionSourcePath).ConfigureAwait(false);
        byte[] cff2CollectionSubset = await File.ReadAllBytesAsync(cff2CollectionSubsetPath).ConfigureAwait(false);
        byte[] colorColrV1Source = await File.ReadAllBytesAsync(colorColrV1SourcePath).ConfigureAwait(false);
        byte[] colorColrV1Subset = await File.ReadAllBytesAsync(colorColrV1SubsetPath).ConfigureAwait(false);
        byte[] colorSbixSource = await File.ReadAllBytesAsync(colorSbixSourcePath).ConfigureAwait(false);
        byte[] colorSbixSubset = await File.ReadAllBytesAsync(colorSbixSubsetPath).ConfigureAwait(false);
        byte[] colorSvgSource = await File.ReadAllBytesAsync(colorSvgSourcePath).ConfigureAwait(false);
        byte[] colorSvgSubset = await File.ReadAllBytesAsync(colorSvgSubsetPath).ConfigureAwait(false);

        var errors = new List<string>();
        var browserMessages = new List<string>();
        using IPlaywright playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        IBrowserType browserType = browserName switch
        {
            "chromium" => playwright.Chromium,
            "firefox" => playwright.Firefox,
            "webkit" => playwright.Webkit,
            _ => throw new InvalidOperationException()
        };
        var launchOptions = new BrowserTypeLaunchOptions { Headless = true };
        string? chromiumExecutable = Environment.GetEnvironmentVariable(
            "ODFKIT_PLAYWRIGHT_CHROMIUM_EXECUTABLE");
        if (browserName == "chromium" && !string.IsNullOrWhiteSpace(chromiumExecutable))
        {
            string executablePath = Path.GetFullPath(chromiumExecutable);
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException("Configured Chromium executable does not exist.", executablePath);
            }

            launchOptions.ExecutablePath = executablePath;
        }
        if (browserName == "firefox")
        {
            launchOptions.FirefoxUserPrefs = new Dictionary<string, object>
            {
                ["browser.privateWindowSeparation.enabled"] = false
            };
        }

        await using IBrowser browser = await browserType.LaunchAsync(launchOptions).ConfigureAwait(false);
        IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "en-US",
            ViewportSize = new ViewportSize { Width = 1500, Height = 1100 }
        }).ConfigureAwait(false);
        IPage page = await context.NewPageAsync().ConfigureAwait(false);
        page.Console += (_, message) =>
        {
            browserMessages.Add($"{message.Type}: {message.Text}");
            if (message.Type == "error")
            {
                errors.Add(message.Text);
            }
        };
        page.PageError += (_, error) => errors.Add(error);
        var fontResponses = new Dictionary<string, (byte[] Bytes, string ContentType)>(StringComparer.Ordinal)
        {
            ["/fonts/arabic-source.ttf"] = (arabicSource, "font/ttf"),
            ["/fonts/arabic-subset"] = (arabicSubset, GetSubsetContentType(arabicSubset)),
            ["/fonts/devanagari-source.ttf"] = (devanagariSource, "font/ttf"),
            ["/fonts/devanagari-subset"] = (devanagariSubset, GetSubsetContentType(devanagariSubset)),
            ["/fonts/cff-source.otf"] = (cffSource, "font/otf"),
            ["/fonts/cff-subset"] = (cffSubset, GetSubsetContentType(cffSubset)),
            ["/fonts/name-cff-source.otf"] = (nameCffSource, "font/otf"),
            ["/fonts/name-cff-subset"] = (nameCffSubset, GetSubsetContentType(nameCffSubset)),
            ["/fonts/seac-cff-source.otf"] = (seacCffSource, "font/otf"),
            ["/fonts/seac-cff-subset"] = (seacCffSubset, GetSubsetContentType(seacCffSubset)),
            ["/fonts/static-cff2-source.otf"] = (staticCff2Source, "font/otf"),
            ["/fonts/static-cff2-subset"] = (staticCff2Subset, GetSubsetContentType(staticCff2Subset)),
            ["/fonts/arabic-variable-source.ttf"] = (arabicVariableSource, "font/ttf"),
            ["/fonts/arabic-variable-subset"]
                = (arabicVariableSubset, GetSubsetContentType(arabicVariableSubset)),
            ["/fonts/devanagari-variable-source.ttf"] = (devanagariVariableSource, "font/ttf"),
            ["/fonts/devanagari-variable-subset"]
                = (devanagariVariableSubset, GetSubsetContentType(devanagariVariableSubset)),
            ["/fonts/bengali-source.ttf"] = (bengaliSource, "font/ttf"),
            ["/fonts/bengali-subset"] = (bengaliSubset, GetSubsetContentType(bengaliSubset)),
            ["/fonts/khmer-source.ttf"] = (khmerSource, "font/ttf"),
            ["/fonts/khmer-subset"] = (khmerSubset, GetSubsetContentType(khmerSubset)),
            ["/fonts/thai-source.ttf"] = (thaiSource, "font/ttf"),
            ["/fonts/thai-subset"] = (thaiSubset, GetSubsetContentType(thaiSubset)),
            ["/fonts/cff2-variable-source.otf"] = (cff2VariableSource, "font/otf"),
            ["/fonts/cff2-variable-subset"] = (cff2VariableSubset, GetSubsetContentType(cff2VariableSubset)),
            ["/fonts/cff-collection-source.otc"]
                = (cffCollectionSource, browserName == "chromium" ? "font/collection" : "font/otf"),
            ["/fonts/cff-collection-subset"] = (cffCollectionSubset, GetSubsetContentType(cffCollectionSubset)),
            ["/fonts/cff2-collection-source.otc"]
                = (cff2CollectionSource, browserName == "chromium" ? "font/collection" : "font/otf"),
            ["/fonts/cff2-collection-subset"]
                = (cff2CollectionSubset, GetSubsetContentType(cff2CollectionSubset)),
            ["/fonts/color-colrv1-source.ttf"] = (colorColrV1Source, "font/ttf"),
            ["/fonts/color-colrv1-subset"] = (colorColrV1Subset, GetSubsetContentType(colorColrV1Subset)),
            ["/fonts/color-sbix-source.ttf"] = (colorSbixSource, "font/ttf"),
            ["/fonts/color-sbix-subset"] = (colorSbixSubset, GetSubsetContentType(colorSbixSubset)),
            ["/fonts/color-svg-source.ttf"] = (colorSvgSource, "font/ttf"),
            ["/fonts/color-svg-subset"] = (colorSvgSubset, GetSubsetContentType(colorSvgSubset))
        };
        await page.RouteAsync("https://odfkit.test/**", async route =>
        {
            var uri = new Uri(route.Request.Url);
            if (uri.AbsolutePath == "/")
            {
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "text/html; charset=utf-8",
                    Body = CreatePage(browserName)
                }).ConfigureAwait(false);
                return;
            }

            if (fontResponses.TryGetValue(uri.AbsolutePath, out (byte[] Bytes, string ContentType) response))
            {
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = response.ContentType,
                    BodyBytes = response.Bytes,
                    Headers = new Dictionary<string, string>
                    {
                        ["Access-Control-Allow-Origin"] = "*",
                        ["Cache-Control"] = "public, max-age=31536000, immutable",
                        ["X-Content-Type-Options"] = "nosniff"
                    }
                }).ConfigureAwait(false);
                return;
            }

            await route.FulfillAsync(new RouteFulfillOptions { Status = 404 }).ConfigureAwait(false);
        }).ConfigureAwait(false);

        try
        {
            IResponse? navigation = await page.GotoAsync(
                "https://odfkit.test/",
                new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60_000 })
                .ConfigureAwait(false);
            if (navigation is null || !navigation.Ok)
            {
                throw new InvalidOperationException($"Layout page navigation failed: {navigation?.Status}.");
            }
            await page.WaitForFunctionAsync(
                "() => document.body.dataset.layoutReady === 'true' "
                + "|| document.body.dataset.layoutReady === 'false'",
                null,
                new PageWaitForFunctionOptions { Timeout = 60_000 }).ConfigureAwait(false);
            string? ready = await page.GetAttributeAsync("body", "data-layout-ready").ConfigureAwait(false);
            JsonElement proof = await page.EvaluateAsync<JsonElement>("() => window.__odfKitLayoutProof")
                .ConfigureAwait(false);
            if (ready != "true" || errors.Count != 0)
            {
                throw new InvalidOperationException(
                    $"Layout proof failed: ready={ready}, errors={string.Join(" | ", errors)}, "
                    + $"browser={string.Join(" | ", browserMessages)}");
            }
            VerifyExpectedColorCases(proof, browserName);

            JsonElement domCases = await page.EvaluateAsync<JsonElement>("() => window.__odfKitDomProofCases")
                .ConfigureAwait(false);
            var domProof = new List<object>();
            var sourceHashesByText = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (JsonElement domCase in domCases.EnumerateArray())
            {
                string caseId = domCase.GetProperty("caseId").GetString()!;
                int textIndex = domCase.GetProperty("textIndex").GetInt32();
                string sourceId = domCase.GetProperty("sourceId").GetString()!;
                string subsetId = domCase.GetProperty("subsetId").GetString()!;
                byte[] sourcePng = await page.Locator($"#{sourceId}").ScreenshotAsync().ConfigureAwait(false);
                byte[] subsetPng = await page.Locator($"#{subsetId}").ScreenshotAsync().ConfigureAwait(false);
                if (!sourcePng.AsSpan().SequenceEqual(subsetPng))
                {
                    throw new InvalidOperationException($"DOM source/subset pixels differ for {caseId}.");
                }

                string sourceHash = ComputeSha256(sourcePng);
                string textKey = $"{caseId}:{textIndex}";
                if (!sourceHashesByText.TryGetValue(textKey, out HashSet<string>? hashes))
                {
                    hashes = new HashSet<string>(StringComparer.Ordinal);
                    sourceHashesByText.Add(textKey, hashes);
                }

                hashes.Add(sourceHash);
                domProof.Add(new
                {
                    caseId,
                    textIndex,
                    axes = domCase.GetProperty("axes"),
                    sourceHash,
                    subsetHash = ComputeSha256(subsetPng),
                    pixelIdentical = true
                });
            }

            if (sourceHashesByText.Values.Any(hashes => hashes.Count != 3))
            {
                throw new InvalidOperationException("A DOM variation axis did not change source pixels.");
            }

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            }).ConfigureAwait(false);
            var evidence = new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                browser = browserName,
                rawCollectionRenderingTested = browserName == "chromium",
                sources = new
                {
                    arabic = ComputeSha256(arabicSource),
                    devanagari = ComputeSha256(devanagariSource),
                    cff = ComputeSha256(cffSource),
                    nameCff = ComputeSha256(nameCffSource),
                    seacCff = ComputeSha256(seacCffSource),
                    staticCff2 = ComputeSha256(staticCff2Source),
                    arabicVariable = ComputeSha256(arabicVariableSource),
                    devanagariVariable = ComputeSha256(devanagariVariableSource),
                    bengali = ComputeSha256(bengaliSource),
                    khmer = ComputeSha256(khmerSource),
                    thai = ComputeSha256(thaiSource),
                    cff2Variable = ComputeSha256(cff2VariableSource),
                    cffCollection = ComputeSha256(cffCollectionSource),
                    cff2Collection = ComputeSha256(cff2CollectionSource),
                    colorColrV1 = ComputeSha256(colorColrV1Source),
                    colorSbix = ComputeSha256(colorSbixSource),
                    colorSvg = ComputeSha256(colorSvgSource)
                },
                subsets = new
                {
                    arabic = ComputeSha256(arabicSubset),
                    devanagari = ComputeSha256(devanagariSubset),
                    cff = ComputeSha256(cffSubset),
                    nameCff = ComputeSha256(nameCffSubset),
                    seacCff = ComputeSha256(seacCffSubset),
                    staticCff2 = ComputeSha256(staticCff2Subset),
                    arabicVariable = ComputeSha256(arabicVariableSubset),
                    devanagariVariable = ComputeSha256(devanagariVariableSubset),
                    bengali = ComputeSha256(bengaliSubset),
                    khmer = ComputeSha256(khmerSubset),
                    thai = ComputeSha256(thaiSubset),
                    cff2Variable = ComputeSha256(cff2VariableSubset),
                    cffCollection = ComputeSha256(cffCollectionSubset),
                    cff2Collection = ComputeSha256(cff2CollectionSubset),
                    colorColrV1 = ComputeSha256(colorColrV1Subset),
                    colorSbix = ComputeSha256(colorSbixSubset),
                    colorSvg = ComputeSha256(colorSvgSubset)
                },
                colorRendering = new
                {
                    colrV1 = "verified",
                    sbix = browserName == "chromium" ? "verified" : "browser-unavailable",
                    svg = browserName == "firefox" ? "verified" : "browser-unavailable"
                },
                proof,
                domProof
            };
            await File.WriteAllTextAsync(
                evidencePath,
                JsonSerializer.Serialize(evidence, s_indentedJsonOptions))
                .ConfigureAwait(false);
            Console.WriteLine(
                $"PASS: {browserName} preserved real CFF/CFF2 and TrueType variable pixels.");
            Console.WriteLine($"Evidence: {evidencePath}");
            return 0;
        }
        catch
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            }).ConfigureAwait(false);
            throw;
        }
    }

    private static string CreatePage(string browserName)
    {
        string cases = JsonSerializer.Serialize(new[]
        {
            new
            {
                id = "arabic",
                direction = "rtl",
                language = "ar",
                sourceFamily = "OdfKit Arabic Source",
                subsetFamily = "OdfKit Arabic Subset",
                texts = s_arabicTexts,
                axes = new[] { new { weight = 400, stretch = "normal" } },
                requireAxisDifference = false
            },
            new
            {
                id = "devanagari",
                direction = "ltr",
                language = "hi",
                sourceFamily = "OdfKit Devanagari Source",
                subsetFamily = "OdfKit Devanagari Subset",
                texts = s_devanagariTexts,
                axes = new[] { new { weight = 400, stretch = "normal" } },
                requireAxisDifference = false
            },
            new
            {
                id = "cff",
                direction = "ltr",
                language = "zh-Hant",
                sourceFamily = "OdfKit CFF Source",
                subsetFamily = "OdfKit CFF Subset",
                texts = s_cffTexts,
                axes = new[] { new { weight = 400, stretch = "normal" } },
                requireAxisDifference = false
            },
            new
            {
                id = "name-cff",
                direction = "ltr",
                language = "en",
                sourceFamily = "OdfKit Name CFF Source",
                subsetFamily = "OdfKit Name CFF Subset",
                texts = new[]
                {
                    "OdfKit café",
                    "fi ffi 0123456789",
                    "OdfKit café 0123"
                },
                axes = new[] { new { weight = 400, stretch = "normal" } },
                requireAxisDifference = false
            },
            new
            {
                id = "seac-cff",
                direction = "ltr",
                language = "en",
                sourceFamily = "OdfKit seac CFF Source",
                subsetFamily = "OdfKit seac CFF Subset",
                texts = new[] { "AÁ", "ÁA", "AÁA" },
                axes = new[] { new { weight = 400, stretch = "normal" } },
                requireAxisDifference = false
            },
            new
            {
                id = "static-cff2",
                direction = "ltr",
                language = "en",
                sourceFamily = "OdfKit Static CFF2 Source",
                subsetFamily = "OdfKit Static CFF2 Subset",
                texts = new[] { "ABCabc", "CBAcba", "AaBbCc" },
                axes = new[] { new { weight = 400, stretch = "normal" } },
                requireAxisDifference = false
            },
            new
            {
                id = "arabic-variable",
                direction = "rtl",
                language = "ar",
                sourceFamily = "OdfKit Arabic Variable Source",
                subsetFamily = "OdfKit Arabic Variable Subset",
                texts = new[] { "السَّلَامُ عَلَيْكُمْ" },
                axes = new[]
                {
                    new { weight = 300, stretch = "normal" },
                    new { weight = 700, stretch = "normal" },
                    new { weight = 300, stretch = "condensed" }
                },
                requireAxisDifference = true
            },
            new
            {
                id = "devanagari-variable",
                direction = "ltr",
                language = "hi",
                sourceFamily = "OdfKit Devanagari Variable Source",
                subsetFamily = "OdfKit Devanagari Variable Subset",
                texts = new[] { "क्षेत्रज्ञ भारत" },
                axes = new[]
                {
                    new { weight = 300, stretch = "normal" },
                    new { weight = 700, stretch = "normal" },
                    new { weight = 300, stretch = "condensed" }
                },
                requireAxisDifference = true
            },
            new
            {
                id = "bengali-variable",
                direction = "ltr",
                language = "bn",
                sourceFamily = "OdfKit Bengali Source",
                subsetFamily = "OdfKit Bengali Subset",
                texts = new[] { "বাংলা ভাষা বাংলাদেশ", "ক্ষুদ্র জ্ঞান", "স্বাধীনতা" },
                axes = new[]
                {
                    new { weight = 300, stretch = "normal" },
                    new { weight = 700, stretch = "normal" },
                    new { weight = 300, stretch = "condensed" }
                },
                requireAxisDifference = true
            },
            new
            {
                id = "khmer-variable",
                direction = "ltr",
                language = "km",
                sourceFamily = "OdfKit Khmer Source",
                subsetFamily = "OdfKit Khmer Subset",
                texts = new[] { "ខ្មែរជាភាសាសម្បូរបែប", "សួស្តីពិភពលោក", "កម្ពុជា" },
                axes = new[]
                {
                    new { weight = 300, stretch = "normal" },
                    new { weight = 700, stretch = "normal" },
                    new { weight = 300, stretch = "condensed" }
                },
                requireAxisDifference = true
            },
            new
            {
                id = "thai-variable",
                direction = "ltr",
                language = "th",
                sourceFamily = "OdfKit Thai Source",
                subsetFamily = "OdfKit Thai Subset",
                texts = new[] { "ภาษาไทยกำลังทดสอบ", "สวัสดีชาวโลก", "น้ำใจ" },
                axes = new[]
                {
                    new { weight = 300, stretch = "normal" },
                    new { weight = 700, stretch = "normal" },
                    new { weight = 300, stretch = "condensed" }
                },
                requireAxisDifference = true
            },
            new
            {
                id = "cff2-variable",
                direction = "ltr",
                language = "zh-Hant",
                sourceFamily = "OdfKit CFF2 Variable Source",
                subsetFamily = "OdfKit CFF2 Variable Subset",
                texts = new[] { "繁體字 香港邨裏" },
                axes = new[]
                {
                    new { weight = 300, stretch = "normal" },
                    new { weight = 500, stretch = "normal" },
                    new { weight = 700, stretch = "normal" }
                },
                requireAxisDifference = true
            },
            new
            {
                id = "cff-collection",
                direction = "ltr",
                language = "zh-Hant",
                sourceFamily = "OdfKit CFF Collection Source",
                subsetFamily = "OdfKit CFF Collection Subset",
                texts = new[] { "香港邨裏𠮷" },
                axes = new[] { new { weight = 400, stretch = "normal" } },
                requireAxisDifference = false
            },
            new
            {
                id = "cff2-collection-variable",
                direction = "ltr",
                language = "zh-Hant",
                sourceFamily = "OdfKit CFF2 Collection Source",
                subsetFamily = "OdfKit CFF2 Collection Subset",
                texts = new[] { "繁體字 香港邨裏" },
                axes = new[]
                {
                    new { weight = 300, stretch = "normal" },
                    new { weight = 500, stretch = "normal" },
                    new { weight = 700, stretch = "normal" }
                },
                requireAxisDifference = true
            },
            new
            {
                id = "color-colrv1",
                direction = "ltr",
                language = "und",
                sourceFamily = "OdfKit Color COLRv1 Source",
                subsetFamily = "OdfKit Color COLRv1 Subset",
                texts = new[] { "😀" },
                axes = new[] { new { weight = 400, stretch = "normal" } },
                requireAxisDifference = false
            },
            new
            {
                id = "color-sbix",
                direction = "ltr",
                language = "und",
                sourceFamily = "OdfKit Color sbix Source",
                subsetFamily = "OdfKit Color sbix Subset",
                texts = new[] { "simple_linear" },
                axes = new[] { new { weight = 400, stretch = "normal" } },
                requireAxisDifference = false
            },
            new
            {
                id = "color-svg",
                direction = "ltr",
                language = "und",
                sourceFamily = "OdfKit Color SVG Source",
                subsetFamily = "OdfKit Color SVG Subset",
                texts = new[] { "simple_linear" },
                axes = new[] { new { weight = 400, stretch = "normal" } },
                requireAxisDifference = false
            }
        });
        string collectionFaces = browserName == "chromium"
            ? """
                @font-face { font-family: "OdfKit CFF Collection Source"; src: url("/fonts/cff-collection-source.otc") format("collection"); }
                @font-face { font-family: "OdfKit CFF Collection Subset"; src: url("/fonts/cff-collection-subset"); }
                @font-face { font-family: "OdfKit CFF2 Collection Source"; src: url("/fonts/cff2-collection-source.otc") format("collection"); font-weight: 250 900; }
                @font-face { font-family: "OdfKit CFF2 Collection Subset"; src: url("/fonts/cff2-collection-subset"); font-weight: 250 900; }
                """
            : """
                @font-face { font-family: "OdfKit CFF Collection Source"; src: url("/fonts/cff-collection-source.otc") format("opentype"); }
                @font-face { font-family: "OdfKit CFF Collection Subset"; src: url("/fonts/cff-collection-subset"); }
                @font-face { font-family: "OdfKit CFF2 Collection Source"; src: url("/fonts/cff2-collection-source.otc") format("opentype"); font-weight: 250 900; }
                @font-face { font-family: "OdfKit CFF2 Collection Subset"; src: url("/fonts/cff2-collection-subset"); font-weight: 250 900; }
                """;
        string template = """
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <style>
                @font-face { font-family: "OdfKit Arabic Source"; src: url("/fonts/arabic-source.ttf") format("truetype"); }
                @font-face { font-family: "OdfKit Arabic Subset"; src: url("/fonts/arabic-subset"); }
                @font-face { font-family: "OdfKit Devanagari Source"; src: url("/fonts/devanagari-source.ttf") format("truetype"); }
                @font-face { font-family: "OdfKit Devanagari Subset"; src: url("/fonts/devanagari-subset"); }
                @font-face { font-family: "OdfKit CFF Source"; src: url("/fonts/cff-source.otf") format("opentype"); }
                @font-face { font-family: "OdfKit CFF Subset"; src: url("/fonts/cff-subset"); }
                @font-face { font-family: "OdfKit Name CFF Source"; src: url("/fonts/name-cff-source.otf") format("opentype"); }
                @font-face { font-family: "OdfKit Name CFF Subset"; src: url("/fonts/name-cff-subset"); }
                @font-face { font-family: "OdfKit seac CFF Source"; src: url("/fonts/seac-cff-source.otf") format("opentype"); }
                @font-face { font-family: "OdfKit seac CFF Subset"; src: url("/fonts/seac-cff-subset"); }
                @font-face { font-family: "OdfKit Static CFF2 Source"; src: url("/fonts/static-cff2-source.otf") format("opentype"); }
                @font-face { font-family: "OdfKit Static CFF2 Subset"; src: url("/fonts/static-cff2-subset"); }
                @font-face { font-family: "OdfKit Arabic Variable Source"; src: url("/fonts/arabic-variable-source.ttf") format("truetype"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Arabic Variable Subset"; src: url("/fonts/arabic-variable-subset"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Devanagari Variable Source"; src: url("/fonts/devanagari-variable-source.ttf") format("truetype"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Devanagari Variable Subset"; src: url("/fonts/devanagari-variable-subset"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Bengali Source"; src: url("/fonts/bengali-source.ttf") format("truetype"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Bengali Subset"; src: url("/fonts/bengali-subset"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Khmer Source"; src: url("/fonts/khmer-source.ttf") format("truetype"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Khmer Subset"; src: url("/fonts/khmer-subset"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Thai Source"; src: url("/fonts/thai-source.ttf") format("truetype"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Thai Subset"; src: url("/fonts/thai-subset"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit CFF2 Variable Source"; src: url("/fonts/cff2-variable-source.otf") format("opentype"); font-weight: 250 900; }
                @font-face { font-family: "OdfKit CFF2 Variable Subset"; src: url("/fonts/cff2-variable-subset"); font-weight: 250 900; }
                @font-face { font-family: "OdfKit Color COLRv1 Source"; src: url("/fonts/color-colrv1-source.ttf") format("truetype"); }
                @font-face { font-family: "OdfKit Color COLRv1 Subset"; src: url("/fonts/color-colrv1-subset"); }
                @font-face { font-family: "OdfKit Color sbix Source"; src: url("/fonts/color-sbix-source.ttf") format("truetype"); }
                @font-face { font-family: "OdfKit Color sbix Subset"; src: url("/fonts/color-sbix-subset"); }
                @font-face { font-family: "OdfKit Color SVG Source"; src: url("/fonts/color-svg-source.ttf") format("truetype"); }
                @font-face { font-family: "OdfKit Color SVG Subset"; src: url("/fonts/color-svg-subset"); }
                __COLLECTION_FACES__
                :root { color-scheme: dark; font-family: system-ui, sans-serif; }
                body { margin: 0; background: #07131c; color: #eef8ff; }
                main { width: min(1320px, calc(100vw - 48px)); margin: 28px auto; }
                h1 { margin: 0 0 8px; }
                #status { color: #ffd887; }
                #status.pass { color: #7fffc2; }
                article { margin-top: 18px; padding: 20px; border: 1px solid #31566e; border-radius: 16px; background: #0d202c; }
                .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
                .sample { min-height: 100px; padding: 16px; border-radius: 11px; background: #f8f5ed; color: #14232c; font-size: 50px; line-height: 1.5; }
                .dom-proof { margin-top: 12px; }
                .proof-sample { box-sizing: border-box; width: 620px; height: 130px; overflow: hidden; padding: 12px; background: #f8f5ed; color: #14232c; font-size: 70px; line-height: 1.35; }
                small { display: block; color: #8db5c7; }
              </style>
            </head>
            <body data-layout-ready="pending">
              <main><h1>OdfKit GSUB／GPOS browser differential</h1><p id="status">Running…</p><div id="cases"></div></main>
              <canvas id="proof" width="1600" height="240" hidden></canvas>
              <script>
                const browserName = "__BROWSER__";
                const cases = __CASES__
                  .filter(testCase => testCase.id !== 'color-sbix' || browserName === 'chromium')
                  .filter(testCase => testCase.id !== 'color-svg' || browserName === 'firefox');
                const root = document.querySelector('#cases');
                const status = document.querySelector('#status');
                const metricFields = ['width', 'actualBoundingBoxLeft', 'actualBoundingBoxRight', 'actualBoundingBoxAscent', 'actualBoundingBoxDescent'];
                const domProofCases = [];
                const fontShorthand = (family, axes) => `${axes.weight} 82px "${family}"`;
                const render = (text, family, direction, axes) => {
                  const canvas = document.querySelector('#proof');
                  const context = canvas.getContext('2d', { willReadFrequently: true });
                  context.clearRect(0, 0, canvas.width, canvas.height);
                  context.fillStyle = '#000';
                  context.font = fontShorthand(family, axes);
                  context.fontStretch = axes.stretch;
                  if (context.fontStretch !== axes.stretch) {
                    throw new Error(`Canvas did not apply font-stretch ${axes.stretch}; actual=${context.fontStretch}`);
                  }
                  context.fontKerning = 'normal';
                  context.textBaseline = 'alphabetic';
                  context.direction = direction;
                  context.textAlign = direction === 'rtl' ? 'right' : 'left';
                  const x = direction === 'rtl' ? canvas.width - 20 : 20;
                  context.fillText(text, x, 150);
                  const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
                  let hash = 2166136261;
                  let alpha = 0;
                  let chromatic = 0;
                  for (let index = 0; index < pixels.length; index++) {
                    hash ^= pixels[index];
                    hash = Math.imul(hash, 16777619);
                    if ((index & 3) === 3) alpha += pixels[index];
                    if ((index & 3) === 0 && pixels[index + 3] !== 0
                        && (pixels[index] !== pixels[index + 1] || pixels[index + 1] !== pixels[index + 2])) chromatic++;
                  }
                  const measured = context.measureText(text);
                  const metrics = Object.fromEntries(metricFields.map(field => [field, measured[field]]));
                  return { pixels, hash: hash >>> 0, alpha, chromatic, metrics, appliedStretch: context.fontStretch };
                };
                const equalMetrics = (left, right) => metricFields.every(field => Math.abs(left[field] - right[field]) <= 0.01);
                const loadFont = async (testCase, family, kind, sample, axes) => {
                  try {
                    const shorthand = fontShorthand(family, axes);
                    const fonts = await document.fonts.load(shorthand, sample);
                    if (fonts.length === 0 || !document.fonts.check(shorthand, sample)) {
                      throw new Error('FontFaceSet did not report the face');
                    }
                    return fonts;
                  } catch (error) {
                    throw new Error(`${testCase.id}/${kind}: ${error}`);
                  }
                };
                (async () => {
                  try {
                    const results = [];
                    for (const testCase of cases) {
                      const sample = testCase.texts.join(' ');
                      for (const axes of testCase.axes) {
                        await loadFont(testCase, testCase.sourceFamily, 'source', sample, axes);
                        await loadFont(testCase, testCase.subsetFamily, 'subset', sample, axes);
                      }
                      const article = document.createElement('article');
                      const preview = testCase.axes[testCase.axes.length - 1];
                      article.innerHTML = `<h2>${testCase.id}</h2><div class="pair"><div class="sample" lang="${testCase.language}" dir="${testCase.direction}" style="font-family:'${testCase.sourceFamily}';font-weight:${preview.weight};font-stretch:${preview.stretch}">${sample}</div><div class="sample" lang="${testCase.language}" dir="${testCase.direction}" style="font-family:'${testCase.subsetFamily}';font-weight:${preview.weight};font-stretch:${preview.stretch}">${sample}</div></div><small>source / managed subset</small>`;
                      root.append(article);
                      if (testCase.requireAxisDifference) {
                        const domRoot = document.createElement('div');
                        domRoot.className = 'dom-proof';
                        testCase.axes.forEach((axes, axisIndex) => {
                          testCase.texts.forEach((text, textIndex) => {
                            const sourceId = `dom-${testCase.id}-${axisIndex}-${textIndex}-source`;
                            const subsetId = `dom-${testCase.id}-${axisIndex}-${textIndex}-subset`;
                            const pair = document.createElement('div');
                            pair.className = 'pair';
                            pair.innerHTML = `<div id="${sourceId}" class="proof-sample" lang="${testCase.language}" dir="${testCase.direction}" style="font-family:'${testCase.sourceFamily}';font-weight:${axes.weight};font-stretch:${axes.stretch}">${text}</div><div id="${subsetId}" class="proof-sample" lang="${testCase.language}" dir="${testCase.direction}" style="font-family:'${testCase.subsetFamily}';font-weight:${axes.weight};font-stretch:${axes.stretch}">${text}</div>`;
                            domRoot.append(pair);
                            domProofCases.push({ caseId: testCase.id, textIndex, sourceId, subsetId, axes });
                          });
                        });
                        article.append(domRoot);
                      }
                      const axisHashes = [];
                      for (const axes of testCase.axes) {
                        for (const text of testCase.texts) {
                          const source = render(text, testCase.sourceFamily, testCase.direction, axes);
                          const subset = render(text, testCase.subsetFamily, testCase.direction, axes);
                          let differentBytes = 0;
                          for (let index = 0; index < source.pixels.length; index++) {
                            if (source.pixels[index] !== subset.pixels[index]) differentBytes++;
                          }
                          if (source.alpha === 0 || subset.alpha === 0 || differentBytes !== 0 || !equalMetrics(source.metrics, subset.metrics)) {
                            throw new Error(`${testCase.id}: source/subset shaping mismatch for ${text} at wdth=${axes.stretch},wght=${axes.weight}; bytes=${differentBytes},sourceAlpha=${source.alpha},subsetAlpha=${subset.alpha}`);
                          }
                          if (testCase.id.startsWith('color-') && (source.chromatic === 0 || subset.chromatic === 0)) {
                            throw new Error(`${testCase.id}: loaded glyph did not contain chromatic pixels`);
                          }
                          axisHashes.push(source.hash);
                          results.push({ id: testCase.id, text, axes, sourceHash: source.hash, subsetHash: subset.hash, differentBytes, chromaticPixels: source.chromatic, metrics: source.metrics });
                        }
                      }
                      if (testCase.requireAxisDifference && new Set(axisHashes).size < 2) {
                        throw new Error(`${testCase.id}: tested weight coordinates did not change source pixels`);
                      }
                    }
                    window.__odfKitLayoutProof = { cases: results };
                    window.__odfKitDomProofCases = domProofCases;
                    status.textContent = `PASS: ${results.length} source/subset shaping comparisons are pixel-identical.`;
                    status.className = 'pass';
                    document.body.dataset.layoutReady = 'true';
                  } catch (error) {
                    window.__odfKitLayoutProof = { error: String(error) };
                    status.textContent = `FAIL: ${error}`;
                    document.body.dataset.layoutReady = 'false';
                    console.error(error);
                  }
                })();
              </script>
            </body>
            </html>
            """;
        return template
            .Replace("__BROWSER__", browserName, StringComparison.Ordinal)
            .Replace("__CASES__", cases, StringComparison.Ordinal)
            .Replace("__COLLECTION_FACES__", collectionFaces, StringComparison.Ordinal);
    }

    private static string ComputeSha256(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void VerifyExpectedColorCases(JsonElement proof, string browserName)
    {
        string[] expected = browserName switch
        {
            "chromium" => ["color-colrv1", "color-sbix"],
            "firefox" => ["color-colrv1", "color-svg"],
            _ => ["color-colrv1"]
        };
        string[] actual = proof.GetProperty("cases")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .Where(id => id.StartsWith("color-", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unexpected color proof matrix for {browserName}: {string.Join(",", actual)}.");
        }
    }

    private static string GetSubsetContentType(byte[] bytes)
        => bytes.AsSpan().StartsWith("wOF2"u8) ? "font/woff2" : "font/ttf";
}
