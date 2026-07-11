# DocFX 正式文件翻譯工作流程

`api-docs/translations.json` 是翻譯契約的單一事實來源。`zh-TW` 是唯一權威語系；不得從其他
譯文轉譯，也不得以翻譯覆寫根目錄授權或第三方法律原文。

Agent 更新流程：

1. 執行 `pwsh eng/Test-ApiDocsTranslations.ps1 -Json` 取得 `missing` 或 `stale` 佇列。
2. 由 manifest 的 `source` 翻譯到 `destination`，保留 Markdown 結構、URL、程式碼、數值及
   `requiredTokens`。
3. 每份譯文 front matter 必須包含正確的 `_lang`、`translation_source` 與
   `translation_source_sha256`。
4. 法律頁必須說明譯文只供參考，權威來源及第三方原始法律文字優先。
5. 執行 `pwsh eng/Test-ApiDocsTranslations.ps1 -FailOnIssues`，再執行
   `pwsh eng/Build-ApiDocs.ps1 -NoRestore -SkipProjectBuild`。

權威來源異動時，先更新 manifest 的 `sourceSha256`，再更新全部受影響譯文。CI 會拒絕來源
雜湊過期、必要 token 遺失或導覽仍連向其他語系的變更。
