# 智慧財產與合規說明（IP Compliance）

本文件供**採用者法遵／採購盡職調查**與**貢獻者**使用。它不是律師意見，也不能替代管轄地法律諮詢。

相關來源稽核見 [provenance/README.md](provenance/README.md) 與
[clean-room-source-index.md](provenance/clean-room-source-index.md)。

## 1. 授權模型（複合授權）

| 範圍 | 授權 | 說明 |
|------|------|------|
| OdfKit 專案原創程式碼 | [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) | 專案盡量拋棄著作權；詳見根目錄 `LICENSE` |
| 建置與執行期相依套件 | 多為 MIT／BSD 等 | **不因 CC0 變成公有領域**；再散布時須保留各自 NOTICE／著作權聲明 |
| OASIS ODF RELAX NG schema | OASIS Copyright | 置於 `tools/OdfSchemaGenerator/schemas/`；詳見 `THIRD-PARTY-NOTICES.md` |
| Corpus／Collaboration fixture | 各 fixture 的 `license` 欄 | 見 `docs/corpus-manifest.md` 與各 `manifest.json` |

**重要：** 分發含 OdfKit 及其相依的應用程式或套件時，必須同時滿足：

1. 專案 `LICENSE`（CC0）對原創碼的效力；以及  
2. [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) 所列第三方授權義務。

不可對外宣稱「整包產物皆為公有領域」。

## 2. 權利人與 AI 產製聲明

- README 已聲明：公開之原始碼、文件、範例與測試內容，目前多為使用 AI 工具撰寫、整理或產製。  
- CC0 的 Affirmer 必須對其拋棄之權利有處分權。貢獻者提交前應確認自己有權將內容以專案授權納入（見下文 DCO）。  
- 部分法域對純機器產出著作權之認定不同；採用者若需「明確版權人＋侵權賠償承諾」，應評估商業替代方案或另行洽詢支援契約——**本開源專案預設不提供商業 indemnity**。

## 3. Clean-room 與禁止來源

高風險模組（OpenFormula 評估、schema pattern 驗證、OpenPGP 加密、JSON Collaboration、受控格式轉換等）的權威來源、允許行為與**不可複製來源**列於
[clean-room-source-index.md](provenance/clean-room-source-index.md)。

摘要原則：

- **允許**：OASIS／ISO／RFC／W3C 等公開規格、公開 wire shape、可再散布 reference JSON／fixture、行為對照與自建 regression。  
- **禁止**：複製 LibreOffice C++、Java ODF Toolkit、Apache POI、NPOI 或商用 SDK 之原始碼；以反組譯閉源二進位作為實作來源。  
- **可相容、非移植**：JSON Collaboration 僅為 extension 範圍內的 TDF 公開 operations 相容子集，不是 Toolkit 原始碼移植。

## 4. 標準實作與商標

- ODF／OpenFormula／OOXML 等為開放或公開文件格式；依規格實作 reader／writer／validator 屬互通常態。  
- 可描述性使用「OpenDocument」「ODF」「LibreOffice 相容測試」等字樣。  
- **不得**暗示本專案為 OASIS、The Document Foundation、LibreOffice 或 Apache 之官方專案、認證或背書。  
- 「對標 ODF Toolkit」指能力與測試證據之對照，**不是**官方移植或聯名產品。

## 5. 貢獻者開發者來源證明（DCO）

提交程式碼或大幅文件時，貢獻者應能聲明（Developer Certificate of Origin 風格）：

1. 貢獻為本人創作，或本人有權以專案授權提交；  
2. 未故意納入無權再散布的第三人原始碼；  
3. 若依公開規格或公開文件實作，已遵守 clean-room 來源索引；  
4. 新增第三方相依時，已更新 `THIRD-PARTY-NOTICES.md` 與必要的套件中繼資料。

建議在 commit 訊息或 PR 描述中使用 `Signed-off-by: Name <email>`（本專案 git 規範亦要求 GPG 簽署）。

## 6. 採用者盡職調查清單

| 項目 | 建議動作 |
|------|----------|
| 授權 | 閱讀 `LICENSE` 與 `THIRD-PARTY-NOTICES.md`；SBOM／授權掃描納入 CI |
| 版本 | 目前為 `0.x`；相容性承諾見 `CHANGELOG` 與 [version-delivery.md](version-delivery.md) |
| 功能邊界 | 以 [odf-format-support.md](odf-format-support.md) 與測試證據為準，勿僅依賴行銷用語 |
| 非目標 | 見 [udx-non-goals.md](udx-non-goals.md)（完整排版引擎、樞紐重算等） |
| 安全 | 使用 `OdfLoadOptions` 資源上限；對不可信輸入跑 `Validate`／sanitize |
| 來源 | 審閱 `docs/provenance/`；必要時對高風險目錄做與上游的相似掃描 |
| 支援 | 開源專案無 SLA；關鍵系統應有備援與自行維運計畫 |

## 7. 漏洞與安全回報

本專案目前未提供公開問題追蹤或安全問題的私密通報管道。在維護者公告正式管道前，專案不宣稱
可接收、追蹤或依服務等級處理安全通報。未來若開放公開問題追蹤，仍不應在公開內容中附上完整
利用細節。安全問題與「授權／侵權」議題應分開處理。

## 8. 相關文件

- [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md)  
- [provenance/README.md](provenance/README.md)  
- [Clean-room 來源索引](provenance/clean-room-source-index.md)  
- [ODF Toolkit 對標線](odf-toolkit-parity.md)  
- [Foreign 擴充政策](foreign-extension-policy.md)  
- [Corpus Manifest 規則](corpus-manifest.md)  
