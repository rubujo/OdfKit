# ODF 指令碼與巨集管理

`OdfKit.Extensions.Scripting` 是選用的純受控擴充套件，負責列舉、新增、更新及移除
ODF 指令碼與 LibreOffice 文件巨集。它不會編譯、驗證或執行巨集原始碼。

## 支援範圍

| 能力 | ODF 1.0～1.4 | Flat XML | 說明 |
|------|--------------|----------|------|
| `office:scripts`／`office:script` | ✅ | ✅ | 管理標準文字指令碼內容 |
| `office:event-listeners`／`script:event-listener` | ✅ | ✅ | 支援 `script:macro-name` 與 `xlink:href` |
| LibreOffice Basic | ✅ | ❌ | 管理 `Basic/script-lc.xml`、library metadata 與 module XML |
| LibreOffice Python | ✅ | ❌ | 管理 `Scripts/python/**/*.py` |
| 內建巨集執行或語法驗證 | ❌ | ❌ | 擴充套件不載入執行階段；實際執行交由 LibreOffice 等 consumer |
| 巨集重新簽章 | ❌ | ❌ | 任何修改都會移除已失效的文件與巨集簽章 |

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
```

## 安全邊界

- API 只處理資料，不載入 LibreOffice、UNO 或任何指令碼 runtime。
- 所有 package 路徑都會經過核心 ZIP entry 路徑驗證。
- LibreOffice metadata XML 禁用外部解析器與實體展開，並限制最多約 4 MiB 字元。
- 修改 `content.xml` 或 package script 後，既有 `documentsignatures.xml` 與
  `macrosignatures.xml` 會失效，因此管理器會將其移除。
- 有效數位簽章只證明簽署者及內容完整性，不表示巨集安全，也不保證 consumer 會允許執行。
- 若只需要移除主動內容，仍應使用核心 `SanitizeMacros()`，無須參照此擴充套件。

## 版本與相容性

ODF 1.0～1.4 使用相同的 `office`、`script` 與 `xlink` 命名空間 URI；管理器會從
`office:version` 偵測版本，未知版本不會被當作已支援版本修改。Flat XML 可儲存標準
`office:script`，但沒有 ZIP package entry，因此拒絕 Basic／Python package profile API。

## LibreOffice 實機證據

`LibreOfficeHeadless_ExecutesManagedDocumentMacros` 會以 OdfKit 分別建立 ODF 1.0～1.4、含
Basic 與 Python 文件巨集的 ODT，再透過隔離的 LibreOffice UNO headless profile 分別呼叫
兩個文件 script URI，並核對巨集實際寫出的標記檔內容。2026-07-22 已以 LibreOffice
Portable 26.2.4.2 在 Windows 通過全部五個版本；此證據代表目前產生的 LibreOffice package
profile 可被該版本載入及執行，不代表 OdfKit 本身提供巨集 runtime，也不把未知巨集判定為安全。

一般測試不會主動執行文件巨集。只有明確設定
`ODFKIT_RUN_LIBREOFFICE_INTEROP=1` 且提供 `ODFKIT_SOFFICE_PATH` 時，專用互通測試才會建立
最低巨集安全層級的暫時 LibreOffice profile；測試結束後會刪除 profile 與測試文件。
