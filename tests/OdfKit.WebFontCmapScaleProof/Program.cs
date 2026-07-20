// 驗證 cmap format 4 規模路徑的實機證據：
//   dense — 單片子集超過 8,188 個 BMP 字元（修正前必定以 cmap4-size 失敗）。
//   sparse — 合併後 segment 數超過 16-bit length 上限，format 4 依規格省略，
//            僅保留 (3,10)/format 12。
// 兩者皆以真實字型產生，檢查實際輸出的 cmap encoding record，再於
// Chromium／Firefox／WebKit 實際載入並確認要求的字元真的以該字型描繪。
using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;
using OdfKit.WebFonts;
using OdfKit.WebFonts.OpenType;

const string SourceSha256 = "10e6d832bc73650840aa7fbfec4e10c527f8136ae2aec71c3e1c13a67475c24a";

string root = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("artifacts", "cmap-scale-proof"));

// 第二個引數可直接指向既有的鎖定字型（CI 由 format matrix 的 corpus cache 取得），
// 就地讀取而不複製，避免在 runner 磁碟上多放一份 16 MB 副本。
string sourcePath = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
    ? Path.GetFullPath(args[1])
    : Path.Combine(root, "sources", "SourceHanSansTC-Regular.otf");
string outputRoot = Path.Combine(root, "assets");
string evidenceRoot = Path.Combine(root, "evidence");
Directory.CreateDirectory(outputRoot);
Directory.CreateDirectory(evidenceRoot);

if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine($"缺少鎖定來源字型：{sourcePath}");
    return 2;
}

string actualSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(sourcePath)));
if (!string.Equals(actualSha256, SourceSha256, StringComparison.Ordinal))
{
    Console.Error.WriteLine($"來源字型 SHA-256 不符：{actualSha256}");
    return 2;
}

Console.WriteLine($"來源字型已驗證 SHA-256：{actualSha256}");

// 由來源 cmap 取得實際可用的 BMP 純量，確保案例規模以真實字型能力為準。
int[] bmpScalars = ReadSourceBmpScalars(sourcePath);
Console.WriteLine($"來源字型 BMP 可用純量：{bmpScalars.Length:N0}");
if (bmpScalars.Length < 20_000)
{
    Console.Error.WriteLine("來源字型 BMP 覆蓋不足以驗證規模路徑。");
    return 2;
}

// dense：取連續區段，合併後 segment 數少，format 4 應存在且字元數遠超 8,188。
int[] dense = bmpScalars.Take(12_000).ToArray();

// sparse：每隔一個取一個，使相鄰碼位不連續而無法合併，segment 數超過上限。
int[] sparse = bmpScalars.Where((_, index) => index % 2 == 0).Take(9_000).ToArray();

var cases = new[]
{
    new ProofCase("dense", dense, ExpectFormat4: true),
    new ProofCase("sparse", sparse, ExpectFormat4: false)
};

var engineOptions = new ManagedOpenTypeWebFontEngineOptions
{
    MaxUnicodeScalars = 200_000,
    MaxOutputBytes = 64L * 1024 * 1024,
    // CharString 全量驗證對 6.5 萬字圖的 CFF 來源過慢，且本次證明目標是 cmap 規模路徑；
    // 產出仍會被重新解析與結構驗證。
    VerifyEveryOutputCharString = false
};
engineOptions.FontSources.Add("proof", sourcePath);
var engine = new ManagedOpenTypeWebFontSubsetEngine(engineOptions);

var results = new List<CaseResult>();
foreach (ProofCase proofCase in cases)
{
    Console.WriteLine($"[{proofCase.Name}] 產生 {proofCase.Scalars.Length:N0} 個 BMP 字元的子集…");
    WebFontManifest manifest = await engine.GenerateAsync(
        new WebFontSubsetRequest
        {
            Face = new WebFontFaceIdentity
            {
                FontSourceId = "proof",
                SourceSha256 = SourceSha256,
                FaceIndex = 0
            },
            ProfileId = "cmap-scale-proof-v1",
            FontFamily = $"OdfKitProof{proofCase.Name}",
            Sequences = proofCase.Scalars
                .Select(scalar => WebFontTextSequence.Create(char.ConvertFromUtf32(scalar)))
                .ToArray(),
            // 同時產生 OTF 與 WOFF2：OTF 為未壓縮 sfnt，可直接檢查 cmap；
            // 送進瀏覽器的則是實際部署用的 WOFF2。
            Formats = [WebFontFormat.OpenType, WebFontFormat.Woff2],
            RequiredBrowserTargets = []
        },
        outputRoot,
        CancellationToken.None).ConfigureAwait(false);

    WebFontAsset asset = manifest.Assets.Single(item => item.Format == WebFontFormat.Woff2);
    WebFontAsset sfntAsset = manifest.Assets.Single(item => item.Format == WebFontFormat.OpenType);
    string assetPath = Path.Combine(outputRoot, asset.Sha256, asset.FileName);
    string sfntPath = Path.Combine(outputRoot, sfntAsset.Sha256, sfntAsset.FileName);

    // 檢查實際輸出的 cmap encoding record，而非只相信產生成功。
    (ushort Platform, ushort Encoding)[] records = ReadEncodingRecords(sfntPath);
    bool hasFormat4 = records.Any(record => record is (3, 1));
    bool hasFormat12 = records.Any(record => record is (3, 10));
    bool sorted = records
        .Select(record => ((int)record.Platform << 16) | record.Encoding)
        .Zip(records.Skip(1).Select(record => ((int)record.Platform << 16) | record.Encoding))
        .All(pair => pair.First < pair.Second);

    Console.WriteLine(
        $"[{proofCase.Name}] {asset.ByteLength:N0} bytes；encoding records = "
        + string.Join(", ", records.Select(record => $"({record.Platform},{record.Encoding})")));

    if (hasFormat4 != proofCase.ExpectFormat4)
    {
        Console.Error.WriteLine(
            $"[{proofCase.Name}] format 4 是否存在不符預期：實際 {hasFormat4}，預期 {proofCase.ExpectFormat4}");
        return 1;
    }

    if (!hasFormat12 || !sorted)
    {
        Console.Error.WriteLine($"[{proofCase.Name}] 缺少 format 12 或 encoding record 未排序。");
        return 1;
    }

    results.Add(new CaseResult(
        proofCase.Name,
        proofCase.Scalars,
        asset,
        assetPath,
        records.Select(record => $"({record.Platform},{record.Encoding})").ToArray(),
        hasFormat4,
        ExpectBrowserAccept: true,
        Truncate: false));
}

// 負向對照：同一資產截斷後供應。若瀏覽器仍回報 faceAccepted，代表量測本身無效，
// 正向案例的通過也不能採信。
CaseResult denseResult = results.First(result => result.Name == "dense");
results.Add(denseResult with
{
    Name = "control",
    ExpectBrowserAccept = false,
    Truncate = true
});

// 以本機 HTTP 服務資產，讓瀏覽器走真實的 @font-face CORS 抓取路徑。
using var listener = new HttpListener();
int port = GetFreePort();
string origin = $"http://127.0.0.1:{port}";
listener.Prefixes.Add($"{origin}/");
listener.Start();
using var listenerCancellation = new CancellationTokenSource();
Task serverTask = ServeAsync(listener, results, listenerCancellation.Token);
Console.WriteLine($"本機資產服務啟動：{origin}");

var browserEvidence = new List<object>();
int exitCode = 0;
using IPlaywright playwright = await Playwright.CreateAsync().ConfigureAwait(false);
foreach (string browserName in new[] { "chromium", "firefox", "webkit" })
{
    IBrowserType browserType = browserName switch
    {
        "chromium" => playwright.Chromium,
        "firefox" => playwright.Firefox,
        _ => playwright.Webkit
    };

    await using IBrowser browser = await browserType
        .LaunchAsync(new BrowserTypeLaunchOptions { Headless = true })
        .ConfigureAwait(false);
    IBrowserContext context = await browser
        .NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "zh-TW",
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 }
        })
        .ConfigureAwait(false);
    IPage page = await context.NewPageAsync().ConfigureAwait(false);
    var consoleErrors = new List<string>();
    page.Console += (_, message) =>
    {
        if (message.Type == "error")
        {
            consoleErrors.Add(message.Text);
        }
    };
    page.PageError += (_, error) => consoleErrors.Add(error);

    foreach (CaseResult result in results)
    {
        IResponse? response = await page
            .GotoAsync($"{origin}/{result.Name}.html", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 120_000
            })
            .ConfigureAwait(false);
        if (response is null || !response.Ok)
        {
            Console.Error.WriteLine($"[{browserName}/{result.Name}] 導覽失敗：{response?.Status}");
            exitCode = 1;
            continue;
        }

        await page
            .WaitForFunctionAsync(
                "() => window.__proof !== undefined",
                null,
                new PageWaitForFunctionOptions { Timeout = 120_000 })
            .ConfigureAwait(false);
        ProofReport report = (await page
            .EvaluateAsync<JsonElement>("() => window.__proof")
            .ConfigureAwait(false))
            .Deserialize<ProofReport>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        string screenshotPath = Path.Combine(evidenceRoot, $"{result.Name}-{browserName}.png");
        await page
            .ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = false })
            .ConfigureAwait(false);

        bool ok = result.ExpectBrowserAccept
            ? report.Loaded
                && report.Checked
                && report.FaceAccepted
                && report.RenderedGlyphs == report.SampledGlyphs
                && consoleErrors.Count == 0
            // 負向對照只要求瀏覽器不接受該 FontFace；載入錯誤與 console 訊息屬預期。
            : !report.FaceAccepted;
        Console.WriteLine(
            $"[{browserName}/{result.Name}] loaded={report.Loaded} checked={report.Checked} "
            + $"faceAccepted={report.FaceAccepted} glyphs={report.RenderedGlyphs}/{report.SampledGlyphs} "
            + $"errors={consoleErrors.Count} expectAccept={result.ExpectBrowserAccept} "
            + $"-> {(ok ? "PASS" : "FAIL")}");
        if (!ok)
        {
            exitCode = 1;
        }

        browserEvidence.Add(new
        {
            browser = browserName,
            caseName = result.Name,
            scalarCount = result.Scalars.Length,
            encodingRecords = result.EncodingRecords,
            hasFormat4 = result.HasFormat4,
            assetSha256 = result.Asset.Sha256,
            assetBytes = result.Asset.ByteLength,
            report.Loaded,
            report.Checked,
            report.FaceAccepted,
            report.SampledGlyphs,
            report.RenderedGlyphs,
            consoleErrors,
            screenshot = Path.GetFileName(screenshotPath)
        });
        consoleErrors.Clear();
    }
}

listenerCancellation.Cancel();
listener.Stop();
try
{
    await serverTask.ConfigureAwait(false);
}
catch (OperationCanceledException)
{
}
catch (HttpListenerException)
{
}
catch (ObjectDisposedException)
{
}

string evidencePath = Path.Combine(evidenceRoot, "cmap-scale-browser-proof.json");
File.WriteAllText(
    evidencePath,
    JsonSerializer.Serialize(
        new
        {
            sourceFont = "SourceHanSansTC-Regular.otf",
            sourceVersion = "2.005R",
            sourceSha256 = SourceSha256,
            sourceLicense = "OFL-1.1",
            sourceBmpScalars = bmpScalars.Length,
            cases = browserEvidence
        },
        new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"證據已寫出：{evidencePath}");
Console.WriteLine(exitCode == 0 ? "PASS：兩個 cmap 規模路徑在三個瀏覽器均通過。" : "FAIL：見上列訊息。");
return exitCode;

static int GetFreePort()
{
    using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
    probe.Start();
    int port = ((IPEndPoint)probe.LocalEndpoint).Port;
    probe.Stop();
    return port;
}

static async Task ServeAsync(
    HttpListener listener,
    IReadOnlyList<CaseResult> results,
    CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
        string path = context.Request.Url!.AbsolutePath.Trim('/');
        CaseResult? match = results.FirstOrDefault(
            result => path == $"{result.Name}.html" || path == $"{result.Name}.woff2");
        if (match is null)
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            continue;
        }

        byte[] payload;
        if (path.EndsWith(".woff2", StringComparison.Ordinal))
        {
            payload = File.ReadAllBytes(match.AssetPath);
            if (match.Truncate)
            {
                payload = payload.AsSpan(0, payload.Length * 6 / 10).ToArray();
            }

            context.Response.ContentType = "font/woff2";
        }
        else
        {
            payload = Encoding.UTF8.GetBytes(CreateHtml(match));
            context.Response.ContentType = "text/html; charset=utf-8";
        }

        context.Response.ContentLength64 = payload.Length;
        await context.Response.OutputStream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        context.Response.Close();
    }
}

// 取樣的字元同時用於 canvas 逐字描繪檢查；取頭、中、尾以涵蓋 cmap 各段。
static int[] SampleScalars(IReadOnlyList<int> scalars)
    => new[] { 0, scalars.Count / 4, scalars.Count / 2, scalars.Count * 3 / 4, scalars.Count - 1 }
        .Distinct()
        .Select(index => scalars[index])
        .ToArray();

static string CreateHtml(CaseResult result)
{
    int[] sample = SampleScalars(result.Scalars);
    string sampleJson = JsonSerializer.Serialize(sample);
    string sampleText = string.Concat(sample.Select(char.ConvertFromUtf32));
    string family = $"OdfKitProof{result.Name}";
    return $$"""
<!doctype html>
<html lang="zh-TW">
<head>
<meta charset="utf-8">
<title>OdfKit cmap scale proof — {{result.Name}}</title>
<style>
@font-face {
  font-family: '{{family}}';
  src: url('./{{result.Name}}.woff2') format('woff2');
  font-display: block;
}
body { margin: 24px; font-size: 48px; }
#probe { font-family: '{{family}}', monospace; }
#fallback { font-family: monospace; }
</style>
</head>
<body>
<div id="probe">{{sampleText}}</div>
<div id="fallback">{{sampleText}}</div>
<canvas id="canvas" width="96" height="96"></canvas>
<script>
(async () => {
  const family = '{{family}}';
  const sample = {{sampleJson}};
  const report = {
    loaded: false, checked: false, faceAccepted: false,
    sampledGlyphs: sample.length, renderedGlyphs: 0
  };
  try {
    await document.fonts.load(`48px ${family}`, {{JsonSerializer.Serialize(sampleText)}});
    await document.fonts.ready;
    report.loaded = true;
    report.checked = document.fonts.check(`48px ${family}`, {{JsonSerializer.Serialize(sampleText)}});

    // 決定性證據：確認 document.fonts 內確實有一個屬於本子集的 FontFace 且
    // status 為 loaded。字型若被瀏覽器的 sanitizer 拒絕，狀態會是 error，
    // 此時即使 canvas 仍有墨跡也只是來自 fallback，不能算通過。
    document.fonts.forEach((face) => {
      if (face.family.replace(/['"]/g, '') === family && face.status === 'loaded') {
        report.faceAccepted = true;
      }
    });

    // 逐字以 canvas 描繪，確認取樣字元都有實際墨跡（非空白、非豆腐框以外的全空）。
    const canvas = document.getElementById('canvas');
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    for (const scalar of sample) {
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      ctx.fillStyle = '#000';
      ctx.font = `64px ${family}`;
      ctx.textBaseline = 'top';
      ctx.fillText(String.fromCodePoint(scalar), 8, 8);
      const data = ctx.getImageData(0, 0, canvas.width, canvas.height).data;
      let ink = 0;
      for (let i = 3; i < data.length; i += 4) { if (data[i] > 0) { ink++; } }
      if (ink > 0) { report.renderedGlyphs++; }
    }
  } catch (error) {
    report.error = String(error);
  }
  window.__proof = report;
})();
</script>
</body>
</html>
""";
}

static int[] ReadSourceBmpScalars(string path)
{
    SfntFont font = SfntFont.Parse(File.ReadAllBytes(path), 0, 256, validateChecksums: true);

    // 限定為必有墨跡的 CJK 表意文字區段（Ext A、URO、相容表意文字）。
    // 先前以 0x3000 起算會納入 U+3000 IDEOGRAPHIC SPACE 等空白字元，
    // 使逐字 canvas 墨跡檢查出現非缺陷的失敗。
    return font.UnicodeScalars
        .Where(scalar => scalar is (>= 0x3400 and <= 0x4DBF)
            or (>= 0x4E00 and <= 0x9FFF)
            or (>= 0xF900 and <= 0xFAFF))
        .Where(scalar => font.GetGlyphId(scalar) != 0)
        .OrderBy(scalar => scalar)
        .ToArray();
}

static (ushort Platform, ushort Encoding)[] ReadEncodingRecords(string path)
{
    byte[] sfnt = File.ReadAllBytes(path);
    ushort tableCount = BinaryPrimitives.ReadUInt16BigEndian(sfnt.AsSpan(4, 2));
    for (int index = 0; index < tableCount; index++)
    {
        int record = 12 + (index * 16);
        if (Encoding.ASCII.GetString(sfnt, record, 4) != "cmap")
        {
            continue;
        }

        int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(sfnt.AsSpan(record + 8, 4));
        ushort count = BinaryPrimitives.ReadUInt16BigEndian(sfnt.AsSpan(offset + 2, 2));
        var records = new (ushort, ushort)[count];
        for (int entry = 0; entry < count; entry++)
        {
            int position = offset + 4 + (entry * 8);
            records[entry] = (
                BinaryPrimitives.ReadUInt16BigEndian(sfnt.AsSpan(position, 2)),
                BinaryPrimitives.ReadUInt16BigEndian(sfnt.AsSpan(position + 2, 2)));
        }

        return records;
    }

    throw new InvalidDataException("輸出缺少 cmap table。");
}

internal sealed record ProofCase(string Name, int[] Scalars, bool ExpectFormat4);

internal sealed record CaseResult(
    string Name,
    int[] Scalars,
    WebFontAsset Asset,
    string AssetPath,
    string[] EncodingRecords,
    bool HasFormat4,
    bool ExpectBrowserAccept,
    bool Truncate);

internal sealed record ProofReport(
    bool Loaded,
    bool Checked,
    bool FaceAccepted,
    int SampledGlyphs,
    int RenderedGlyphs);
