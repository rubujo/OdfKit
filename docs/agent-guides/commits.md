# Git 提交規範

只有使用者要求建立 commit 時才需要讀取並套用本文件。

- 遵循 Conventional Commits v1.0.0。
- 提交訊息不得只有單行；必須包含主旨與內文，必要時再加腳註。
- 主旨不超過 50 個字元，以 `feat`、`fix`、`docs`、`refactor` 等類型開頭，結尾不加句點。
- 內文每行不超過 72 個字元，精簡說明變更原因與細節。
- 使用正體中文臺灣地區用語，只有必要的技術名稱保留英文。
- 所有 commit 必須經 GPG 簽署。非互動環境執行前確認 `gpg-agent` 已快取金鑰密碼，
  避免簽署程序停滯。

提交後可使用下列命令稽核簽署：

```powershell
pwsh eng/Test-GpgSignatures.ps1
```
