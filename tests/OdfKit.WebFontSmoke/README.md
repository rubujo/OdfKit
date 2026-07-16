# OdfKit WebFont 最小驗證

此專案不是正式 WebFont 套件，而是驗證套件拆分提案中最關鍵且尚未由既有測試覆蓋的技術鏈：

1. 使用具備 OFL-1.1 授權的 Noto Sans TC 真實字型。
2. 保留 Unicode Plane 0、1、2、3 的 13 個測試字元，其中 Plane 2 字元分別來自 CNS 11643 第 3、4、5、6、7、10、11、12、15 字面。
3. 以純 .NET 引擎與 BCL Brotli 產生真正的 WOFF2 子集。
4. `eng/Test-WebFontFormatMatrix.ps1` 另以真實 CNS PUA、IPAmj IVS、TTC、CFF／CFF2、variable 與 color font 驗證成功或明確拒絕路徑。
5. 由最小 ASP.NET Core 應用程式提供 font/woff2，並讓瀏覽器透過 FontFaceSet 驗證載入。
6. 同頁顯示 OdfKit OdfFontContext 對 Plane 0～3 文字的分段結果。

在方案根目錄執行 pwsh eng/Test-WebFontSmoke.ps1。

若已具備字型與 CNS 11643 對照表，可傳入 `FontPath` 與 `MappingTablesRoot`，避免重複下載。

產物寫入忽略版控的 artifacts/webfont-smoke/assets。測試只證明核心資料鏈可行；正式產品仍需要定義授權政策、字形閉包、可重現快取、錯誤模型及獨立 NuGet 套件 API。
