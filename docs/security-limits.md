# 載入與串流讀取安全限制

> 內容語系：正體中文（臺灣）（`zh-TW`）。

核心封裝載入與 `OdsStreamReader`／`OdtStreamReader` 都會處理不可信 ZIP／XML 輸入。
串流 Reader 不建立完整文件 DOM，但仍會配置目前資料列、
節點文字、ZIP 解壓及 XML Reader 所需的緩衝。低常駐設計不等於不受輸入大小影響。

## 核心封裝限制

`OdfDocument.Load`、各格式 `Load` facade 與直接使用 `OdfPackage.Open` 的流程，共用
`OdfLoadOptions` 的封裝資源預算。預設限制如下：

| 限制 | 預設值 | 防護目的 |
|---|---:|---|
| ZIP 項目數 | 5,000 | 避免大量微小項目造成 CPU 與記憶體耗盡 |
| 單一項目解壓縮大小 | 500 MiB | 限制單一 ZIP 項目的展開量 |
| 全封裝解壓縮總量 | 1 GiB | 限制跨項目的總展開量 |
| 不可搜尋輸入原始大小 | 1 GiB | 在 ZIP 展開前限制緩衝量 |
| 單一 XML 文件字元數 | 64 MiB | 限制 XML 解析與 DOM 建立成本 |

ZIP 項目數、單一項目、總解壓量與原始封裝大小必須設定為正值；0 或負值會立即擲出
`ArgumentOutOfRangeException`，不會在不同載入路徑中被解讀成不同語意。只有
`MaxXmlCharactersInDocument = 0` 明確表示停用 XML 字元限制，負值仍屬無效輸入。

所有核心 XML Reader 均應禁止外部 DTD／resolver；新增載入入口時，必須沿用
`OdfLoadOptions` 或提供等價且文件化的資源預算，不可建立無上限的旁路。套件與 Flat XML
驗證路徑（`OdfPackageValidator`、`OdfFlatDocumentValidator`、profile 規則掃描）同樣套用
`MaxXmlCharactersInDocument`：封裝路徑使用 `package.LoadOptions`，Flat 路徑使用
`OdfValidationOptions.LoadOptions`（未指定時採 `OdfLoadOptions` 預設 64 MiB）。簽章、
時間戳記、憑證撤銷資料與外部網路回應另有各自的較小界限，不能以核心封裝上限取代。

這些限制是載入階段的資源防線，不等於文件內容安全政策。巨集、外部資源、簽章、加密與
profile 規則應另以 `OdfPackageValidator`、`SanitizeMacros`、簽章驗證 API 或
`pwsh eng/Test-OdfPolicy.ps1` 處理。

## 串流 Reader 限制

| Reader | 限制 | 預設值 |
|---|---|---:|
| ODS | XML 字元 | 64 MiB |
| ODS | 單一工作表資料列 | 1,048,576 |
| ODS | 單列資料行 | 16,384 |
| ODS | 單一 repeat 宣告 | 列 1,048,576；欄 16,384 |
| ODS | 單一儲存格擷取文字 | 16 MiB |
| ODT | XML 字元 | 64 MiB |
| ODT | 回傳文字節點 | 1,000,000 |
| ODT | 單一節點擷取文字 | 16 MiB |

超過限制時讀取會失敗，不會截斷 repeat 後繼續回傳看似完整的資料。應將這類失敗視為
資源保護結果，不應自動改用無上限設定重試。

## 資料流所有權

options 的 `LeaveOpen` 預設為 `false`。設為 `true` 時，處置 Reader 仍會關閉其 XML entry
串流及 ZIP Reader，但保留呼叫端提供的最外層資料流。

## 信任邊界

不可信文件應保留預設限制，並先執行 package／schema 驗證。可信且確實需要處理大型文件
時，可以提高個別限制；提高 XML 或文字上限也會同步提高記憶體與 CPU DoS 風險。
`MaxXmlCharactersInDocument = 0` 只會停用 XML 字元限制，其餘 Reader 限制仍然有效。
ODS／ODT Reader options 在設定屬性時即驗證相同規則：XML 字元限制允許 0、拒絕負值；
資料列、資料行、repeat、節點與單一文字上限都必須大於 0，不會延後到開始讀取後才失敗。

安全限制、驗證及 sanitize 是降低風險的措施，不構成對惡意文件絕對安全的保證。
