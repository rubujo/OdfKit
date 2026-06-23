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

## 非目標（Wave 3 尚未涵蓋）

- 像素級視覺 diff（見 [ooxml-visual-golden-matrix.md](ooxml-visual-golden-matrix.md)）
- Microsoft Word / Excel 開啟驗收
- 全 24 種 extension 逐一 LO 轉換（目前聚焦四主格式與追蹤修訂）
- 動畫播放、圖表樣式、樞紐表重算等編輯器行為驗證

## 相關測試

| 類別 | 說明 |
|------|------|
| `LibreOfficeInteropTests` | 本矩陣的實機 headless 測試 |
| `TrackedChangesInteropTests` | 追蹤修訂語意（非必須 LO） |
| `LoExtInteropTests` | `loext:decorative` 載入映射 |
| `LibreOfficeRenderer*Tests` | `Extensions.Rendering` 可替換 backend |