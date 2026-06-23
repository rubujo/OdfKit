# LibreOffice 互通性矩陣

本文件記錄 OdfKit 與 **LibreOffice 26.x** 的實機 headless 互通驗收範圍。
矩陣以 `OdfKit.Tests/LibreOfficeInteropTests.cs` 與 `eng/Test-LibreOfficeInterop.ps1` 為準。

## 執行方式

```powershell
# 有 LibreOffice 26.x 時執行互通測試；找不到則略過（exit 0）
pwsh eng/Test-LibreOfficeInterop.ps1

# CI 或本機強制要求 LibreOffice
pwsh eng/Test-LibreOfficeInterop.ps1 -RequireLibreOffice

# 指定 portable 安裝路徑
$env:ODFKIT_SOFFICE_PATH = "D:\Tools\LibreOffice\program\soffice.com"
pwsh eng/Test-LibreOfficeInterop.ps1
```

環境變數（擇一）：

| 變數 | 用途 |
|------|------|
| `ODFKIT_SOFFICE_PATH` | soffice 可執行檔或安裝目錄（優先） |
| `LIBREOFFICE_PATH` | 同上，相容別名 |

需求：

- LibreOffice **26.x**（`soffice --version` 輸出含 `LibreOffice 26.`）
- 排除 `MockSoffice` 測試替身

## 矩陣

| 測試 | 來源格式 | OdfKit 建立內容 | LibreOffice 轉換 | 驗收方式 | 狀態 |
|------|----------|-----------------|------------------|----------|------|
| `LibreOfficeHeadless_LoadsGeneratedDocuments` | ODT | 標題、段落、互通標記 | `txt` | 轉出文字含 `OdfKit-LibreOffice-26-Interop-Marker` | ✅ |
| 同上 | ODS | 嵌入圖表（條形圖、標題、圖例） | `xlsx` | 轉出 XLSX 非空 | ✅ |
| 同上 | ODP | 進場動畫（fade-in） | `fodp` | 轉出 XML 含 `ooo-entrance-fade-in` | ✅ |
| 同上 | ODG | 文字方塊互通標記 | `fodg` | 轉出 XML 含 `OdfKit-LibreOffice-26-Interop-Marker` | ✅ |
| `LibreOfficeHeadless_LoadsTrackedChangesOdt` | ODT | `text:tracked-changes` 段落與表格 | `txt` / `odt` | 標記保留；可 accept 修訂 | ✅ |
| `LibreOfficeHeadless_LoadsTrackedChangesOds` | ODS | `table:tracked-changes` 公式變更 | `ods` | 標記與修訂節點保留 | ✅ |
| `LibreOfficeHeadless_LoadsTemplateVariantDocuments` | OTT | 母片頁面、互通標記 | `txt` | 轉出文字含 `OdfKit-LibreOffice-Template-Interop-Marker` | ✅ |
| 同上 | OTS | 工作表互通標記 | `fods` | 轉出 Flat XML 含互通標記 | ✅ |
| 同上 | OTP | 標題 placeholder、文字方塊互通標記 | `fodp` | 轉出 Flat XML 含互通標記 | ✅ |
| 同上 | OTG | 文字方塊互通標記 | `fodg` | 轉出 Flat XML 含互通標記 | ✅ |
| `LibreOfficeHeadless_LoadsNativeFlatXmlDocuments` | FODT（原生產生，非由 ZIP 轉換） | 標題、段落、互通標記 | `txt` | 轉出文字含 `OdfKit-LibreOffice-NativeFlat-Interop-Marker` | ✅ |
| 同上 | FODS（原生產生） | 工作表互通標記 | `xlsx` | 轉出 XLSX 非空 | ✅ |
| 同上 | FODP（原生產生） | 文字方塊互通標記 | `odp` | 轉出 ODP 含互通標記 | ✅ |
| 同上 | FODG（原生產生） | 文字方塊互通標記 | `png` | 轉出 PNG 非空 | ✅ |
| `LibreOfficeHeadless_LoadsMasterDocument` | ODM | 段落、子文件參照、互通標記 | `txt` / `odm` | LibreOffice 識別為 Writer master document（`writerglobal8`）；轉出文字含互通標記；往返後子文件參照保留 | ✅ |
| `LibreOfficeHeadless_LoadsWebTemplateDocument` | OTH | 標題、段落、互通標記 | `txt` / `odt` | LibreOffice 識別為 Writer/Web document（`writerweb8_writer`）；轉出文字含互通標記；轉出 ODT 內容保留 | ✅ |

## 已知上游限制（非 OdfKit 缺陷）

獨立（非嵌入 ODS/ODT/ODP）的 ODC／OTC／FODC 圖表文件，實測確認 LibreOffice 26.2.1 不接受
為可直接開啟的主文件：

| 格式 | 實測結果 |
|------|----------|
| `.odc` | `soffice --headless --convert-to txt` 回報 `source file could not be loaded` |
| `.otc` | 同上，回報 `source file could not be loaded` |
| `.fodc` | 未回報錯誤，但被誤判為「Writer document」，僅原樣回顯來源 XML，並非真正剖析為圖表——不構成有效互通 |

ODF Chart 設計上即為僅可嵌入 ODS/ODT/ODP 內的子文件類型，並非獨立可開啟的主文件格式。
改以封裝結構與 schema 層級的精確驗證取代真機驗證（見
`LibreOfficeInteropTests.OdfChartDocument_PackageStructureMatchesOdf14Schema`），並以既有
「圖表嵌入 ODS 後由 LibreOffice 開啟」驗收（`LibreOfficeHeadless_LoadsGeneratedDocuments` 對
含圖表 ODS 的 `xlsx` 轉換）佐證嵌入式圖表的真實互通性。

獨立 OTF／FDF 公式範本與 Flat 變體同樣不被 LibreOffice 26.2.1 接受為可直接開啟的主文件：

| 格式 | 實測結果 |
|------|----------|
| `.odf` | ✅ 真機支援——LibreOffice 識別為「Math document」，使用 `math8` 篩選器；MathML 內容（含 `mrow` 群組）往返保留 |
| `.otf` | ❌ `soffice --headless --convert-to odf` 回報 `source file could not be loaded` |
| `.fdf` | ❌ 未回報錯誤，但被誤判為「Calc document」，以 `calc_png_Export` 篩選器產生與公式內容完全無關的輸出 |

ODF Formula 是唯一一個獨立 ZIP 主格式（`.odf`）確實有真機支援的次要格式（不同於 Chart／Image），
但其 Template／Flat 變體仍與 Chart／Image 的變體一樣不受支援。見
`LibreOfficeInteropTests.LibreOfficeHeadless_LoadsFormulaDocument`（.odf 真機驗收）與
`OdfFormulaVariantDocument_PackageStructureMatchesOdf14Schema`（.otf／.fdf 封裝結構驗證）。

獨立 ODI／OTI／FODI 影像文件同樣不被 LibreOffice 26.2.1 與 Microsoft Office 365 接受為可直接
開啟的主文件：

| 格式 | 實測結果 |
|------|----------|
| `.odi` | ❌ `soffice --headless --convert-to png` 回報 `source file could not be loaded` |
| `.oti` | ❌ 同上，回報 `source file could not be loaded` |
| `.fodi` | ❌ 未回報錯誤，但被誤判為「Writer document」，以 `writer_png_Export` 篩選器產生與影像內容完全無關的輸出 |

`.fodi` 的誤判模式與 `.fodc`（誤判為 Writer document）、`.fdf`（誤判為 Calc document）一致——
這修正了既有 `OdfImageDocument_PackageStructureMatchesOdf14Schema` 註解中原先聲稱「ODI／OTI／
FODI 一律回報 source file could not be loaded」的不準確描述（該描述對 ODI／OTI 成立，但對
FODI 並不成立）。改以封裝結構與 schema 層級的精確驗證取代真機驗證（該測試已擴充涵蓋
ODI／OTI／FODI 三者）。

## 非目標（Wave 3 尚未涵蓋）

- 像素級視覺 diff（見 [ooxml-visual-golden-matrix.md](ooxml-visual-golden-matrix.md)）
- Microsoft Word / Excel 開啟驗收
- 剩餘 1 種次要格式 extension（.odb）逐一 LO 轉換驗收
  （Batch 1、Batch 2 已涵蓋四主格式 template／flat 變體與 .odm／.oth；Batch 3 已涵蓋 Chart
  全族；Batch 4 已涵蓋 Formula 全族並確認 .odf 真機支援、.otf／.fdf 的上游限制；Batch 5 已涵蓋
  Image 全族並確認 .odi／.oti／.fodi 的上游限制；其餘為後續 Batch 工作）
- 動畫播放、圖表樣式、樞紐表重算等編輯器行為驗證

## 相關測試

| 類別 | 說明 |
|------|------|
| `LibreOfficeInteropTests` | 本矩陣的實機 headless 測試 |
| `TrackedChangesInteropTests` | 追蹤修訂語意（非必須 LO） |
| `LoExtInteropTests` | `loext:decorative` 載入映射 |
| `LibreOfficeRenderer*Tests` | `Extensions.Rendering` 可替換 backend |