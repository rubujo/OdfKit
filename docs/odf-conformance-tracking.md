# ODF 條款對測試追蹤

本文件說明 OdfKit 如何驗證輸出與輸入符合 ODF 1.0～1.4，以及哪些事情**不**宣稱。
它不是合規認證，OdfKit 也不宣稱完整合規。

## 方法

依 [OASIS OIC 互通性白皮書](https://docs.oasis-open.org/oic/StateOfInterop/v2.0/StateOfInterop-v2.0.html)
的建議，組合多種互補的自動化檢查，而不是為每個句子維護人工裁決帳本：

1. **官方 RELAX NG schema 驗證**：XML 有效性是必要但不充分的條件。
2. **獨立驗證器差異測試**：由官方 schema 合成最小有效文件，Jing 為獨立判定者，
   OdfKit 驗證器在 Strict 與 Extended 設定下都必須與它一致。
3. **schema 之外的規則**：ODF 1.2 以後只出現在規範文字中的約束（例如 `xml:id` 唯一、
   `config:name` 前綴、動畫識別碼），以具名規則（`ODF3xxx`）實作，並各有正反向回歸測試。
4. **原子功能與往返測試**：每個功能以最小文件讀寫、再載入並比對。
5. **互通語料**：`tests/fixtures/corpus` 與 LibreOffice／OPF 驗證器的互通工作流程。

## 追蹤範圍

追蹤單位是已出版的編號條款（約 12,256 條），不再拆成逐句義務。
條款清單與缺口統計見 [合規矩陣](odf-conformance-matrix.md)。

## 測試位置

| 內容 | 位置 |
|---|---|
| 屬性定義差異測試（8,358 案例） | `OdfKit.Tests/OdfAttributeDefinitionConformanceTests.cs`、`OdfKit.Tests/TestData/OdfAttributeCases.jsonl.gz` |
| 元素定義差異測試（12,139 案例） | `OdfKit.Tests/OdfElementDefinitionConformanceTests.cs`、`OdfKit.Tests/TestData/OdfElementCases.jsonl.gz` |
| 夾具重新驗證 | `eng/Test-OdfAttributeFixture.py`、`eng/Test-OdfElementFixture.py` |
| 語料驗證 | `eng/Test-OdfCorpus.ps1`、`tests/fixtures/corpus/manifest.json` |
| 合規矩陣契約 | `eng/Test-OdfConformanceMatrix.ps1` |

## 不宣稱的事

- 不宣稱完整合規，也不是 OASIS 或其他機構的認證。
- 差異測試證明 OdfKit 驗證器與 Jing 對合成案例判定一致，不證明所有真實文件都能正確處理。
- 規範文字中無法自動測試的要求，保留為缺口，不算成已通過。

## 歷史

先前曾維護逐句語意分類與實作裁決的雜湊帳本（約 5,862 個義務群組）。因維護成本
遠高於它帶來的驗證價值，且沒有外部標準依據，已於後續提交移除。最後完整版本位於
提交 `3add086f`，需要時可由 git 歷史還原。逐句義務清單與分類（約 123,633 個候選句、
逾 100 MB 的 CSV）也已一併移除，因為沒有任何工具或測試使用它們。其中發現的缺陷修正與測試均已保留。
