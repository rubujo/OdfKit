using System.Net;
using System.Text.Json;
using OdfKit.Styles;
using OdfKit.WebFonts.Hosting.AspNetCore;

string? assetDirectory = Environment.GetEnvironmentVariable("ODFKIT_WEBFONT_SMOKE_ASSETS");
if (string.IsNullOrWhiteSpace(assetDirectory))
{
    Console.Error.WriteLine("ODFKIT_WEBFONT_SMOKE_ASSETS is required.");
    return;
}

string fontPath = Path.Combine(assetDirectory, "smoke.woff2");
string metadataPath = Path.Combine(assetDirectory, "metadata.json");
if (!File.Exists(fontPath) || !File.Exists(metadataPath))
{
    Console.Error.WriteLine("Run eng/Test-WebFontSmoke.ps1 to prepare the smoke-test assets.");
    return;
}

byte[] fontBytes = await File.ReadAllBytesAsync(fontPath);
using JsonDocument metadata = JsonDocument.Parse(await File.ReadAllTextAsync(metadataPath));
JsonElement root = metadata.RootElement;

string sourceFile = root.GetProperty("sourceFile").GetString() ?? string.Empty;
long sourceBytes = root.GetProperty("sourceBytes").GetInt64();
long subsetBytes = root.GetProperty("subsetBytes").GetInt64();
string sourceSha256 = root.GetProperty("sourceSha256").GetString() ?? string.Empty;
string subsetSha256 = root.GetProperty("subsetSha256").GetString() ?? string.Empty;
string internationalDirectory = Path.Combine(assetDirectory, "international");
string internationalMetadataPath = Path.Combine(internationalDirectory, "international.json");
List<InternationalSmokeCase> internationalCases = LoadInternationalCases(internationalMetadataPath);
Dictionary<string, InternationalFontAsset> internationalAssets = internationalCases
    .SelectMany(smokeCase => smokeCase.Outputs)
    .ToDictionary(output => output.FileName, StringComparer.OrdinalIgnoreCase);
var testCases = root.GetProperty("testCases")
    .EnumerateArray()
    .Select(item => new
    {
        CodePoint = item.GetProperty("codePoint").GetString() ?? string.Empty,
        Text = item.GetProperty("text").GetString() ?? string.Empty,
        UnicodePlane = item.GetProperty("unicodePlane").GetInt32(),
        Label = item.GetProperty("label").GetString() ?? string.Empty,
        CnsCode = item.GetProperty("cnsCode").ValueKind == JsonValueKind.Null
            ? null
            : item.GetProperty("cnsCode").GetString()
    })
    .ToList();
string codePoints = string.Join(", ", testCases.Select(testCase => testCase.CodePoint));
string sampleText = string.Concat(testCases.Select(testCase => testCase.Text));
string planeCards = string.Join(
    Environment.NewLine,
    testCases
        .GroupBy(testCase => testCase.UnicodePlane)
        .Select(group =>
        {
            string glyphs = string.Join(" ", group.Select(testCase => WebUtility.HtmlEncode(testCase.Text)));
            string details = string.Join(
                " · ",
                group.Select(testCase =>
                {
                    string cns = testCase.CnsCode is null ? string.Empty : $" / CNS {testCase.CnsCode}";
                    return $"{WebUtility.HtmlEncode(testCase.CodePoint)}{WebUtility.HtmlEncode(cns)}";
                }));
            string labels = string.Join("、", group.Select(testCase => WebUtility.HtmlEncode(testCase.Label)));
            return $"""
                <section class="plane-card">
                  <div class="plane-title">Unicode Plane {group.Key}</div>
                  <div class="plane-glyphs">{glyphs}</div>
                  <div class="plane-details">{details}</div>
                  <div class="plane-labels">{labels}</div>
                </section>
                """;
        }));

var fontContext = new OdfFontContext();
using IDisposable mapping = fontContext.RegisterSupplementaryPlaneFontMapping(
    "TW-Kai",
    new Dictionary<int, string>
    {
        [1] = "Noto Sans TC Web P1",
        [2] = "Noto Sans TC Web P2",
        [3] = "Noto Sans TC Web P3"
    });
List<(string Text, string FontName)> segments = fontContext.SegmentText(sampleText, "TW-Kai");
string segmentSummary = string.Join(
    " → ",
    segments.Select(segment => $"{segment.Text} [{segment.FontName}]"));

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddOdfWebFonts(options =>
{
    options.AssetRootPath = internationalDirectory;
});
WebApplication app = builder.Build();

app.MapGet("/font.woff2", () => Results.File(fontBytes, "font/woff2"));
app.MapOdfWebFonts();
app.MapGet(
    "/health",
    () => Results.Json(new
    {
        status = "ok",
        signature = "wOF2",
        sourceBytes,
        subsetBytes,
        codePoints,
        testCases,
        segments = segments.Select(segment => new { segment.Text, segment.FontName })
    }));

app.MapGet(
    "/",
    () =>
    {
        string page = $$"""
            <!doctype html>
            <html lang="zh-Hant-TW">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>OdfKit WebFont 最小驗證</title>
              <style>
                @font-face {
                  font-family: "OdfKit Smoke";
                  src: url("/font.woff2") format("woff2");
                  font-display: block;
                  unicode-range: {{codePoints}};
                }
                :root { color-scheme: dark; font-family: system-ui, sans-serif; }
                body {
                  margin: 0;
                  min-height: 100vh;
                  display: grid;
                  place-items: center;
                  background: radial-gradient(circle at top, #16344b, #08131d 60%);
                  color: #edf7ff;
                }
                main {
                  width: min(880px, calc(100vw - 48px));
                  padding: 38px;
                  border: 1px solid #37617b;
                  border-radius: 24px;
                  background: #0c1c28e8;
                  box-shadow: 0 22px 70px #0008;
                }
                h1 { margin: 0 0 8px; font-size: 28px; }
                .lead { margin: 0 0 28px; color: #a9c9dc; }
                .plane-grid {
                  margin: 20px 0;
                  display: grid;
                  grid-template-columns: repeat(2, minmax(0, 1fr));
                  gap: 14px;
                }
                .plane-card {
                  min-width: 0;
                  padding: 18px;
                  border: 1px solid #31536a;
                  border-radius: 15px;
                  background: #102735;
                }
                .plane-title { color: #8cc6e8; font-weight: 800; }
                .plane-glyphs {
                  margin: 10px 0;
                  color: #17232b;
                  font: 42px/1.3 "OdfKit Smoke", sans-serif;
                  overflow-wrap: anywhere;
                  padding: 12px;
                  border-radius: 10px;
                  background: #f7f4ed;
                }
                .plane-details { color: #d5e6ef; font: 12px/1.6 ui-monospace, monospace; }
                .plane-labels { margin-top: 5px; color: #86a8bb; font-size: 12px; }
                @media (max-width: 760px) {
                  .plane-grid { grid-template-columns: 1fr; }
                }
                .status {
                  display: inline-flex;
                  gap: 9px;
                  align-items: center;
                  padding: 8px 13px;
                  border-radius: 999px;
                  background: #163546;
                  color: #d8edfa;
                  font-weight: 700;
                }
                .status.pass { background: #124c3a; color: #b9ffe0; }
                .dot { width: 10px; height: 10px; border-radius: 50%; background: #f0b14a; }
                .pass .dot { background: #4cf0aa; box-shadow: 0 0 12px #4cf0aa; }
                dl { display: grid; grid-template-columns: 170px 1fr; gap: 12px 18px; margin: 28px 0 0; }
                dt { color: #88b2ca; }
                dd { margin: 0; overflow-wrap: anywhere; font-family: ui-monospace, monospace; }
                code { color: #bfe8ff; }
              </style>
            </head>
            <body>
              <main>
                <h1>OdfKit WebFont 最小驗證</h1>
                <p class="lead">真實 TTF → 字形子集 → WOFF2 → 瀏覽器載入</p>
                <div id="status" class="status"><span class="dot"></span><span>正在載入字型…</span></div>
                <div class="plane-grid" id="sample">{{planeCards}}</div>
                <dl>
                  <dt>驗證字元</dt><dd>{{WebUtility.HtmlEncode(codePoints)}}</dd>
                  <dt>來源字型</dt><dd>{{WebUtility.HtmlEncode(sourceFile)}}（{{sourceBytes:N0}} bytes）</dd>
                  <dt>WOFF2 子集</dt><dd>{{subsetBytes:N0}} bytes</dd>
                  <dt>來源 SHA-256</dt><dd>{{WebUtility.HtmlEncode(sourceSha256)}}</dd>
                  <dt>子集 SHA-256</dt><dd>{{WebUtility.HtmlEncode(subsetSha256)}}</dd>
                  <dt>OdfKit 分段</dt><dd>{{WebUtility.HtmlEncode(segmentSummary)}}</dd>
                </dl>
              </main>
              <script>
                const status = document.querySelector("#status");
                document.fonts.load('42px "OdfKit Smoke"', {{JsonSerializer.Serialize(sampleText)}})
                  .then(fonts => {
                    if (fonts.length === 0) throw new Error("FontFaceSet 未回報已載入字型");
                    status.classList.add("pass");
                    status.lastElementChild.textContent = "PASS：瀏覽器已載入 WOFF2";
                    document.body.dataset.fontReady = "true";
                  })
                  .catch(error => {
                    status.lastElementChild.textContent = "FAIL：" + error.message;
                    document.body.dataset.fontReady = "false";
                  });
              </script>
            </body>
            </html>
            """;

        return Results.Content(page, "text/html; charset=utf-8");
    });

app.MapGet(
    "/international/health",
    () => Results.Json(new
    {
        status = internationalCases.Count == 6 ? "ok" : "incomplete",
        caseCount = internationalCases.Count,
        assetCount = internationalAssets.Count,
        cases = internationalCases.Select(smokeCase => new
        {
            smokeCase.Id,
            smokeCase.CodePoints,
            smokeCase.FaceIndex,
            signatures = smokeCase.Outputs.Select(output => output.Signature)
        })
    }));

app.MapGet(
    "/international",
    () =>
    {
        if (internationalCases.Count == 0)
        {
            return Results.Problem("Run prepare_international.py before opening this page.");
        }

        string fontFaces = string.Join(
            Environment.NewLine,
            internationalCases.Select(smokeCase =>
            {
                InternationalFontAsset woff2 = smokeCase.Outputs.Single(output => output.Signature == "wOF2");
                string unicodeRange = string.Join(
                    ", ",
                    smokeCase.CodePoints.Distinct(StringComparer.OrdinalIgnoreCase));
                return $$"""
                    @font-face {
                      font-family: "{{smokeCase.FontFamily}}";
                      src: url("/_odf-fonts/{{woff2.Sha256}}/{{woff2.FileName}}") format("woff2");
                      font-display: block;
                      unicode-range: {{unicodeRange}};
                    }
                    """;
            }));
        string cards = string.Join(
            Environment.NewLine,
            internationalCases.Select(smokeCase =>
            {
                string outputs = string.Join(
                    " · ",
                    smokeCase.Outputs.Select(output =>
                        $"{WebUtility.HtmlEncode(output.Signature)} {output.Bytes:N0} bytes"));
                string ivsComparison = smokeCase.Id == "japan-ivs"
                    ? $"""
                      <div class="ivs-grid">
                        <div><span>基底字</span><b style="font-family: '{smokeCase.FontFamily}'">{WebUtility.HtmlEncode(smokeCase.IvsBaseText)}</b></div>
                        <div><span>IVS 字形</span><b style="font-family: '{smokeCase.FontFamily}'">{WebUtility.HtmlEncode(smokeCase.Text)}</b></div>
                      </div>
                      """
                    : string.Empty;
                return $$"""
                    <article class="case-card" data-case="{{smokeCase.Id}}">
                      <header><span class="case-pass">待驗證</span><h2>{{WebUtility.HtmlEncode(smokeCase.Title)}}</h2></header>
                      <div class="sample" lang="{{smokeCase.Language}}" dir="{{smokeCase.Direction}}"
                           style="font-family: '{{smokeCase.FontFamily}}'">{{WebUtility.HtmlEncode(smokeCase.Text)}}</div>
                      {{ivsComparison}}
                      <p>{{WebUtility.HtmlEncode(smokeCase.Description)}}</p>
                      <code>{{WebUtility.HtmlEncode(string.Join(" ", smokeCase.CodePoints))}}</code>
                      <small>{{WebUtility.HtmlEncode(outputs)}}</small>
                    </article>
                    """;
            }));
        string browserCases = JsonSerializer.Serialize(
            internationalCases.Select(smokeCase => new
            {
                id = smokeCase.Id,
                family = smokeCase.FontFamily,
                text = smokeCase.Text,
                ivsBaseText = smokeCase.IvsBaseText
            }));
        string page = $$"""
            <!doctype html>
            <html lang="zh-Hant-TW">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>OdfKit 多國罕用字 WebFont 驗證</title>
              <style>
                {{fontFaces}}
                :root { color-scheme: dark; font-family: system-ui, sans-serif; }
                * { box-sizing: border-box; }
                body { margin: 0; background: #07131c; color: #ecf7ff; }
                main { width: min(1180px, calc(100vw - 40px)); margin: 32px auto 60px; }
                .hero {
                  padding: 30px 34px; border: 1px solid #31566e; border-radius: 22px;
                  background: radial-gradient(circle at top right, #174e5b, #0c2230 58%);
                  box-shadow: 0 20px 55px #0007;
                }
                h1 { margin: 0 0 8px; font-size: clamp(26px, 4vw, 42px); }
                .lead { color: #a9cbd9; margin: 0 0 20px; }
                #status {
                  display: inline-flex; gap: 9px; align-items: center; padding: 8px 14px;
                  border-radius: 999px; background: #5c4217; font-weight: 800;
                }
                #status.pass { background: #11513d; color: #b9ffe0; }
                .dot { width: 10px; height: 10px; border-radius: 50%; background: #ffbd50; }
                .pass .dot { background: #4cf0aa; box-shadow: 0 0 12px #4cf0aa; }
                .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; margin-top: 18px; }
                .case-card { padding: 22px; border: 1px solid #2f5369; border-radius: 17px; background: #0d202c; }
                .case-card:last-child { grid-column: 1 / -1; }
                .case-card header { display: flex; align-items: center; gap: 10px; }
                .case-card h2 { margin: 0; font-size: 18px; }
                .case-pass { padding: 4px 8px; border-radius: 999px; background: #54401c; color: #ffd88b; font-size: 11px; }
                .case-card.verified .case-pass { background: #124b39; color: #aaffd8; }
                .sample {
                  min-height: 102px; margin: 16px 0 12px; padding: 15px; display: grid;
                  place-items: center; overflow-wrap: anywhere; border-radius: 12px;
                  background: #f8f5ed; color: #14232c; font-size: clamp(42px, 6vw, 68px);
                  line-height: 1.25;
                }
                p { color: #b8d2de; }
                code, small { display: block; color: #90bfd4; overflow-wrap: anywhere; }
                small { margin-top: 8px; color: #6f9bad; }
                .ivs-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
                .ivs-grid div { padding: 10px; border: 1px solid #29485b; border-radius: 10px; text-align: center; }
                .ivs-grid span { display: block; color: #86b1c5; font-size: 12px; }
                .ivs-grid b { display: block; margin-top: 4px; font-size: 54px; }
                @media (max-width: 760px) { .grid { grid-template-columns: 1fr; } .case-card:last-child { grid-column: auto; } }
              </style>
            </head>
            <body>
              <main>
                <section class="hero">
                  <h1>多國罕用字 WebFont 驗證</h1>
                  <p class="lead">實際 TTC／TTF → layout closure → WOFF2／WOFF／TTF／OTF → 瀏覽器塑形</p>
                  <div id="status"><span class="dot"></span><span>正在驗證 {{internationalCases.Count}} 組字型…</span></div>
                </section>
                <section class="grid">{{cards}}</section>
              </main>
              <canvas id="proof" width="260" height="130" hidden></canvas>
              <script>
                const cases = {{browserCases}};
                const status = document.querySelector("#status");
                const rasterHash = (text, family) => {
                  const canvas = document.querySelector("#proof");
                  const context = canvas.getContext("2d", { willReadFrequently: true });
                  context.clearRect(0, 0, canvas.width, canvas.height);
                  context.fillStyle = "#000";
                  context.font = `78px "${family}"`;
                  context.textBaseline = "top";
                  context.fillText(text, 8, 8);
                  const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
                  let hash = 2166136261;
                  for (let index = 3; index < pixels.length; index += 4) {
                    hash ^= pixels[index];
                    hash = Math.imul(hash, 16777619);
                  }
                  return hash >>> 0;
                };
                Promise.all(cases.map(async testCase => {
                  const fonts = await document.fonts.load(`68px "${testCase.family}"`, testCase.text);
                  if (fonts.length === 0 || !document.fonts.check(`68px "${testCase.family}"`, testCase.text)) {
                    throw new Error(`${testCase.id} 未載入`);
                  }
                  document.querySelector(`[data-case="${testCase.id}"]`).classList.add("verified");
                  document.querySelector(`[data-case="${testCase.id}"] .case-pass`).textContent = "PASS";
                  return testCase.id;
                })).then(loadedCases => {
                  const ivs = cases.find(testCase => testCase.id === "japan-ivs");
                  const baseHash = rasterHash(ivs.ivsBaseText, ivs.family);
                  const variantHash = rasterHash(ivs.text, ivs.family);
                  if (baseHash === variantHash) throw new Error("IVS 與基底字的像素相同");
                  window.__odfKitInternationalProof = { loadedCases, baseHash, variantHash };
                  status.classList.add("pass");
                  status.lastElementChild.textContent = `PASS：${cases.length} 組字型已載入，IVS 像素不同`;
                  document.body.dataset.internationalReady = "true";
                }).catch(error => {
                  status.lastElementChild.textContent = "FAIL：" + error.message;
                  document.body.dataset.internationalReady = "false";
                });
              </script>
            </body>
            </html>
            """;

        return Results.Content(page, "text/html; charset=utf-8");
    });

await app.RunAsync();

static List<InternationalSmokeCase> LoadInternationalCases(string path)
{
    if (!File.Exists(path))
    {
        return [];
    }

    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
    return document.RootElement.GetProperty("cases")
        .EnumerateArray()
        .Select(item => new InternationalSmokeCase(
            item.GetProperty("id").GetString() ?? string.Empty,
            item.GetProperty("title").GetString() ?? string.Empty,
            item.GetProperty("language").GetString() ?? string.Empty,
            item.GetProperty("direction").GetString() ?? "ltr",
            item.GetProperty("fontFamily").GetString() ?? string.Empty,
            item.GetProperty("text").GetString() ?? string.Empty,
            item.GetProperty("codePoints").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToList(),
            item.GetProperty("description").GetString() ?? string.Empty,
            item.GetProperty("faceIndex").ValueKind == JsonValueKind.Null ? null : item.GetProperty("faceIndex").GetInt32(),
            item.GetProperty("ivsBaseText").ValueKind == JsonValueKind.Null ? null : item.GetProperty("ivsBaseText").GetString(),
            item.GetProperty("outputs").EnumerateArray().Select(output => new InternationalFontAsset(
                output.GetProperty("fileName").GetString() ?? string.Empty,
                output.GetProperty("bytes").GetInt64(),
                output.GetProperty("sha256").GetString() ?? string.Empty,
                output.GetProperty("signature").GetString() ?? string.Empty)).ToList()))
        .ToList();
}

internal sealed record InternationalSmokeCase(
    string Id,
    string Title,
    string Language,
    string Direction,
    string FontFamily,
    string Text,
    IReadOnlyList<string> CodePoints,
    string Description,
    int? FaceIndex,
    string? IvsBaseText,
    IReadOnlyList<InternationalFontAsset> Outputs);

internal sealed record InternationalFontAsset(string FileName, long Bytes, string Sha256, string Signature);
