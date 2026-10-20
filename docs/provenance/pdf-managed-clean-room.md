# PDF managed 引擎 Clean-room 來源紀錄

> 建立日期：2026-10-20
>
> 適用範圍：`OdfKit.Extensions.Pdf/Internal` 的 PDF 寫入器、PDF 讀取器、標準安全處理常式、
> 增量更新、sfnt／CFF 字型子集、PNG／JPEG／GIF／BMP 影像解碼與版面引擎。

## 為什麼自行實作

PDFsharp-MigraDoc 6.5.0 起改為 AGPL-3.0-only 並在執行時要求授權金鑰，與本專案的 CC0-1.0 不相容；
6.2.4 是最後一個 MIT 版本且不再有修正。因此 `OdfKit.Extensions.Pdf` 不再依賴任何第三方 PDF 函式庫。

## 實作契約

只依公開標準的欄位定義、演算法與互通行為重新撰寫。實作者不得閱讀後再翻譯、移植或改寫
PDFsharp、iText、PdfPig、QPDF、MuPDF、pdfium、poppler、FreeType、FontTools、libpng、giflib 或
其他 PDF／字型／影像函式庫的實作程式碼，也不得反編譯。

允許的設計來源：

- ISO 32000-1:2008（PDF 1.7）：檔案結構、交叉參照表與串流、物件串流、增量更新、標準安全處理常式
  （RC4、AES-128、AES-256 R6）、字型與 ToUnicode、註解、Name Tree、Associated Files
- [Microsoft OpenType 規格](https://learn.microsoft.com/en-us/typography/opentype/spec/)：`cmap`、`glyf`、`loca`、`hmtx`
- [Adobe CFF TN #5176](https://adobe-type-tools.github.io/font-tech-notes/pdfs/5176.CFF.pdf) 與
  [Type 2 Charstring TN #5177](https://adobe-type-tools.github.io/font-tech-notes/pdfs/5177.Type2.pdf)：CFF 結構、CID charset、子程式
- RFC 1950／1951（zlib／Deflate，使用 .NET `DeflateStream`）
- PNG 規格（W3C）、JPEG ITU-T T.81 的標記格式、GIF89a 規格、Windows BMP 的 BITMAPINFOHEADER 格式
- Unicode UAX #14 的換行概念（中日韓行首行尾禁則採用自訂的有界字元集合）

## 驗證方式

不依賴被禁止的實作作為「答案」：測試以專案自己的讀取器與 ToUnicode 擷取文字，並以獨立工具產生的
夾具（qpdf／pikepdf 產生的加密與物件串流 PDF）驗證讀取、解密與增量更新；影像測試資料由獨立影像函式庫產生，
只作為預期像素。這些工具只用於本機產生測試資料與比對輸出，沒有任何程式碼被複製進本專案。
