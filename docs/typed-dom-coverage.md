# Typed DOM Coverage

本文件記錄 OdfKit typed DOM 對標 ODFDOM 的目前完成線。typed DOM 的目標不是取代高階文件 facade，而是提供 schema-aware、prefix-independent、可保真的 ODF-first 操作層。

## 目前能力

- `OdfNodeFactory.CreateElement(...)` 會先呼叫 generated factory，找不到 typed wrapper 時才回退到通用 `OdfElement`。
- `OdfElement.cs` 保留常用手寫 wrapper，例如 text paragraph、heading、table cell、draw frame、office document 與 manifest file entry。
- `GeneratedDomWrappers.g.cs` 由 ODF 1.4 schema metadata 產生，涵蓋大量 schema element wrapper、factory case 與 attribute property。
- `OdfTypedDomCoverage.Build()` 會產生 machine-readable schema-to-wrapper report，列出 schema element、wrapper type、fallback 狀態、wrapper property 數與 schema attribute value type 分布。
- CLI `typed-dom-coverage --format json` 可輸出同一份 report，供 CI 或 release artifact 保存。
- 所有元素與屬性比對仍以 namespace URI + local name 為準，不依賴 XML prefix。
- generated wrapper 保留局部 CS1591 pragma；手寫 public / protected API 仍必須具備正體中文 XML docs。

## Coverage guard

目前 readiness test 固定下列最低門檻：

| Metric | Minimum |
|---|---:|
| Generated typed element classes | 550 |
| Generated factory cases | 590 |
| Generated attribute properties | 100000 |

這些數字不是產品宣稱，而是退化保護。若 schema generator 改版導致數字下降，必須在同一變更說明原因，並更新本文件。

## ODFDOM parity gaps

- 尚未產生完整 child collection facade；目前主要是 element wrapper 與 attribute property。
- Attribute property 目前多為 string-based；尚未全面轉為 enum、length、date、boolean、integer 等強型別。
- schema-to-wrapper coverage report 已可由 API / CLI 產生；尚未在 release pipeline 固定保存 artifact。
- 尚未對 ODF Toolkit / ODFDOM 的 sample corpus 做逐項 API parity。
- High-level facade 仍由 Text / Spreadsheet / Presentation / Drawing 等文件模型承接，不直接從 generated DOM 推導。

## 完成條件

Typed DOM parity 要升為 `complete`，至少需要：

- 產生 machine-readable coverage report，列出 schema element / attribute 對應 wrapper。
- 所有 ODF 1.4 schema element 可由 factory 建立，並能 parse / serialize round-trip。
- 常用 datatype attribute 有強型別存取或明確保留為 string 的理由。
- 與 ODFDOM sample usage 對照的 user story tests。
