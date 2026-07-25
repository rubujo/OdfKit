using Microsoft.Playwright;

try
{
    if (args.Length > 0 && args[0] == "csp")
    {
        return await OdfKit.WebFontBrowserSmoke.CspBrowserSmoke.RunAsync(args[1..]);
    }

    if (args.Length > 0 && args[0] == "layout")
    {
        return await OdfKit.WebFontBrowserSmoke.LayoutBrowserSmoke.RunAsync(args[1..]);
    }

    if (args.Length is < 2 or > 3
        || !Uri.TryCreate(args[0], UriKind.Absolute, out Uri? target)
        || target.Scheme is not ("http" or "https")
        || args[1] is not ("chromium" or "firefox" or "webkit"))
    {
        Console.Error.WriteLine("Usage: OdfKit.WebFontBrowserSmoke <url> <chromium|firefox|webkit> [screenshot-path]");
        return 2;
    }

    string browserName = args[1];
    string screenshotPath = Path.GetFullPath(args.Length == 3
        ? args[2]
        : Path.Combine("artifacts", "webfont-smoke", $"playwright-{browserName}.png"));
    Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
    var errors = new List<string>();

    using IPlaywright playwright = await Playwright.CreateAsync();
    IBrowserType browserType = browserName switch
    {
        "chromium" => playwright.Chromium,
        "firefox" => playwright.Firefox,
        "webkit" => playwright.Webkit,
        _ => throw new InvalidOperationException()
    };
    var launchOptions = new BrowserTypeLaunchOptions
    {
        Headless = true
    };
    if (browserName == "firefox")
    {
        launchOptions.FirefoxUserPrefs = new Dictionary<string, object>
        {
            ["browser.privateWindowSeparation.enabled"] = false
        };
    }
    await using IBrowser browser = await browserType.LaunchAsync(launchOptions);
    IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
    {
        Locale = "zh-TW",
        ViewportSize = new ViewportSize { Width = 1440, Height = 1100 }
    });
    IPage page = await context.NewPageAsync();
    page.Console += (_, message) =>
    {
        if (message.Type == "error")
        {
            errors.Add(message.Text);
        }
    };
    page.PageError += (_, error) => errors.Add(error);

    try
    {
        IResponse? response = await page.GotoAsync(
            target.AbsoluteUri,
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60_000 });
        if (response is null || !response.Ok)
        {
            throw new InvalidOperationException($"Browser navigation failed: {response?.Status}.");
        }

        await page.WaitForFunctionAsync(
            "() => document.body.dataset.internationalReady === 'true' || document.body.dataset.internationalReady === 'false'",
            null,
            new PageWaitForFunctionOptions { Timeout = 60_000 });
        string? ready = await page.GetAttributeAsync("body", "data-international-ready");
        int loadedCases = await page.EvaluateAsync<int>(
            "() => window.__odfKitInternationalProof?.loadedCases?.length ?? 0");
        if (ready != "true" || loadedCases != 1 || errors.Count != 0)
        {
            string statusText = await page.Locator("#status").CountAsync() > 0
                ? await page.Locator("#status").InnerTextAsync().ConfigureAwait(false)
                : string.Empty;
            throw new InvalidOperationException(
                $"Browser proof failed: ready={ready}, loadedCases={loadedCases}, "
                + $"status={statusText}, errors={string.Join(" | ", errors)}");
        }

        if (await page.Locator("#fontSelect").CountAsync() > 0)
        {
            int initialRequestCount = await VerifyDynamicFormatAsync(
                page,
                "cns-sung-plus",
                "Woff2").ConfigureAwait(false);
            ILocator commonPuaProbe = page.Locator("#liveInputPreview, #previewBox").First;
            string inputText = await page.Locator("#txtRareInput, #rareInput").First.InputValueAsync()
                .ConfigureAwait(false);
            string previewText = await commonPuaProbe.EvaluateAsync<string>(
                "element => element.textContent").ConfigureAwait(false);
            if (!previewText.Contains(inputText, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The live preview does not contain the complete rare-character input.");
            }
            byte[] sungPixels = await commonPuaProbe.ScreenshotAsync()
                .ConfigureAwait(false);
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = AddScreenshotSuffix(screenshotPath, "sung-plus"),
                FullPage = true
            }).ConfigureAwait(false);

            await page.Locator("#fontSelect").SelectOptionAsync("cns-kai-plus")
                .ConfigureAwait(false);
            int kaiRequestCount = await VerifyDynamicFormatAsync(
                page,
                "cns-kai-plus",
                "Woff2").ConfigureAwait(false);
            byte[] kaiPixels = await commonPuaProbe.ScreenshotAsync()
                .ConfigureAwait(false);
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = AddScreenshotSuffix(screenshotPath, "kai-plus"),
                FullPage = true
            }).ConfigureAwait(false);
            if (kaiRequestCount <= initialRequestCount || sungPixels.SequenceEqual(kaiPixels))
            {
                throw new InvalidOperationException(
                    "The Sung-to-Kai WOFF2 switch did not issue new requests or change preview pixels.");
            }

            await page.Locator("#fontSelect").SelectOptionAsync("cns-sung-plus")
                .ConfigureAwait(false);
            int finalRequestCount = await VerifyDynamicFormatAsync(
                page,
                "cns-sung-plus",
                "Woff2").ConfigureAwait(false);
            if (finalRequestCount <= kaiRequestCount)
            {
                throw new InvalidOperationException(
                    "The Kai-to-Sung WOFF2 switch did not issue a new request.");
            }
            Console.WriteLine(
                "PASS: Chromium dynamically switched Sung Plus -> Kai Plus -> Sung Plus WOFF2.");

            if (await page.Locator("#formatSelect").CountAsync() > 0)
            {
                foreach (string format in new[] { "Woff", "TrueType", "Woff2" })
                {
                    await page.Locator("#formatSelect").SelectOptionAsync(format)
                        .ConfigureAwait(false);
                    int formatRequestCount = await VerifyDynamicFormatAsync(
                        page,
                        "cns-sung-plus",
                        format).ConfigureAwait(false);
                    if (formatRequestCount <= finalRequestCount)
                    {
                        throw new InvalidOperationException(
                            $"Switching to {format} did not issue a new request.");
                    }
                    finalRequestCount = formatRequestCount;
                    await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = AddScreenshotSuffix(
                            screenshotPath,
                            $"format-{format.ToLowerInvariant()}"),
                        FullPage = true
                    }).ConfigureAwait(false);
                }
                Console.WriteLine(
                    "PASS: Chromium dynamically switched WOFF2 -> WOFF -> TrueType -> WOFF2.");
            }

            if (await page.Locator("#sidecarEnabled").CountAsync() > 0)
            {
                byte[] enabledPixels = await commonPuaProbe.ScreenshotAsync()
                    .ConfigureAwait(false);
                await page.Locator("#sidecarEnabled").UncheckAsync().ConfigureAwait(false);
                int managedRequestCount = await VerifyDynamicFormatAsync(
                    page,
                    "cns-sung-plus",
                    "Woff").ConfigureAwait(false);
                byte[] managedPixels = await commonPuaProbe.ScreenshotAsync()
                    .ConfigureAwait(false);
                string managedStatus = await page.Locator("#sidecarStatus").InnerTextAsync()
                    .ConfigureAwait(false);
                if (managedRequestCount <= finalRequestCount
                    || !managedStatus.Contains("Managed", StringComparison.Ordinal)
                    || managedPixels.Length == 0
                    || enabledPixels.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Disabling Sidecar did not generate a managed WOFF WebFont.");
                }
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = AddScreenshotSuffix(screenshotPath, "managed-woff"),
                    FullPage = true
                }).ConfigureAwait(false);

                foreach (string managedFormat in new[] { "Woff", "TrueType" })
                {
                    await page.Locator("#formatSelect").SelectOptionAsync(managedFormat)
                        .ConfigureAwait(false);
                    int nextManagedRequestCount = await VerifyDynamicFormatAsync(
                        page,
                        "cns-sung-plus",
                        managedFormat).ConfigureAwait(false);
                    if (nextManagedRequestCount <= managedRequestCount)
                    {
                        throw new InvalidOperationException(
                            $"Managed {managedFormat} did not issue a new WebFont request.");
                    }
                    managedRequestCount = nextManagedRequestCount;
                    await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = AddScreenshotSuffix(
                            screenshotPath,
                            $"managed-{managedFormat.ToLowerInvariant()}"),
                        FullPage = true
                    }).ConfigureAwait(false);
                }

                await page.Locator("#formatSelect").SelectOptionAsync("Woff2")
                    .ConfigureAwait(false);
                managedRequestCount = await VerifyDynamicFormatAsync(
                    page,
                    "cns-sung-plus",
                    "Woff").ConfigureAwait(false);
                await page.Locator("#sidecarEnabled").CheckAsync().ConfigureAwait(false);
                int reenabledRequestCount = await VerifyDynamicFormatAsync(
                    page,
                    "cns-sung-plus",
                    "Woff2").ConfigureAwait(false);
                byte[] reenabledPixels = await commonPuaProbe.ScreenshotAsync()
                    .ConfigureAwait(false);
                if (reenabledRequestCount <= managedRequestCount
                    || reenabledPixels.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Re-enabling Sidecar did not issue a WOFF2 WebFont request.");
                }
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = AddScreenshotSuffix(screenshotPath, "sidecar-reenabled"),
                    FullPage = true
                }).ConfigureAwait(false);
                Console.WriteLine(
                    "PASS: Chromium generated managed WOFF and TrueType, then restored Sidecar WOFF2.");
            }
        }

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });
        Console.WriteLine($"PASS: {browserName} loaded the managed CNS WebFont case.");
        Console.WriteLine($"Screenshot: {screenshotPath}");
        return 0;
    }
    catch
    {
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });
        throw;
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static async Task<int> VerifyDynamicFormatAsync(
    IPage page,
    string expectedFontSourceId,
    string expectedFormat)
{
    await page.WaitForFunctionAsync(
        """
        expected => {
            const proof = window.__odfKitInternationalProof?.loadedCases?.[0];
            return document.body.dataset.internationalReady === "true"
                && proof?.fontSourceId === expected.source
                && proof?.format === expected.format;
        }
        """,
        new { source = expectedFontSourceId, format = expectedFormat },
        new PageWaitForFunctionOptions { Timeout = 60_000 }).ConfigureAwait(false);
    int puaScalarCount = await page.EvaluateAsync<int>(
        "() => window.__odfKitInternationalProof.loadedCases[0].puaScalarCount ?? 0")
        .ConfigureAwait(false);
    string? actualFormat = await page.GetAttributeAsync(
        "html",
        "data-odf-asset-format").ConfigureAwait(false);
    string? actualSource = await page.GetAttributeAsync(
        "html",
        "data-odf-selected-font-source").ConfigureAwait(false);
    string? requestedBasic = await page.GetAttributeAsync(
        "html",
        "data-odf-requested-basic").ConfigureAwait(false);
    string? requestedPua = await page.GetAttributeAsync(
        "html",
        "data-odf-requested-pua").ConfigureAwait(false);
    int systemCovered = await page.EvaluateAsync<int>(
        "() => Number(document.documentElement.dataset.odfSystemCovered ?? 0)")
        .ConfigureAwait(false);
    double elapsedMilliseconds = await page.EvaluateAsync<double>(
        "() => Number(document.documentElement.dataset.odfElapsedMilliseconds ?? 0)")
        .ConfigureAwait(false);
    long fontTransferBytes = await page.EvaluateAsync<long>(
        "() => Number(document.documentElement.dataset.odfFontTransferBytes ?? 0)")
        .ConfigureAwait(false);
    if (actualFormat != expectedFormat
        || actualSource != expectedFontSourceId
        || puaScalarCount < 500
        || requestedBasic != "false"
        || requestedPua != "true"
        || systemCovered < 1
        || elapsedMilliseconds <= 0)
    {
        throw new InvalidOperationException(
            $"Dynamic WebFont proof failed: source={actualSource}, format={actualFormat}, "
            + $"PUA={puaScalarCount}, basic={requestedBasic}, requestedPUA={requestedPua}, "
            + $"systemCovered={systemCovered}, elapsed={elapsedMilliseconds:F1} ms.");
    }

    Console.WriteLine(
        $"PERF: {expectedFontSourceId}/{expectedFormat}: "
        + $"{elapsedMilliseconds:F1} ms end-to-end, {fontTransferBytes:N0} font bytes, "
        + $"{systemCovered} system-covered scalars.");
    return await page.EvaluateAsync<int>(
        "() => Number(document.documentElement.dataset.odfRequestCount ?? 0)")
        .ConfigureAwait(false);
}

static string AddScreenshotSuffix(string path, string suffix)
    => Path.Combine(
        Path.GetDirectoryName(path)!,
        $"{Path.GetFileNameWithoutExtension(path)}-{suffix}{Path.GetExtension(path)}");
