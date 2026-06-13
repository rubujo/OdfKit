# Typed DOM Coverage

本文件記錄 OdfKit typed DOM 對標 ODFDOM 的目前完成線。typed DOM 的目標不是取代高階文件 facade，而是提供 schema-aware、prefix-independent、可保真的 ODF-first 操作層。

## 目前能力

- `OdfNodeFactory.CreateElement(...)` 會先呼叫 generated factory，找不到 typed wrapper 時才回退到通用 `OdfElement`。
- `OdfElement.cs` 保留常用手寫 wrapper，例如 text paragraph、heading、table cell、draw frame、office document 與 manifest file entry。
- `GeneratedDomWrappers.g.cs` 由 ODF 1.4 schema metadata 產生，涵蓋大量 schema element wrapper、factory case 與 attribute property，且已重產生為第一批 typed datatype property。
- `OdfTypedDomCoverage.Build()` 會產生 machine-readable schema-to-wrapper report，列出 schema element、wrapper type、fallback 狀態、wrapper property 數、schema attribute value type 分布與 wrapper property CLR type 分布。
- CLI `typed-dom-coverage --format json` 可輸出同一份 report，供 CI 或 release artifact 保存。
- `eng/Test-OdfTypedDomCoverage.ps1` 會產生 `artifacts/typed-dom-coverage/odf-typed-dom-coverage.json`，GitHub Actions `Typed DOM coverage` workflow 會上傳同一份 artifact。
- `OdfElement` 已提供 schema-aware typed attribute helpers，涵蓋 `int`、`bool`、`decimal`、ODF 日期時間字串、XML Schema `time` / `duration`、ODF 長度 / 百分比、0 到 100 百分比、角度、樣式名稱 / 樣式名稱參照清單、色彩、IRI 參照、儲存格位址 / 範圍位址、儲存格範圍位址清單、三維向量 / 三維點、二維整數座標清單、語言 / 國別 / 文字系統 token、命名空間 token、單一字元、XML 名稱、`style:family`、`office:version` 與 MIME 類型。
- `DomWrappersCSharpWriter` 會從 RELAX NG `data` / `value` 節點與常用 named pattern 推斷 datatype，讓 wrapper 可把 boolean、integer、decimal、date/dateTime、time、duration、length/percent、bounded percent、angle、style name/list、color、IRI reference、cell address/range address、cell range address list、vector3D、point3D、points、language/country/script tokens、namespacedToken、character、XML name、style family、ODF version 與 media type attribute 輸出為可空強型別屬性；未知或衝突型別會保守維持 `string?`。
- 所有元素與屬性比對仍以 namespace URI + local name 為準，不依賴 XML prefix。
- generated wrapper 保留局部 CS1591 pragma；手寫 public / protected API 仍必須具備正體中文 XML docs。

## Coverage guard

目前 readiness test 固定下列最低門檻：

| Metric | Minimum |
|---|---:|
| Generated typed element classes | 550 |
| Generated factory cases | 590 |
| Generated attribute properties | 100000 |
| Generated integer attribute properties | 1000 |
| Generated boolean attribute properties | 10000 |
| Generated decimal attribute properties | 100 |
| Generated date/time attribute properties | 100 |
| Generated time attribute properties | 6 |
| Generated duration attribute properties | 1000 |
| Generated length / percent attribute properties | 10000 |
| Generated angle attribute properties | 1000 |
| Generated style name attribute properties | 1000 |
| Generated style name list attribute properties | 300 |
| Generated color attribute properties | 1000 |
| Generated IRI reference attribute properties | 400 |
| Generated bounded percent attribute properties | 1000 |
| Generated cell address attribute properties | 400 |
| Generated cell range address attribute properties | 400 |
| Generated cell range address list attribute properties | 800 |
| Generated vector3D attribute properties | 1000 |
| Generated point3D attribute properties | 90 |
| Generated points attribute properties | 90 |
| Generated language code attribute properties | 100 |
| Generated country code attribute properties | 100 |
| Generated script code attribute properties | 100 |
| Generated language tag attribute properties | 100 |
| Generated namespaced token attribute properties | 100 |
| Generated character attribute properties | 100 |
| Generated XML name attribute properties | 1000 |
| Generated style family attribute properties | 50 |
| Generated ODF version attribute properties | 50 |
| Generated media type attribute properties | 100 |

這些數字不是產品宣稱，而是退化保護。若 schema generator 改版導致數字下降，必須在同一變更說明原因，並更新本文件。

## ODFDOM parity gaps

- 尚未產生完整 child collection facade；目前主要是 element wrapper 與 attribute property。
- Attribute property 目前已有基底 typed helper，且 generated artifact 已包含 integer、boolean、decimal、date/dateTime、time、duration、length/percent、bounded percent、angle、style name/list、color、IRI reference、cell address/range address、cell range address list、vector3D、point3D、points、language/country/script tokens、namespacedToken、character、XML name、style family、ODF version 與 media type 強型別 property；其他 ODF datatype 仍需補齊。
- schema-to-wrapper coverage report 已可由 API / CLI 產生，並在 release pipeline 中固定保存 artifact。
- 尚未對 ODF Toolkit / ODFDOM 的 sample corpus 做逐項 API parity。
- High-level facade 仍由 Text / Spreadsheet / Presentation / Drawing 等文件模型承接，不直接從 generated DOM 推導。

## 完成條件

Typed DOM parity 要升為 `complete`，至少需要：

- 產生 machine-readable coverage report，列出 schema element / attribute 對應 wrapper。
- 所有 ODF 1.4 schema element 可由 factory 建立，並能 parse / serialize round-trip。
- 常用 datatype attribute 有強型別存取或明確保留為 string 的理由。
- 與 ODFDOM sample usage 對照的 user story tests。
