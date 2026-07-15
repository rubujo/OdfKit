using Microsoft.Playwright;

if (args.Length is < 1 or > 2
    || !Uri.TryCreate(args[0], UriKind.Absolute, out Uri? target)
    || target.Scheme is not ("http" or "https"))
{
    Console.Error.WriteLine("Usage: OdfKit.WebFontBrowserSmoke <url> [screenshot-path]");
    return 2;
}

string screenshotPath = Path.GetFullPath(args.Length == 2
    ? args[1]
    : Path.Combine("artifacts", "webfont-smoke", "playwright-proof.png"));
Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
var errors = new List<string>();

using IPlaywright playwright = await Playwright.CreateAsync();
await using IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = true
});
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
    if (ready != "true" || loadedCases != 6 || errors.Count != 0)
    {
        throw new InvalidOperationException(
            $"Browser proof failed: ready={ready}, loadedCases={loadedCases}, errors={string.Join(" | ", errors)}");
    }

    await page.ScreenshotAsync(new PageScreenshotOptions
    {
        Path = screenshotPath,
        FullPage = true
    });
    Console.WriteLine($"PASS: Chromium loaded {loadedCases} international WebFont cases.");
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
