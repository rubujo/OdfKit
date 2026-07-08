# ODF 1.4 規格覆蓋與高階深度路線圖

OdfKit 的長期方向是成為 ODF 界的 NPOI：C# / .NET 友善、任務導向、可測且可維護。這不等於把所有 ODF 元素都升成高階 facade。規格覆蓋與實務 API 深度必須分層追蹤，避免把冷門規格功能誤判成一般使用者的主要缺口。

## 分層目標

| 層級 | 範圍 | 目標 | 非目標 |
| --- | --- | --- | --- |
| L0 | schema coverage / typed DOM | ODF 1.4 元素與屬性能被盤點、產生 wrapper、讀寫、保留、round-trip 與驗證 | 每個元素都有高階 C# API |
| L1 | package lifecycle | 主要文件類型、template、flat XML variant 能建立、載入、儲存、轉換、嵌入與驗證 | 內建完整 office layout/render/calculation engine |
| L2 | high-level facade | Text、Spreadsheet、Presentation、Drawing、Chart、Image、Formula、Database 等常用工作流有一致 API | 完整動畫模型、完整 ODB 執行引擎、完整 TeX/CAS、完整 3D 設計器 |
| L3 | interop behavior | 針對 LibreOffice、Microsoft Office ODF 與 portable editing 提供實務風險提示 | 宣稱跨套件像素級一致 |

## L0：Schema Coverage

L0 以 `typed-dom-coverage` 與 `OdfTypedDomCoverage.Build()` 追蹤。目前的驗收不是一次達到 100%，而是每次變更都能穩定輸出：

- ODF 1.4 schema 元素與屬性總數。
- 已有 generated wrapper 的元素與屬性數。
- 缺口分類：缺 wrapper、缺屬性型別、缺 round-trip 測試、需要手寫 facade。
- 優先級：常見文件生命周期與互通會碰到的節點優先，冷門節點先保留在 typed DOM 與 validator 層。

達成 100% typed DOM coverage 在工程上可行，但高階 API 不追 100%。高階 API 只補實務工作流。

## L1：Format Lifecycle

每個主要文件類型至少要有 lifecycle scenario：

- ODT / OTT / FODT
- ODS / OTS / FODS
- ODP / OTP / FODP
- ODG / OTG / FODG
- ODC / OTC / FODC
- ODF / OTF / FODF
- ODI / OTI / FODI
- ODB

每個 scenario 至少覆蓋建立、載入、儲存、round-trip 與基本驗證。template 與 flat XML variant 不要求有獨立高階 facade 深度，但相容入口與短名 facade 行為必須一致。

## L2：High-Level Facade

高階 API 以「C# 使用者會直接拿來完成工作」為準：

- TemplateBinder：支援 scalar token 與 `{{Items[].Field}}` 集合展開，保留既有 `Bind(document, values)` 相容行為，進階用法回傳 `OdfTemplateBindReport`。
- Spreadsheet / Chart：`InsertChartFromRange` 回傳可編輯的 embedded `OdfChartDocument`，並與 standalone chart 共用 bubble、stock、3D、wall / floor 能力。
- Image：批次 crop / rotation helper 回報更新數與未命中名稱，不在核心做影像轉檔。
- Drawing：`AddFlow` 提供方向、間距與節點大小 options，不追完整流程圖自動排版引擎。
- Database / Formula：保持可建立、可載入、可嵌入、可查詢與常見編輯方便，不內建 DB engine、SQL executor、CAS 或完整 TeX parser。

## L3：Interop Risk Intelligence

`OdfPracticalCompatibilityValidator` 只做風險提示，不取代 OASIS schema 驗證，也不保證跨套件呈現一致。

文件類型特化規則：

- ODT：頁首／頁尾、文字方塊、非可攜影像、巨集／腳本。
- ODS：欄寬／列高、列印設定、嵌入進階圖表、自動樣式碎裂。
- ODP / ODG：文字方塊、群組圖形、裁切／旋轉圖片、連接線。
- ODC：bubble、stock、3D、wall / floor 對非 LibreOffice profile 提示風險。
- ODI：非 PNG / JPEG / SVG 圖片對 portable editing 提示風險。

## 追蹤方式

- CLI：`dotnet run --project tools/OdfKit.Cli -- typed-dom-coverage`
- 測試：coverage audit 測試只要求穩定輸出與合理門檻，不把 100% 當作短期 gate。
- 文件：新增 facade 時同步更新 API 文件、cookbook 或 scenario test。
- 排除：NuGet 發佈、套件上架與完整 office engine 均不納入本路線圖。
