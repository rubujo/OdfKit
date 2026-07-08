# OOXML 視覺 Golden 矩陣

本文件記錄 OdfKit **ODF → OOXML** 轉換的實機視覺驗收範圍（Wave 3 Q-3）。
驗收策略為：以 **LibreOffice 匯出 PDF** 作為基準，**Microsoft Office 匯出 PDF** 作為候選結果，
透過像素差異百分比判定是否落在門檻內（預設 5%）。

場景清單見 [`tests/fixtures/ooxml-visual-golden/manifest.json`](../tests/fixtures/ooxml-visual-golden/manifest.json)。

## 執行方式

```powershell
# Windows + Office COM + LibreOffice 26.x + Python 依賴齊備時執行
pwsh eng/Test-OoxmlVisualGolden.ps1

# 強制要求完整環境（缺任一項則 exit 1）
pwsh eng/Test-OoxmlVisualGolden.ps1 -RequireEnvironment

# Office GUI / COM ODF 開啟煙霧驗收
pwsh eng/Test-OfficeGuiSmoke.ps1 -RequireEnvironment
```

## 環境需求

| 項目 | 變數 / 條件 | 說明 |
|------|-------------|------|
| 作業系統 | Windows | Word / Excel COM 僅支援 Windows |
| LibreOffice | `ODFKIT_SOFFICE_PATH` | 26.x soffice，用於 baseline PDF |
| Microsoft Office | COM ProgID | `Word.Application`、`Excel.Application` |
| Python | `ODFKIT_PDF_RENDERER_PYTHON` | 指向 `python.exe` |
| Python 套件 | — | `numpy`、`Pillow`、`pypdfium2` |

PDF 像素比對腳本：[`eng/scripts/PdfVisualDiff.py`](../eng/scripts/PdfVisualDiff.py)。

## 矩陣

| 場景 ID | 來源 | 轉換 | 基準 PDF | 候選 PDF | 測試 | 狀態 |
|---------|------|------|--------------|---------------|------|------|
| `odt-docx-word-pdf` | ODT | DOCX | LibreOffice | Word | `WordAndLibreOffice_RenderConvertedDocxToPdf` | ✅ 自動化（可選環境） |
| `ods-xlsx-excel-pdf` | ODS | XLSX | LibreOffice | Excel | `ExcelAndLibreOffice_RenderConvertedXlsxToPdf` | ✅ 自動化（可選環境） |

## Office GUI / COM 煙霧驗收

`eng/Test-OfficeGuiSmoke.ps1` 使用本機 Microsoft Office COM 開啟 repo 內代表性 ODF fixture：

| 場景 | 檔案 | 目標應用程式 | 驗收 |
|------|------|--------------|------|
| ODT 文字文件 | `complex-annual-report.odt` | Word | 可復原並讀取「年度報告」與「營運摘要」 |
| ODS 試算表 | `complex-financial-model.ods` | Excel | 可讀取 A1「月份」 |
| ODP 簡報 | `complex-business-deck.odp` | PowerPoint | 可讀取至少 2 張投影片 |

此 smoke test 驗證 Office 實機可載入代表性 ODF，但不取代 PDF pixel diff，
也不宣稱 Microsoft Office 與 LibreOffice 呈現像素級一致。

## 驗收邏輯

1. OdfKit 建立含代表性內容的 ODF 樣本。
2. 轉換為 OOXML（`OdfToDocxConverter` / `OdfToXlsxConverter`）。
3. 原始 ODF 與轉換後 OOXML 分別由 LO / Office 匯出 PDF。
4. `PdfVisualDiff.py` 計算像素差異百分比，須 ≤ `thresholdPercent`（5%）。

## 非目標

- 儲存庫內預先提交基準 PDF 二進位（目前採即時雙路徑渲染比對）
- macOS / Linux Office COM 驗收
- Codex Computer Use 或瀏覽器 PDF 截圖作為自動化門檻
- 圖表樣式、樞紐表等進階視覺保真

## 相關測試

| 類別 | 說明 |
|------|------|
| `OfficeInteropConversionTests` | 本矩陣的實機 PDF 視覺比對 |
| `OfficeGuiSmokeTests` | Office COM 開啟代表性 ODT / ODS / ODP 的煙霧驗收 |
| `OoxmlConversionTests` | OOXML 結構與語意來回讀寫（非視覺）；含 ODT↔DOCX 圖片／追蹤修訂與 ODS↔XLSX 樞紐表往返；`Manifest_DefinesExpectedScenarios` 驗證本矩陣 manifest 結構與場景清單完整性 |
