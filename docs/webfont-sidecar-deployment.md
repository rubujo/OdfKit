# WebFont Sidecar 部署與維運

本文件說明 `OdfKit.WebFonts.Sidecar.Host` 的適用範圍、System.Web 設定、Windows
生命週期管理，以及非 Windows Server 環境的替代路徑。Sidecar 的唯一主要用途，是讓
ASP.NET Web Forms／.NET Framework 4.8 在要求階段產生 WOFF2。ASP.NET Core 在支援的
.NET Runtime 上直接使用 managed 引擎，不需要 Sidecar。

## 平台決策

| 網站平台 | 作業系統 | 建議路徑 |
|----------|----------|----------|
| ASP.NET Web Forms／.NET Framework 4.8 | Windows Server | 使用 NativeAOT Sidecar；正式環境由受管程序監督器管理 |
| ASP.NET Web Forms／IIS Express | Windows 10／11 開發機 | 使用 `sidecar.autoStart: true` |
| ASP.NET Core | Windows Server、Windows 10／11 | 直接使用 managed 引擎，不部署 Sidecar |
| ASP.NET Core | Linux／macOS | 直接使用 managed 引擎，不部署 Sidecar |
| System.Web | Linux／macOS | 不支援；System.Web 與目前 Sidecar 發布產物均為 Windows 路徑 |

不要在 Linux／macOS 以 Wine、Mono 或相容層執行 Windows Sidecar。這些組合未進入支援矩陣，
也沒有具名 pipe、字型解析、檔案鎖定及瀏覽器端到端證據。

## 取得與驗證 Host

`OdfKit.WebFonts.Sidecar` NuGet 只包含 client library，不包含 Host。從相同版本的 GitHub
Release 取得符合目標架構的 ZIP：

- `win-x64`：一般 x64 Windows Server 或 Windows 10／11。
- `win-arm64`：Windows ARM64；目前只有交叉發布證據，正式採用前仍須在目標機器驗收。

解壓縮前核對 Release 隨附的 `OdfKit.WebFonts.Sidecar.Host-SHA256SUMS`，並保留版本、RID、
SHA-256、下載來源及部署日期。Host 是 self-contained NativeAOT 執行檔，目標機器不需要安裝
.NET 10 Runtime。

可先確認執行檔與 WOFF2 Runtime 能力：

```powershell
.\OdfKit.WebFonts.Sidecar.Host.exe --probe
```

成功輸出包含協定版本、`woff2=True` 及 Runtime Identifier。`--probe` 只驗證執行檔本身，
不會確認正式 Host 已啟動、pipe 可連線或權杖一致。

## System.Web 設定

將完整字型、資產及 Host 分開放置，並限制 IIS 應用程式集區與 Sidecar 服務帳號的 ACL：

```text
C:\OdfKitWebFonts\
  Host\OdfKit.WebFonts.Sidecar.Host.exe
  Fonts\TW-Sung-Plus-98_1.ttf
  Fonts\TW-Kai-Plus-98_1.ttf
  Assets\
  Cache\
```

`App_Data/webfonts.dynamic.json` 使用固定 pipe、相同資產根目錄及明確 allowlist：

```json
{
  "schemaVersion": 1,
  "assetRootPath": "C:\\OdfKitWebFonts\\Assets",
  "allowManagedFallback": false,
  "sidecar": {
    "pipeName": "odfkit-webfonts-production",
    "tokenEnvironmentVariable": "ODFKIT_WEBFONT_SIDECAR_TOKEN",
    "tokenAppSettingName": "OdfKit.WebFonts.SidecarToken",
    "tokenFilePath": "C:\\OdfKitWebFonts\\Secrets\\sidecar.token",
    "connectTimeoutSeconds": 5,
    "requestTimeoutSeconds": 180,
    "maxMessageBytes": 4194304,
    "autoStart": false,
    "startupTimeoutSeconds": 15,
    "stopWithApplicationProcess": false
  },
  "allowedFormats": [ "Woff2", "Woff", "TrueType" ]
}
```

正式環境應維持 `autoStart: false`。只要 JSON 具有有效 `sidecar` 區段，Handler 就會自動使用
Sidecar；前端不需要，也不應提供正式環境的啟用開關。`allowManagedFallback` 是受信任診斷
功能，不是 Sidecar 故障時的靜默遞補。

Host 與 System.Web 必須使用相同：

- pipe 名稱；
- 資產根目錄；
- 至少 32 UTF-8 bytes 的高熵 Sidecar token；
- 字型來源 ID；
- 套件／協定版本。

## Host 命令列

正式 Host 的最小命令列如下：

```powershell
$env:ODFKIT_WEBFONT_SIDECAR_TOKEN = '<secret-store-value>'
.\OdfKit.WebFonts.Sidecar.Host.exe `
  --service-name OdfKitWebFontsSidecar `
  --pipe odfkit-webfonts-production `
  --asset-root C:\OdfKitWebFonts\Assets `
  --cache-root C:\OdfKitWebFonts\Cache `
  --token-file C:\OdfKitWebFonts\Secrets\sidecar.token `
  --font-source cns-sung-plus=C:\OdfKitWebFonts\Fonts\TW-Sung-Plus-98_1.ttf `
  --font-source cns-kai-plus=C:\OdfKitWebFonts\Fonts\TW-Kai-Plus-98_1.ttf
```

| 參數 | 預設值／範圍 | 說明 |
|------|---------------|------|
| `--pipe` | 必填，最長 128 字元 | 不得包含 `/` 或 `\` |
| `--asset-root` | 必填 | 必須與 System.Web 資產根目錄相同 |
| `--cache-root` | 未設定 | 選用的 durable cache |
| `--token-environment-variable` | `ODFKIT_WEBFONT_SIDECAR_TOKEN` | 只指定變數名稱，不傳送 secret 值 |
| `--token-file` | 未設定 | 環境變數沒有值時讀取的 ACL 保護 token 檔案 |
| `--service-name` | `OdfKit WebFonts Sidecar` | 必須與 SCM 服務名稱一致 |
| `--font-source` | 至少一個，可重複 | 格式為 `id=絕對路徑` |
| `--max-message-bytes` | 4 MiB；4 KiB～16 MiB | 單一 pipe frame 上限 |
| `--max-connections` | 8；1～64 | 同時 pipe 連線上限 |
| `--max-concurrency` | 1；1～32 | 同時產字工作上限 |
| `--queue-capacity` | 32；1～4,096 | 有界工作佇列容量 |
| `--max-unicode-scalars` | 65,536；1～65,536 | 單一 Host 工作字元上限；HTTP 層通常應更小 |
| `--max-asset-bytes` | 64 MiB；1 byte～256 MiB | 輸出及快取資產上限 |
| `--job-timeout-seconds` | 180；1～1,800 | 單一工作與連線逾時基準 |
| `--parent-process-id` | 未設定 | 僅供受控自動啟動；父程序結束後停止 |
| `--allow-cross-user` | 關閉 | 允許不同 Windows 使用者連線；仍須另外設定 ACL |

HTTP Handler 的 `maxUnicodeScalarCount`、`maxAssetBytes`、request body、並行數及 allowlist
仍是外層安全邊界。不要只放寬 Host 上限。

## Windows Server 生命週期

Host 使用 Microsoft Generic Host 與 `AddWindowsService`，可以同一個 NativeAOT 執行檔接受
SCM 啟動、停止與系統關機通知，也保留主控台及 IIS Express 自動啟動模式。正式部署不需要
WinSW、NSSM 或其它 wrapper。

Release ZIP 內的 `Manage-WebFontSidecarService.ps1` 會建立虛擬服務帳號、延遲自動啟動、
service SID、ACL、Event Log source 與有退避的復原設定。請在系統管理員 PowerShell 執行：

```powershell
.\Manage-WebFontSidecarService.ps1 `
  -Action Install `
  -ServiceName OdfKitWebFontsSidecar `
  -DisplayName "OdfKit WebFonts Sidecar" `
  -HostExecutablePath C:\OdfKitWebFonts\Host\OdfKit.WebFonts.Sidecar.Host.exe `
  -PipeName odfkit-webfonts-production `
  -AssetRootPath C:\OdfKitWebFonts\Assets `
  -CacheRootPath C:\OdfKitWebFonts\Cache `
  -TokenFilePath C:\OdfKitWebFonts\Secrets\sidecar.token `
  -FontSource "cns-sung-plus=C:\OdfKitWebFonts\Fonts\TW-Sung-Plus-98_1.ttf" `
  -FontSource "cns-kai-plus=C:\OdfKitWebFonts\Fonts\TW-Kai-Plus-98_1.ttf" `
  -IisAppPoolName OdfKit `
  -StartService
```

Token 檔案不存在時，腳本會產生 48-byte 隨機值，移除繼承 ACL，只授予 Administrators、
SYSTEM、服務帳號及指定 IIS application pool 讀取權。token 值不會出現在 SCM `binPath`。
若組織已有 secret store，可自行佈署 token，或繼續使用
`ODFKIT_WEBFONT_SIDECAR_TOKEN`；環境變數優先於 token 檔案。

查詢、更新及解除安裝：

```powershell
.\Manage-WebFontSidecarService.ps1 -Action Status -ServiceName OdfKitWebFontsSidecar

.\Manage-WebFontSidecarService.ps1 `
  -Action Update `
  -ServiceName OdfKitWebFontsSidecar `
  -HostExecutablePath C:\OdfKitWebFonts\Host\OdfKit.WebFonts.Sidecar.Host.exe `
  -AssetRootPath C:\OdfKitWebFonts\Assets `
  -CacheRootPath C:\OdfKitWebFonts\Cache `
  -TokenFilePath C:\OdfKitWebFonts\Secrets\sidecar.token `
  -FontSource "cns-sung-plus=C:\OdfKitWebFonts\Fonts\TW-Sung-Plus-98_1.ttf" `
  -IisAppPoolName OdfKit `
  -StartService

.\Manage-WebFontSidecarService.ps1 `
  -Action Uninstall `
  -ServiceName OdfKitWebFontsSidecar
```

解除安裝只刪除 SCM 服務及 Event Log source，不刪除 Host、字型、資產、cache 或 token。
服務帳號預設為 `NT SERVICE\<ServiceName>` 虛擬帳號；多節點部署可明確傳入 gMSA。服務與
IIS application pool 通常是不同身分，所以安裝腳本會啟用跨使用者 pipe，並以 token 與檔案
ACL 保護。這不能取代服務帳號、服務管理權限及組織 secret store。

使用 gMSA 時，先由網域管理員完成帳號建立、主機授權及安裝，再傳入結尾含 `$` 的帳號：

```powershell
.\Manage-WebFontSidecarService.ps1 `
  -Action Install `
  -ServiceName OdfKitWebFontsSidecar `
  -ServiceAccount 'CONTOSO\OdfKitWebFonts$' `
  -HostExecutablePath C:\OdfKitWebFonts\Host\OdfKit.WebFonts.Sidecar.Host.exe `
  -PipeName odfkit-webfonts-production `
  -AssetRootPath C:\OdfKitWebFonts\Assets `
  -CacheRootPath C:\OdfKitWebFonts\Cache `
  -TokenFilePath C:\OdfKitWebFonts\Secrets\sidecar.token `
  -FontSource "cns-sung-plus=C:\OdfKitWebFonts\Fonts\TW-Sung-Plus-98_1.ttf" `
  -IisAppPoolName OdfKit `
  -StartService
```

腳本不接受需要在命令列傳遞密碼的一般網域或本機帳號；只接受 Windows 內建帳號、服務虛擬
帳號或 gMSA，避免密碼進入命令歷程、程序參數或部署記錄。

## Windows 10／11 與 IIS Express

本機開發可由 System.Web Handler 自動啟動：

```json
{
  "sidecar": {
    "pipeName": "odfkit-webfonts-development",
    "tokenAppSettingName": "OdfKit.WebFonts.SidecarToken",
    "autoStart": true,
    "hostExecutablePath": "Sidecar/OdfKit.WebFonts.Sidecar.Host.exe",
    "startupTimeoutSeconds": 15,
    "stopWithApplicationProcess": true
  }
}
```

Handler 會先探測既有 pipe，只有服務不存在時才以隱藏子程序啟動 Host。這個模式只適用單一
使用者的開發機或單 worker 受控測試，不適用 web garden、多站台共用 Host 或正式機關環境。

一般 Windows 10／11 MIS 單機若不使用 IIS Express，也可以安裝前節的原生 Windows Service；
不要讓使用者登入後手動開啟 Host 視窗。

## Linux、macOS 與非 System.Web 伺服器

ASP.NET Core 在 Windows、Linux 與 macOS 均應直接註冊
`ManagedOpenTypeWebFontSubsetEngine`。現代 .NET Runtime 提供 OdfKit WOFF2 路徑所需的
Brotli API，因此不需要用 Windows Sidecar 繞行：

```csharp
builder.Services.AddOdfWebFontGeneration(
    _ => new ManagedOpenTypeWebFontSubsetEngine(engineOptions),
    generationOptions =>
    {
        // 設定 face、Profile、format allowlist 與有界 Worker。
    });
```

部署為 systemd、launchd、Kubernetes 或 Linux container 時，管理的是 ASP.NET Core 網站
程序本身，不是 Sidecar。不得把 Windows Sidecar ZIP 複製到 Linux container，也不要公開
具名 pipe 為 TCP 服務。

## 健康檢查與故障語意

正式監控至少包含：

1. Host 程序存在且沒有快速重啟。
2. 受信任後端能以正確 token 連線至 pipe。
3. 固定的宋體／楷體小型 canary 能產生 manifest。
4. manifest 中的內容定址 WOFF2 可以 GET，SHA-256 與 ETag 正確。
5. 瀏覽器 canary 能以像素驗證指定 PUA，不只呼叫 `document.fonts.check()`。

| 現象 | 意義 | 優先檢查 |
|------|------|----------|
| HTTP 204 | 指定來源沒有可產生 glyph | 字型版本、`cmap`、Profile、PUA 對照 |
| HTTP 401／403 | 動態產字 API 授權失敗 | API key 與 BFF |
| HTTP 429 | HTTP 限流或 Sidecar queue 已滿 | 並行數、queue、`Retry-After`、負載 |
| HTTP 503 | Sidecar 不可連線、啟動失敗、token／協定錯誤或逾時 | Host、pipe、帳號、ACL、token、版本 |
| 一般字正常但 PUA 是豆腐字 | manifest 沒有該字，或 WebFont 未實際畫出 | 來源 `cmap`、資產 GET、CSP、像素驗證 |

不要在 Sidecar 失敗時把所有文字強制改成 CNS Plus。系統已有的文字仍應由系統字型顯示；
既有預產生難字資產可繼續使用，尚未產生的難字則應回報可觀察的暫時失敗。

## 升級與回復

Sidecar client、Host、System.Web Hosting 套件與 pipe 協定視為同一版部署單元：

1. 在隔離目錄放置新 Host，核對 SHA-256。
2. 使用新 pipe 名稱啟動新 Host，完成 `--probe`、pipe、產字、資產 GET 與瀏覽器 canary。
3. 更新網站設定及套件，讓新 worker 指向新 pipe。
4. 觀察 429、503、產字延遲及 cache 命中。
5. 確認沒有舊 worker 後停止舊 Host。
6. 保留舊 Host、設定與字型版本至回復窗口結束。

目前 Sidecar token 是單值契約，沒有同一 pipe 的雙 token overlap。需要輪替時，使用新 pipe
與新 Host 做藍綠切換，避免把 secret 同時寫入多個不受控位置。

## 第一方部署依據

- [Microsoft：使用 BackgroundService 建立 Windows Service](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service)
- [Microsoft：NativeAOT 部署](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [Microsoft：Windows Server 服務帳號](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/understand-service-accounts)
- [Microsoft：Windows Service 復原準則](https://learn.microsoft.com/windows/win32/rstmgr/guidelines-for-services)
- [Microsoft：以 systemd 託管 ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-10.0)
