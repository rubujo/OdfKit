# NativeAOT 支援與部署邊界

OdfKit 將核心受控 API 的 NativeAOT 契約與擴充套件、外部程式及任意執行期 plugin 分開描述。
核心 smoke 必須實際發布並執行原生程式，不能把該結果外推至未個別驗證的第三方相依。

## 支援矩陣

| 範圍 | 狀態 | 證據與限制 |
|------|------|------------|
| ODF 核心 API 家族 | NativeAOT 實機驗證 | `eng/Test-TrimSmoke.ps1 -PublishAot` 在 Windows x64、Linux x64 與 macOS ARM64 建立原生程式，驗證 ODT／ODS／ODP／ODG／ODC／ODF、ZIP／XML、公式、OpenPGP、typed DOM coverage、物件資料讀取、字典範本繫結與明確 renderer 註冊 |
| typed DOM coverage | AOT-compatible | `OdfTypedDomCoverage.Build` 使用 schema generator 產生且按需初始化的靜態 wrapper metadata，不掃描執行期屬性；一般文件建立不會初始化 coverage 字典 |
| 動態探索與任意物件反射 | 明確邊界 | NativeAOT 應以 `OdfRendererRegistry.Register` 明確註冊 renderer，並以 `IOdfTemplateValueResolver` 處理 POCO 範本路徑；任意執行期 plugin 載入不屬核心靜態保證 |
| `OdfKit.WebFonts.Abstractions` | `net10.0` AOT-compatible | 專案啟用 `IsAotCompatible` |
| `OdfKit.WebFonts.OpenType` | `net10.0` AOT-compatible | 有界 parser、子集化、WOFF／WOFF2 與 source-generated／無反射熱路徑 |
| `OdfKit.WebFonts.Worker` | AOT-compatible | durable manifest 改用 `System.Text.Json` source generation |
| `OdfKit.WebFonts.Sidecar` | `net10.0` AOT-compatible；`net48` 用戶端 | 版本化具名 pipe 協定不依賴 reflection serialization |
| `OdfKit.WebFonts.Sidecar.Host` | Windows x64／ARM64 NativeAOT | x64 以主控台及 net48 用戶端執行，原生 Windows Service 由提升權限的 CI smoke 驗證；ARM64 交叉發布。Host 是 self-contained，不要求部署 .NET Runtime |
| 其它擴充套件 | 逐套件評估 | SkiaSharp、ClosedXML、PDF、RDF、LibreOffice／Office 互通及其它第三方相依，不由核心 smoke 推定為 AOT-compatible |

## net48 WOFF2 sidecar

ASP.NET Web Forms 安裝 `OdfKit.WebFonts.Hosting.SystemWeb` 後，會傳遞取得
`OdfKit.WebFonts.Sidecar`。處理程序內引擎仍維持 TTF／OTF／WOFF；只有在 JSON 明確加入
`sidecar` 且 allowlist 包含 `Woff2` 時，Handler 才委派至 NativeAOT Host。

Host 與 net48 用戶端須符合以下部署不變量：

- 使用相同 pipe 名稱、共同資產根目錄及至少 32-byte 高熵權杖。
- 權杖只從環境變數或受控 secret store 取得，不寫入 JSON、命令列、記錄或原始碼。
- Host 啟動參數固定允許的 `fontSourceId=path`；HTTP 要求不能提供檔案路徑。
- 預設採目前使用者限定的 Windows pipe。只有服務帳號分離時才使用
  `--allow-cross-user`，並另以 ACL 限制 pipe 使用者、Host 執行檔、字型及資產根目錄。
- IIS application pool 與 Host 不必採相同 CPU 架構；32-bit net48 可連線至 x64 Host。
- 每個 frame、sequence、scalar、連線、queue、資產大小與工作時間均有上限；協定錯誤不回傳
  內部例外文字。

本機完整閘門：

```powershell
pwsh eng/Test-WebFontSidecarAot.ps1 -RuntimeIdentifier win-x64
pwsh eng/Test-WebFontSidecarAot.ps1 -RuntimeIdentifier win-arm64 -PublishOnly
```

第一個命令會發布及執行 x64 NativeAOT Host，再由 net48 用戶端與真正的 System.Web Handler
產生 WOFF2。第二個命令只驗證 ARM64 交叉發布；ARM64 實機執行仍屬部署環境驗收項目。

## 為何不要求安裝 .NET 10 Runtime

NativeAOT publish 會把受控程式與所需 Runtime 元件編譯成平台原生、self-contained
執行檔。部署者下載符合 Windows 架構的 Host 產物即可，不應另以全機 Runtime 安裝作為
前置條件。建置 Host 的 CI 或開發機仍需 .NET 10 SDK 與對應 NativeAOT 工具鏈。

標籤發布流程會以 `eng/Publish-WebFontSidecar.ps1` 建立 x64／ARM64 ZIP、授權檔、第三方聲明
與獨立 SHA-256 manifest，並和 NuGet 套件一起附加至 GitHub Release。採用者應下載與
`OdfKit.WebFonts.Sidecar` 套件相同版本及正確 RID 的 ZIP，部署前核對 manifest。
