# 高階語意 API Clean-room 規範

ODT、ODS、ODP 與 ODG 高階 API 的語意來源以 OASIS ODF 1.4 規格為主，
並以 ODF 1.1～1.3 官方 schema 驗證舊版文件相容性。競品只可作為公開工作流程
與使用者體驗參考，不得閱讀、翻譯或移植其原始碼。

## 允許來源

- OASIS 發佈的 ODF 規格、schema 與 errata。
- LibreOffice 與 Microsoft Office 的公開文件。
- OdfKit 自行產生或具明確可重散布授權的 corpus。
- 由自動化操作建立的辦公軟體輸出；只能記錄可觀察行為與結構差異。

## 禁止來源

- 商業文件 SDK 或授權不相容專案的原始碼、反編譯結果及內部測試資料。
- 無法確認授權的文件 corpus 或競品產生的 golden file。
- 逐行轉譯、API 一對一抄錄，或以競品內部型別名稱推導 OdfKit 實作。

## 證據流程

1. 在 `docs/semantic-coverage.json` 登錄語意族群、ODF 規格章節、實作、測試、
   互通證據與限制。
2. 研究記錄只描述輸入、可觀察輸出及規格對照；實作者依中立需求完成程式。
3. 每項能力必須具新建、讀取、修改、移除及 round-trip 證據；互通限制不得
   以跳過測試或空白說明掩蓋。
4. `eng/Test-SemanticCoverage.ps1` 會阻擋缺少來源、證據、限制或未完成操作的
   語意族群。
5. `semantic-api-provenance.json` 逐族群記錄規格來源、fixture 來源、黑箱觀察與
   實作邊界；其族群集合必須與 semantic coverage manifest 完全一致。

完整 coverage manifest 是可稽核完成狀態的單一事實來源；README 或支援矩陣的
文字宣稱不得高於該 manifest。
