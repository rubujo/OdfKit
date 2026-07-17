using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Playwright;

namespace OdfKit.WebFontBrowserSmoke;

internal static class LayoutBrowserSmoke
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 7 || args[0] is not ("chromium" or "firefox" or "webkit"))
        {
            Console.Error.WriteLine(
                "Usage: layout <browser> <arabic-source> <arabic-subset> "
                + "<devanagari-source> <devanagari-subset> <screenshot> <evidence>");
            return 2;
        }

        string browserName = args[0];
        string arabicSourcePath = Path.GetFullPath(args[1]);
        string arabicSubsetPath = Path.GetFullPath(args[2]);
        string devanagariSourcePath = Path.GetFullPath(args[3]);
        string devanagariSubsetPath = Path.GetFullPath(args[4]);
        string screenshotPath = Path.GetFullPath(args[5]);
        string evidencePath = Path.GetFullPath(args[6]);
        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);

        byte[] arabicSource = await File.ReadAllBytesAsync(arabicSourcePath).ConfigureAwait(false);
        byte[] arabicSubset = await File.ReadAllBytesAsync(arabicSubsetPath).ConfigureAwait(false);
        byte[] devanagariSource = await File.ReadAllBytesAsync(devanagariSourcePath).ConfigureAwait(false);
        byte[] devanagariSubset = await File.ReadAllBytesAsync(devanagariSubsetPath).ConfigureAwait(false);

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
            ["/fonts/devanagari-subset"] = (devanagariSubset, GetSubsetContentType(devanagariSubset))
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
                    Body = CreatePage()
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
                sources = new
                {
                    arabic = ComputeSha256(arabicSource),
                    devanagari = ComputeSha256(devanagariSource)
                },
                subsets = new
                {
                    arabic = ComputeSha256(arabicSubset),
                    devanagari = ComputeSha256(devanagariSubset)
                },
                proof
            };
            await File.WriteAllTextAsync(
                evidencePath,
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }))
                .ConfigureAwait(false);
            Console.WriteLine($"PASS: {browserName} preserved real Arabic and Devanagari shaping pixels.");
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

    private static string CreatePage()
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
                }
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
                }
            }
        });
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
                :root { color-scheme: dark; font-family: system-ui, sans-serif; }
                body { margin: 0; background: #07131c; color: #eef8ff; }
                main { width: min(1320px, calc(100vw - 48px)); margin: 28px auto; }
                h1 { margin: 0 0 8px; }
                #status { color: #ffd887; }
                #status.pass { color: #7fffc2; }
                article { margin-top: 18px; padding: 20px; border: 1px solid #31566e; border-radius: 16px; background: #0d202c; }
                .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
                .sample { min-height: 100px; padding: 16px; border-radius: 11px; background: #f8f5ed; color: #14232c; font-size: 50px; line-height: 1.5; }
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
                const render = (text, family, direction) => {
                  const canvas = document.querySelector('#proof');
                  const context = canvas.getContext('2d', { willReadFrequently: true });
                  context.clearRect(0, 0, canvas.width, canvas.height);
                  context.fillStyle = '#000';
                  context.font = `82px "${family}"`;
                  context.fontKerning = 'normal';
                  context.textBaseline = 'alphabetic';
                  context.direction = direction;
                  context.textAlign = direction === 'rtl' ? 'right' : 'left';
                  const x = direction === 'rtl' ? canvas.width - 20 : 20;
                  context.fillText(text, x, 150);
                  const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
                  let hash = 2166136261;
                  let alpha = 0;
                  for (let index = 0; index < pixels.length; index++) {
                    hash ^= pixels[index];
                    hash = Math.imul(hash, 16777619);
                    if ((index & 3) === 3) alpha += pixels[index];
                  }
                  const measured = context.measureText(text);
                  const metrics = Object.fromEntries(metricFields.map(field => [field, measured[field]]));
                  return { pixels, hash: hash >>> 0, alpha, metrics };
                };
                const equalMetrics = (left, right) => metricFields.every(field => Math.abs(left[field] - right[field]) <= 0.01);
                const loadFont = async (testCase, family, kind, sample) => {
                  try {
                    const fonts = await document.fonts.load(`82px "${family}"`, sample);
                    if (fonts.length === 0 || !document.fonts.check(`82px "${family}"`, sample)) {
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
                      await loadFont(testCase, testCase.sourceFamily, 'source', sample);
                      await loadFont(testCase, testCase.subsetFamily, 'subset', sample);
                      const article = document.createElement('article');
                      article.innerHTML = `<h2>${testCase.id}</h2><div class="pair"><div class="sample" lang="${testCase.language}" dir="${testCase.direction}" style="font-family:'${testCase.sourceFamily}'">${sample}</div><div class="sample" lang="${testCase.language}" dir="${testCase.direction}" style="font-family:'${testCase.subsetFamily}'">${sample}</div></div><small>source / managed subset</small>`;
                      root.append(article);
                      for (const text of testCase.texts) {
                        const source = render(text, testCase.sourceFamily, testCase.direction);
                        const subset = render(text, testCase.subsetFamily, testCase.direction);
                        let differentBytes = 0;
                        for (let index = 0; index < source.pixels.length; index++) {
                          if (source.pixels[index] !== subset.pixels[index]) differentBytes++;
                        }
                        if (source.alpha === 0 || subset.alpha === 0 || differentBytes !== 0 || !equalMetrics(source.metrics, subset.metrics)) {
                          throw new Error(`${testCase.id}: source/subset shaping mismatch for ${text}; bytes=${differentBytes}`);
                        }
                        results.push({ id: testCase.id, text, sourceHash: source.hash, subsetHash: subset.hash, differentBytes, metrics: source.metrics });
                      }
                    }
                    window.__odfKitLayoutProof = { cases: results };
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
        return template.Replace("__CASES__", cases, StringComparison.Ordinal);
    }

    private static string ComputeSha256(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string GetSubsetContentType(byte[] bytes)
        => bytes.AsSpan().StartsWith("wOF2"u8) ? "font/woff2" : "font/ttf";
}
