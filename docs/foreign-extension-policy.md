# Foreign Extension 隔離策略

本文件記錄 OdfKit 對 foreign namespace 內容的支援邊界。目標是在符合
ODF extended profile 的前提下，保留未知內容，同時讓驗證器能指出可移除性風險。

## 名詞

- ODF namespace：`urn:oasis:names:tc:opendocument:xmlns:*:1.0` 與專案已知的
  ODF 相容命名空間。
- infrastructure namespace：XML、XMLNS、XLink、XML Signature 等 ODF 文件常用的
  基礎命名空間。
- foreign namespace：不屬於 ODF namespace，也不屬於 infrastructure namespace 的
  自訂擴充命名空間。

## 驗證策略

`OASIS_ODF_1_4_Extended` profile 允許 foreign namespace，但會透過
`RequireForeignExtensionIsolation` 對 foreign element 產生 warning。這個 warning
不讓文件失敗，目的是提醒呼叫端確認擴充內容是隔離且可移除的。

Strict profile 不允許以 ODF namespace 偽裝擴充；若元素或屬性位於 ODF namespace
但不存在於選定 schema metadata，驗證器會回報 `DisallowInvalidOdfNamespaceExtensions`
或 `RequireOdfNamespaceValidity`。

## Round-Trip 策略

OdfKit 的 DOM reader / writer 以 `NamespaceURI` 與 `LocalName` 為判斷基準，並保留
prefix、unknown ODF element、foreign element、foreign attribute、comment 與 processing
instruction。高階 API 在保存文件時不得主動刪除 foreign namespace 內容。

## 淨化策略

安全淨化只移除已知高風險內容，例如 macro / script package entry、macro URI 與過期簽章。
安全的 foreign namespace 內容不應被 macro sanitization 誤刪。若使用者需要移除 foreign
content，應以明確的文件轉換或專用 policy 實作，不應隱含在一般保存流程中。

## 可移除性準則

一個 foreign extension 應符合下列準則：

- 使用非 ODF namespace，不在 ODF namespace 中自造 element 或 attribute。
- 不讓核心 ODF 結構依賴 foreign element 才能保持 well-formed。
- 移除 foreign subtree 後，周邊 ODF element 仍應保留有效的 schema content model。
- 不以 foreign attribute 取代必要的 ODF attribute。
- 不以 remote resource、macro URI 或 script entry 建立額外安全風險。

## 測試證據

- `ComplianceTests.ExtendedProfileReportsForeignExtensionIsolationWarning` 驗證 extended profile
  對 foreign element 產生 warning，但不阻擋文件。
- `OdfUnknownXmlRoundTripTests.HighLevelSavePreservesUnknownXmlForeignContentAndProcessingInstructions`
  驗證 high-level save 保留 unknown XML、foreign namespace、prefix、comment 與 processing
  instruction。
- `OdfSecurityBoundaryTests` 驗證 macro sanitization 不會誤刪安全的 foreign content。
