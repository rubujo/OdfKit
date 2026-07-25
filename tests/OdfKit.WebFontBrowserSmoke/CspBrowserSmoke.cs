using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Playwright;

namespace OdfKit.WebFontBrowserSmoke;

internal static class CspBrowserSmoke
{
    private const string Csp =
        "default-src 'none'; script-src 'self'; connect-src 'none'; font-src 'self'; "
        + "style-src 'none'; img-src 'none'; object-src 'none'; base-uri 'none'; "
        + "frame-ancestors 'none'; form-action 'none'; manifest-src 'none'; worker-src 'none'; "
        + "require-trusted-types-for 'script'; trusted-types 'none'";

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length > 1
            || args.Length == 1 && args[0] is not ("chromium" or "firefox" or "webkit"))
        {
            Console.Error.WriteLine(
                "Usage: OdfKit.WebFontBrowserSmoke csp [chromium|firefox|webkit]");
            return 2;
        }

        string browserName = args.Length == 0 ? "chromium" : args[0];
        string repositoryRoot = FindRepositoryRoot();
        byte[] helper = await File.ReadAllBytesAsync(Path.Combine(
            repositoryRoot,
            "samples",
            "WebFonts.AspNetCore",
            "wwwroot",
            "webfont-autosubset.js")).ConfigureAwait(false);
        byte[] font = await File.ReadAllBytesAsync(FindTestFont()).ConfigureAwait(false);
        byte[] html = Encoding.UTF8.GetBytes(
            """
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><title>CSP proof</title></head>
            <body data-csp-ready="pending">
            <canvas id="glyph-proof" width="256" height="160"></canvas>
            <p id="proof-status">pending</p>
            <script src="/webfont-autosubset.js"></script>
            <script src="/proof.js"></script>
            </body>
            </html>
            """);
        byte[] proof = Encoding.UTF8.GetBytes(
            """
            "use strict";
            addEventListener("securitypolicyviolation", event => {
                document.body.dataset.cspReady = "false";
                document.body.dataset.cspError = `${event.effectiveDirective}:${event.blockedURI}`;
            });
            (async () => {
                const manifest = await OdfKitWebFontAutoSubset.normalizeManifest({
                    ok: true,
                    status: 200,
                    json: async () => ({
                        Assets: [{
                            FileName: "proof font.ttf",
                            Sha256: "csp-proof",
                            FontFamily: "OdfKit CSP Proof",
                            Format: "TrueType",
                            UnicodeRanges: ["U+0041"]
                        }]
                    })
                });
                if (manifest.assets.length !== 1
                    || manifest.assets[0].fileName !== "proof font.ttf") {
                    throw new Error("PascalCase manifest normalization failed.");
                }
                const camelCase = await OdfKitWebFontAutoSubset.normalizeManifest({
                    assets: [{
                        fileName: "camel.ttf",
                        sha256: "camel-proof",
                        fontFamily: "OdfKit Camel Proof",
                        format: "TrueType",
                        unicodeRanges: []
                    }]
                });
                if (camelCase.assets[0].sha256 !== "camel-proof") {
                    throw new Error("camelCase manifest normalization failed.");
                }
                const detectSystemGlyph =
                    OdfKitWebFontAutoSubset.createSystemGlyphDetector({
                        fontFamily: "Arial, sans-serif",
                        assumePrivateUseMissing: true
                    });
                if (!await detectSystemGlyph("A")
                    || await detectSystemGlyph(String.fromCodePoint(0xFFAE0))) {
                    throw new Error("System-first glyph detection failed.");
                }
                await OdfKitWebFontAutoSubset.injectManifest(manifest, "/fonts");
                const glyph = "A";
                const rendered = await OdfKitWebFontAutoSubset.verifyGlyphRendering(
                    "OdfKit CSP Proof",
                    glyph,
                    { fallbackFamily: "monospace", fontSize: 120 });
                if (!rendered) {
                    throw new Error("The loaded WebFont did not produce distinct glyph pixels.");
                }
                const mixedRendered = await OdfKitWebFontAutoSubset.verifyGlyphRendering(
                    "OdfKit CSP Proof",
                    `A${String.fromCodePoint(0xFFAE0)}`,
                    { fallbackFamily: "monospace", fontSize: 120 });
                if (mixedRendered) {
                    throw new Error("A supported glyph masked a missing glyph in the pixel proof.");
                }
                const canvas = document.getElementById("glyph-proof");
                const context = canvas.getContext("2d");
                context.fillStyle = "#fff";
                context.fillRect(0, 0, canvas.width, canvas.height);
                context.fillStyle = "#000";
                context.font = '120px "OdfKit CSP Proof", monospace';
                context.textBaseline = "top";
                context.fillText(glyph, 16, 8);
                document.getElementById("proof-status").textContent = "rendered";
                document.body.dataset.cspReady = "true";
            })().catch(error => {
                document.body.dataset.cspReady = "false";
                document.body.dataset.cspError = String(error);
            });
            """);

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cancellation = new CancellationTokenSource();
        Task server = ServeAsync(listener, html, helper, proof, font, cancellation.Token);
        var errors = new List<string>();

        try
        {
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
            await using IBrowser browser = await browserType.LaunchAsync(
                launchOptions).ConfigureAwait(false);
            IBrowserContext browserContext = await browser.NewContextAsync(
                new BrowserNewContextOptions { Locale = "en-US" }).ConfigureAwait(false);
            IPage page = await browserContext.NewPageAsync().ConfigureAwait(false);
            page.Console += (_, message) =>
            {
                if (message.Type == "error")
                {
                    errors.Add(message.Text);
                }
            };
            page.PageError += (_, error) => errors.Add(error);

            IResponse? response = await page.GotoAsync(
                $"http://127.0.0.1:{port}/",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30_000 })
                .ConfigureAwait(false);
            await page.WaitForFunctionAsync(
                "() => document.body.dataset.cspReady !== 'pending'",
                null,
                new PageWaitForFunctionOptions { Timeout = 30_000 }).ConfigureAwait(false);
            string? ready = await page.GetAttributeAsync("body", "data-csp-ready").ConfigureAwait(false);
            string? proofError = await page.GetAttributeAsync("body", "data-csp-error").ConfigureAwait(false);
            if (response is null || !response.Ok || ready != "true" || errors.Count != 0)
            {
                throw new InvalidOperationException(
                    $"Strict CSP proof failed: HTTP={response?.Status}, ready={ready}, "
                    + $"proof={proofError}, errors={string.Join(" | ", errors)}");
            }

            string screenshotPath = Path.Combine(
                repositoryRoot,
                "artifacts",
                "webfont-smoke",
                $"csp-{browserName}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            }).ConfigureAwait(false);

            Console.WriteLine(
                $"PASS: {browserName} rendered distinct WebFont glyph pixels under the strict CSP.");
            Console.WriteLine($"Screenshot: {screenshotPath}");
            return 0;
        }
        finally
        {
            cancellation.Cancel();
            listener.Stop();
            await server.ConfigureAwait(false);
        }
    }

    private static async Task ServeAsync(
        TcpListener listener,
        byte[] html,
        byte[] helper,
        byte[] proof,
        byte[] font,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken)
                    .ConfigureAwait(false);
                await using NetworkStream stream = client.GetStream();
                byte[] requestBuffer = new byte[16 * 1024];
                int requestLength = await stream.ReadAsync(requestBuffer, cancellationToken)
                    .ConfigureAwait(false);
                string request = Encoding.ASCII.GetString(requestBuffer, 0, requestLength);
                string target = request.Split("\r\n", 2, StringSplitOptions.None)[0]
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .ElementAtOrDefault(1) ?? "/";
                string path = Uri.UnescapeDataString(new Uri($"http://127.0.0.1{target}").AbsolutePath);
                (byte[] Content, string ContentType, int Status, string Reason) result = path switch
                {
                    "/" => (html, "text/html; charset=utf-8", 200, "OK"),
                    "/webfont-autosubset.js" => (
                        helper,
                        "text/javascript; charset=utf-8",
                        200,
                        "OK"),
                    "/proof.js" => (proof, "text/javascript; charset=utf-8", 200, "OK"),
                    "/fonts/csp-proof/proof font.ttf" => (
                        font,
                        "font/ttf",
                        200,
                        "OK"),
                    _ => (Array.Empty<byte>(), "text/plain; charset=utf-8", 404, "Not Found")
                };
                byte[] headers = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 {result.Status} {result.Reason}\r\n"
                    + $"Content-Type: {result.ContentType}\r\n"
                    + $"Content-Length: {result.Content.Length}\r\n"
                    + $"Content-Security-Policy: {Csp}\r\n"
                    + "X-Content-Type-Options: nosniff\r\n"
                    + "Connection: close\r\n\r\n");
                await stream.WriteAsync(headers, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(result.Content, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(Environment.CurrentDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OdfKit.slnx")))
            {
                return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the OdfKit repository root.");
    }

    private static string FindTestFont()
    {
        string windowsFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string[] candidates =
        [
            Path.Combine(windowsFonts, "arial.ttf"),
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/System/Library/Fonts/Supplemental/Arial.ttf"
        ];
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Could not locate a browser smoke test font.");
    }
}
