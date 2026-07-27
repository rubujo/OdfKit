# ODF 指令碼與巨集管理

`OdfKit.Extensions.Scripting` 是選用的受控擴充套件，負責列舉、新增、更新及移除
ODF 指令碼與 LibreOffice 文件巨集。管理、掃描與政策 API 不會執行巨集；選用的外部診斷
worker 會啟動 Python 或 LibreOffice 子處理程序，能力與限制見下文。

## 支援範圍

| 能力 | ODF 1.0～1.4 | Flat XML | 說明 |
|------|--------------|----------|------|
| `office:scripts`／`office:script` | ✅ | ✅ | 管理標準文字指令碼內容 |
| `office:event-listeners`／`script:event-listener` | ✅ | ✅ | 支援 `script:macro-name` 與 `xlink:href` |
| LibreOffice Basic | ✅ | ❌ | 管理 `Basic/script-lc.xml`、library metadata 與 module XML |
| LibreOffice Python | ✅ | ❌ | 管理 `Scripts/python/**/*.py` |
| Basic／Python 結構式語法診斷 | ✅ | ❌ | 不啟動指令碼；空診斷不保證編譯器接受 |
| Python AST 編譯診斷 | ✅ | ❌ | 以 `ast.parse` 的隔離 Python 子處理程序驗證，不執行來源 |
| LibreOffice Basic compiler probe | ✅ | ❌ | 真實啟動 LibreOffice，但 headless UNO 無完整 compile diagnostic API，因此成功時回報 `Indeterminate` |
| AMSI／企業掃描 provider | ✅ | ❌ | `IOdfScriptScanner` 可串接 AMSI、防毒或沙箱；各結果不與憑證信任混合 |
| 巨集能力政策 | ✅ | ❌ | 保守標示自動執行、檔案、網路、程序、UNO 與動態求值 |
| 巨集執行 | ❌ | ❌ | 擴充套件不載入執行階段；實際執行交由 LibreOffice 等 consumer |
| LibreOffice 巨集簽章 | ✅ | ❌ | XMLDSig／XAdES 寫入 `META-INF/macrosignatures.xml`；涵蓋 `Basic/` 與 `Scripts/` |
| 簽署者信任政策 | ✅ | ❌ | 系統鏈、自訂根、釘選、EKU、Subject／Issuer、離線撤銷快取與輪替時窗 |

`office:script` 與事件繫結是 ODF 標準層；LibreOffice Basic／Python 的封裝目錄則是
LibreOffice 相容 profile，不應解讀為其它 ODF consumer 一定會執行的通用格式。

## 使用方式

```csharp
using OdfKit.Extensions.Scripting;
using OdfKit.Text;

using TextDocument document = TextDocument.Create();
OdfScriptManager scripting = document.Scripting();

int scriptIndex = scripting.AddInlineScript(
    "example:language",
    "document.log('loaded')");

scripting.AddOrUpdateLibreOfficeBasicModule(
    "Standard",
    "Module1",
    "Sub Main\n    MsgBox \"Hello\"\nEnd Sub");

scripting.AddDocumentEventBinding(
    "dom:load",
    "ooo:script",
    "vnd.sun.star.script:Standard.Module1.Main?language=Basic&location=document",
    OdfScriptTargetKind.Uri);

document.Save("scripted.odt");
```

Python 文件巨集使用相對於 `Scripts/python/` 的路徑：

```csharp
scripting.AddOrUpdateLibreOfficePythonModule(
    "Automation/report.py",
    "def create_report():\n    return None");

IReadOnlyList<OdfPackageScriptDiagnostics> diagnostics =
    scripting.DiagnosePackageScripts();
```

掃描器與政策引擎是兩條獨立訊號；企業 provider 只需實作 `IOdfScriptScanner`：

```csharp
var scanners = new OdfScriptScannerPipeline(
    new IOdfScriptScanner[] { new OdfAmsiScriptScanner(), enterpriseSandbox });
IReadOnlyList<OdfPackageScriptScanReport> scans =
    await scripting.ScanPackageScriptsAsync(scanners, cancellationToken);

var policy = new OdfMacroSecurityPolicy();
policy.AllowedUnoServicePrefixes.Add("com.sun.star.text.");
OdfMacroPolicyResult policyResult = scripting.EvaluateMacroPolicy(policy);
```

Python 真實語法診斷需明確指定執行檔，worker 使用 `-I -S` 與 `ast.parse`，不匯入或執行來源：

```csharp
var compiler = new OdfScriptCompilerOptions
{
    PythonExecutablePath = @"C:\Python314\python.exe",
    Timeout = TimeSpan.FromSeconds(15)
};
OdfScriptCompilationResult compiled = await OdfExternalScriptCompiler.DiagnoseAsync(
    pythonSource,
    OdfScriptCompilerBackend.PythonAst,
    compiler,
    cancellationToken);
```

修改完成後可建立巨集簽章；簽章與信任是兩個不同步驟：

```csharp
using System.Security.Cryptography.X509Certificates;
using OdfKit.Core;

await scripting.SignLibreOfficeMacrosAsync(
    signingCertificate,
    new OdfSigningOptions { SignatureLevel = OdfSignatureLevel.XadesBes },
    cancellationToken);

var trust = new OdfMacroTrustPolicy { Mode = OdfMacroTrustMode.CustomRoot };
trust.CustomRoots.Add(enterpriseRootCertificate);
trust.RevocationMode = OdfMacroRevocationMode.OfflineCache;
trust.AllowedEnhancedKeyUsages.Add("1.3.6.1.5.5.7.3.3"); // Code signing
trust.AllowedSubjects.Add("CN=Contoso ODF Signing");
OdfMacroSignatureValidationResult result =
    await scripting.VerifyLibreOfficeMacroSignaturesAsync(trust, cancellationToken);

bool removed = scripting.RemoveLibreOfficeMacroSignatures();
```

## 安全邊界

- 管理、掃描、政策與結構診斷只處理資料；只有明確呼叫 `OdfExternalScriptCompiler` 才會啟動外部 worker。
- 所有 package 路徑都會經過核心 ZIP entry 路徑驗證。
- LibreOffice metadata XML 禁用外部解析器與實體展開，並限制最多約 4 MiB 字元。
- 修改 `content.xml` 或 package script 後，既有 `documentsignatures.xml` 與
  `macrosignatures.xml` 會失效，因此管理器會將其移除。
- 有效數位簽章只證明簽署者及內容完整性，不表示巨集安全，也不保證 consumer 會允許執行。
- ODF 1.4 Part 2 標準化的是文件簽章；非文件簽章屬 implementation-defined。此 API 因此明確
  命名為 LibreOffice 巨集簽章，不宣稱 `macrosignatures.xml` 是跨所有 ODF consumer 的標準格式。
- `System` 信任模式要求作業系統憑證鏈通過；`CustomRoot` 要求鏈終點與指定根憑證相同；
  `PinnedCertificate` 比對不含分隔符號的 SHA-256 憑證指紋。三者都先要求 XMLDSig 完整性有效。
- `RotatingCertificatePins` 可同時接受舊、新憑證並以 `ActiveFrom`／`ActiveUntil` 控制輪替；
  `AllowedSubjects`、`AllowedIssuers` 與 `AllowedEnhancedKeyUsages` 是額外 allowlist，不取代密碼學驗證。
- `OfflineCache` 使用作業系統已快取的撤銷資料並採 fail closed；缺少或過期的撤銷資料會回報
  `OdfMacroTrustFailure.Revocation`，不會偷偷改成線上查詢。
- `IsCodeSafetyEvaluated` 固定為 `false`。惡意程式碼掃描應由獨立的 AMSI／防毒或沙箱政策處理，
  不可從受信任憑證推導程式碼安全。
- AMSI 的 `Clean`、`NotDetected` 與 provider `Unavailable` 是不同狀態；pipeline 保留每個 provider
  的原始判定，不會把「未偵測」提升為「安全」。
- LibreOffice Basic 採逐常式延遲編譯；`XScriptProvider` 與 headless `XScript.invoke` 不提供等同
  Basic IDE「Compile」按鈕的可靠診斷物件。安全 probe 因此只回報 `Indeterminate`，不得當成
  compiler acceptance。處理程序隔離也不等於 OS 沙箱。
- 若只需要移除主動內容，仍應使用核心 `SanitizeMacros()`，無須參照此擴充套件。

## 版本與相容性

ODF 1.0～1.4 使用相同的 `office`、`script` 與 `xlink` 命名空間 URI；管理器會從
`office:version` 偵測版本，未知版本不會被當作已支援版本修改。Flat XML 可儲存標準
`office:script`，但沒有 ZIP package entry，因此拒絕 Basic／Python package profile API。

## LibreOffice 實機證據

`LibreOfficeHeadlessExecutesManagedDocumentMacros` 會以 OdfKit 分別建立 ODF 1.0～1.4、含
已簽署 Basic 與 Python 文件巨集的 ODT，再透過隔離的 LibreOffice UNO headless profile 分別呼叫
兩個文件 script URI，並核對巨集實際寫出的標記檔內容。2026-07-22 已以 LibreOffice
Portable 26.2.4.2 在 Windows 通過全部五個版本；此證據代表目前產生的 LibreOffice package
profile 可被該版本載入及執行，不代表 OdfKit 本身提供巨集 runtime，也不把未知巨集判定為安全。

`ExternalCompilerWorkersUsePythonAstAndProbeLibreOfficeBasic` 另以同一 LibreOffice 26.2.4.2
驗證 Python AST 的 Valid／Invalid 判定，以及 Basic 安全 probe 必須維持 `Indeterminate`。
Microsoft Word COM 實機測試則強制 `AutomationSecurity = ForceDisable`，確認 Word 可開啟含
LibreOffice Basic／Python package entries 的 ODF 1.0～1.4 文件，且不會產生巨集執行標記。

一般測試不會主動執行文件巨集。只有明確設定
`ODFKIT_RUN_LIBREOFFICE_INTEROP=1` 且提供 `ODFKIT_SOFFICE_PATH` 時，專用互通測試才會建立
最低巨集安全層級的暫時 LibreOffice profile；測試結束後會刪除 profile 與測試文件。

## 規格與平台參考

- [OASIS OpenDocument 1.4 Part 2：Digital Signatures](https://docs.oasis-open.org/office/OpenDocument/v1.4/part2-packages/OpenDocument-v1.4-os-part2-packages.html)
- [LibreOffice：Macro Security](https://help.libreoffice.org/latest/en-GB/text/shared/optionen/macrosecurity_sl.html)
- [LibreOffice：Digital Signatures](https://help.libreoffice.org/latest/en-US/text/shared/guide/digital_signatures.html)
- [Microsoft：Antimalware Scan Interface](https://learn.microsoft.com/en-us/windows/win32/amsi/antimalware-scan-interface-portal)
- [Microsoft：AmsiScanBuffer](https://learn.microsoft.com/en-us/windows/win32/api/amsi/nf-amsi-amsiscanbuffer)
- [Python：`ast` — Abstract syntax trees](https://docs.python.org/3/library/ast.html)
- [LibreOffice SDK：`XScriptProvider`](https://api.libreoffice.org/docs/idl/ref/interfacecom_1_1sun_1_1star_1_1script_1_1provider_1_1XScriptProvider.html)
