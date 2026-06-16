# Third-Party Notices

OdfKit 原創程式碼採用 CC0-1.0 Universal 授權。下列建置與執行期相依套件維持各自授權。

| 套件 (Package) | 用途 (Purpose) | 授權 (License) |
|---|---|---|
| BouncyCastle.Cryptography | 提供加密、雜湊與金鑰衍生演算法支援 | MIT |
| CommunityToolkit.HighPerformance | 高效能記憶體與緩衝區工具 | MIT |
| System.Security.Cryptography.Xml | XML 數位簽章處理 | MIT |
| System.Security.Cryptography.Pkcs | PKCS7 / CMS 簽章處理 | MIT |
| Sylvan.Data.Csv | 用於 ODS 檔案的 CSV 匯入與匯出 | MIT |
| System.Memory / System.Buffers / System.Threading.Tasks.Extensions / Microsoft.Bcl.AsyncInterfaces / System.Text.Encoding.CodePages | netstandard2.0 平台相容性支援 | MIT |
| AngleSharp | 用於 HTML 解析與轉換擴充（於 OdfKit.Extensions.Html 中使用） | MIT |
| SkiaSharp / HarfBuzzSharp | 跨平台圖像繪製與文字排版支援（於 OdfKit.Extensions.Imaging 中使用） | MIT |
| ClosedXML / DocumentFormat.OpenXml | 用於 OOXML 格式（如 Excel）之整合與匯入匯出（於 OdfKit.Extensions.Ooxml 中使用） | MIT |
| PDFsharp-MigraDoc | PDF 相關處理、排版與繪製擴充（於 OdfKit.Extensions.Pdf 中使用） | MIT |

分發包含上述相依套件的應用程式時，請依各套件之授權條款，保留其必要的授權與著作權聲明。
