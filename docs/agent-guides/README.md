# Agent 按需開發指南

本目錄承接不需要在每次任務載入的開發細節。根層 `AGENTS.md` 負責全域契約與路由；
Agent 只在修改相應範圍時讀取下列文件。

| 文件 | 適用範圍 |
| --- | --- |
| [程式碼與 XML 文件風格](code-style.md) | 手寫 C#、公開 API 文件與中文註解 |
| [測試開發](testing.md) | xUnit 測試、非同步測試與 analyzer 驗證 |
| [Git 提交](commits.md) | 使用者要求建立 commit 時 |

領域規範繼續以既有文件為準，例如 `docs/maintainability.md`、
`OdfKit/PublicAPI/README.md`、`OdfKit/Compliance/i18n/README.md` 與
`docs/ci-cd.md`。
