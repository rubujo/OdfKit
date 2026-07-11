# 串流讀取安全限制

> 內容語系：正體中文（臺灣）（`zh-TW`）。

`OdsStreamReader` 與 `OdtStreamReader` 不建立完整文件 DOM，但仍會配置目前資料列、
節點文字、ZIP 解壓及 XML Reader 所需的緩衝。低常駐設計不等於不受輸入大小影響。

## 預設限制

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

安全限制、驗證及 sanitize 是降低風險的措施，不構成對惡意文件絕對安全的保證。
