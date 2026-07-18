using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Playwright;

namespace OdfKit.WebFontBrowserSmoke;

internal static class LayoutBrowserSmoke
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 21 || args[0] is not ("chromium" or "firefox" or "webkit"))
        {
            Console.Error.WriteLine(
                "Usage: layout <browser> <arabic-source> <arabic-subset> "
                + "<devanagari-source> <devanagari-subset> <cff-source> <cff-subset> "
                + "<arabic-variable-source> <arabic-variable-subset> "
                + "<devanagari-variable-source> <devanagari-variable-subset> "
                + "<cff2-variable-source> <cff2-variable-subset> "
                + "<cff-collection-source> <cff-collection-subset> "
                + "<cff2-collection-source> <cff2-collection-subset> "
                + "<color-colrv1-source> <color-colrv1-subset> "
                + "<screenshot> <evidence>");
            return 2;
        }

        string browserName = args[0];
        string arabicSourcePath = Path.GetFullPath(args[1]);
        string arabicSubsetPath = Path.GetFullPath(args[2]);
        string devanagariSourcePath = Path.GetFullPath(args[3]);
        string devanagariSubsetPath = Path.GetFullPath(args[4]);
        string cffSourcePath = Path.GetFullPath(args[5]);
        string cffSubsetPath = Path.GetFullPath(args[6]);
        string arabicVariableSourcePath = Path.GetFullPath(args[7]);
        string arabicVariableSubsetPath = Path.GetFullPath(args[8]);
        string devanagariVariableSourcePath = Path.GetFullPath(args[9]);
        string devanagariVariableSubsetPath = Path.GetFullPath(args[10]);
        string cff2VariableSourcePath = Path.GetFullPath(args[11]);
        string cff2VariableSubsetPath = Path.GetFullPath(args[12]);
        string cffCollectionSourcePath = Path.GetFullPath(args[13]);
        string cffCollectionSubsetPath = Path.GetFullPath(args[14]);
        string cff2CollectionSourcePath = Path.GetFullPath(args[15]);
        string cff2CollectionSubsetPath = Path.GetFullPath(args[16]);
        string colorColrV1SourcePath = Path.GetFullPath(args[17]);
        string colorColrV1SubsetPath = Path.GetFullPath(args[18]);
        string screenshotPath = Path.GetFullPath(args[19]);
        string evidencePath = Path.GetFullPath(args[20]);
        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);

        byte[] arabicSource = await File.ReadAllBytesAsync(arabicSourcePath).ConfigureAwait(false);
        byte[] arabicSubset = await File.ReadAllBytesAsync(arabicSubsetPath).ConfigureAwait(false);
        byte[] devanagariSource = await File.ReadAllBytesAsync(devanagariSourcePath).ConfigureAwait(false);
        byte[] devanagariSubset = await File.ReadAllBytesAsync(devanagariSubsetPath).ConfigureAwait(false);
        byte[] cffSource = await File.ReadAllBytesAsync(cffSourcePath).ConfigureAwait(false);
        byte[] cffSubset = await File.ReadAllBytesAsync(cffSubsetPath).ConfigureAwait(false);
        byte[] arabicVariableSource = await File.ReadAllBytesAsync(arabicVariableSourcePath).ConfigureAwait(false);
        byte[] arabicVariableSubset = await File.ReadAllBytesAsync(arabicVariableSubsetPath).ConfigureAwait(false);
        byte[] devanagariVariableSource = await File.ReadAllBytesAsync(devanagariVariableSourcePath)
            .ConfigureAwait(false);
        byte[] devanagariVariableSubset = await File.ReadAllBytesAsync(devanagariVariableSubsetPath)
            .ConfigureAwait(false);
        byte[] cff2VariableSource = await File.ReadAllBytesAsync(cff2VariableSourcePath).ConfigureAwait(false);
        byte[] cff2VariableSubset = await File.ReadAllBytesAsync(cff2VariableSubsetPath).ConfigureAwait(false);
        byte[] cffCollectionSource = await File.ReadAllBytesAsync(cffCollectionSourcePath).ConfigureAwait(false);
        byte[] cffCollectionSubset = await File.ReadAllBytesAsync(cffCollectionSubsetPath).ConfigureAwait(false);
        byte[] cff2CollectionSource = await File.ReadAllBytesAsync(cff2CollectionSourcePath).ConfigureAwait(false);
        byte[] cff2CollectionSubset = await File.ReadAllBytesAsync(cff2CollectionSubsetPath).ConfigureAwait(false);
        byte[] colorColrV1Source = await File.ReadAllBytesAsync(colorColrV1SourcePath).ConfigureAwait(false);
        byte[] colorColrV1Subset = await File.ReadAllBytesAsync(colorColrV1SubsetPath).ConfigureAwait(false);

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
            ["/fonts/arabic-variable-source.ttf"] = (arabicVariableSource, "font/ttf"),
            ["/fonts/arabic-variable-subset"]
                = (arabicVariableSubset, GetSubsetContentType(arabicVariableSubset)),
            ["/fonts/devanagari-variable-source.ttf"] = (devanagariVariableSource, "font/ttf"),
            ["/fonts/devanagari-variable-subset"]
                = (devanagariVariableSubset, GetSubsetContentType(devanagariVariableSubset)),
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
            ["/fonts/color-colrv1-subset"] = (colorColrV1Subset, GetSubsetContentType(colorColrV1Subset))
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

            JsonElement domCases = await page.EvaluateAsync<JsonElement>("() => window.__odfKitDomProofCases")
                .ConfigureAwait(false);
            var domProof = new List<object>();
            var sourceHashesByCase = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (JsonElement domCase in domCases.EnumerateArray())
            {
                string caseId = domCase.GetProperty("caseId").GetString()!;
                string sourceId = domCase.GetProperty("sourceId").GetString()!;
                string subsetId = domCase.GetProperty("subsetId").GetString()!;
                byte[] sourcePng = await page.Locator($"#{sourceId}").ScreenshotAsync().ConfigureAwait(false);
                byte[] subsetPng = await page.Locator($"#{subsetId}").ScreenshotAsync().ConfigureAwait(false);
                if (!sourcePng.AsSpan().SequenceEqual(subsetPng))
                {
                    throw new InvalidOperationException($"DOM source/subset pixels differ for {caseId}.");
                }

                string sourceHash = ComputeSha256(sourcePng);
                if (!sourceHashesByCase.TryGetValue(caseId, out HashSet<string>? hashes))
                {
                    hashes = new HashSet<string>(StringComparer.Ordinal);
                    sourceHashesByCase.Add(caseId, hashes);
                }

                hashes.Add(sourceHash);
                domProof.Add(new
                {
                    caseId,
                    axes = domCase.GetProperty("axes"),
                    sourceHash,
                    subsetHash = ComputeSha256(subsetPng),
                    pixelIdentical = true
                });
            }

            if (sourceHashesByCase.Values.Any(hashes => hashes.Count != 3))
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
                    arabicVariable = ComputeSha256(arabicVariableSource),
                    devanagariVariable = ComputeSha256(devanagariVariableSource),
                    cff2Variable = ComputeSha256(cff2VariableSource),
                    cffCollection = ComputeSha256(cffCollectionSource),
                    cff2Collection = ComputeSha256(cff2CollectionSource),
                    colorColrV1 = ComputeSha256(colorColrV1Source)
                },
                subsets = new
                {
                    arabic = ComputeSha256(arabicSubset),
                    devanagari = ComputeSha256(devanagariSubset),
                    cff = ComputeSha256(cffSubset),
                    arabicVariable = ComputeSha256(arabicVariableSubset),
                    devanagariVariable = ComputeSha256(devanagariVariableSubset),
                    cff2Variable = ComputeSha256(cff2VariableSubset),
                    cffCollection = ComputeSha256(cffCollectionSubset),
                    cff2Collection = ComputeSha256(cff2CollectionSubset),
                    colorColrV1 = ComputeSha256(colorColrV1Subset)
                },
                proof,
                domProof
            };
            await File.WriteAllTextAsync(
                evidencePath,
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }))
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
                texts = new[]
                {
                    "السَّلَامُ عَلَيْكُمْ",
                    "لا إله إلا الله",
                    "بِسْمِ اللَّهِ الرَّحْمَنِ الرَّحِيمِ"
                },
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
                texts = new[]
                {
                    "क्षेत्रज्ञ भारत",
                    "शृंखला हिन्दी",
                    "कर्मण्येवाधिकारस्ते"
                },
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
                texts = new[]
                {
                    "香港邨裏𠮷",
                    "全字庫難字顯示",
                    "繁體中文測試"
                },
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
                @font-face { font-family: "OdfKit Arabic Variable Source"; src: url("/fonts/arabic-variable-source.ttf") format("truetype"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Arabic Variable Subset"; src: url("/fonts/arabic-variable-subset"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Devanagari Variable Source"; src: url("/fonts/devanagari-variable-source.ttf") format("truetype"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit Devanagari Variable Subset"; src: url("/fonts/devanagari-variable-subset"); font-weight: 100 900; font-stretch: 62.5% 100%; }
                @font-face { font-family: "OdfKit CFF2 Variable Source"; src: url("/fonts/cff2-variable-source.otf") format("opentype"); font-weight: 250 900; }
                @font-face { font-family: "OdfKit CFF2 Variable Subset"; src: url("/fonts/cff2-variable-subset"); font-weight: 250 900; }
                @font-face { font-family: "OdfKit Color COLRv1 Source"; src: url("/fonts/color-colrv1-source.ttf") format("truetype"); }
                @font-face { font-family: "OdfKit Color COLRv1 Subset"; src: url("/fonts/color-colrv1-subset"); }
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
                const cases = __CASES__;
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
                            domProofCases.push({ caseId: testCase.id, sourceId, subsetId, axes });
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
                            throw new Error(`${testCase.id}: source/subset shaping mismatch for ${text} at wdth=${axes.stretch},wght=${axes.weight}; bytes=${differentBytes}`);
                          }
                          if (testCase.id === 'color-colrv1' && (source.chromatic === 0 || subset.chromatic === 0)) {
                            throw new Error('color-colrv1: loaded glyph did not contain chromatic pixels');
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
            .Replace("__CASES__", cases, StringComparison.Ordinal)
            .Replace("__COLLECTION_FACES__", collectionFaces, StringComparison.Ordinal);
    }

    private static string ComputeSha256(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string GetSubsetContentType(byte[] bytes)
        => bytes.AsSpan().StartsWith("wOF2"u8) ? "font/woff2" : "font/ttf";
}
