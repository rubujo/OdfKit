# 互通與風險工作流

OdfKit 將規格驗證、實務風險提示與真實應用程式互通分成三個邊界：

| 邊界 | 入口 | 保證 |
| --- | --- | --- |
| ODF 結構與 profile | `Validate`／`ValidateAsync`、CLI `validate` | package、schema metadata、pattern 與 profile 問題報告 |
| 實務相容風險 | `OdfPracticalCompatibilityValidator` | 依 LibreOffice、Microsoft Office ODF 或 portable editing profile 提示風險 |
| 真實渲染／轉換 | `OdfKit.Extensions.Rendering` | 透過已安裝的 LibreOffice backend 執行列明轉換 |

實務相容性報告不取代 schema 驗證，也不保證像素級一致。完整 RELAX NG baseline 可由 CLI 明確
啟用外部 ODF Validator。LibreOffice workflow 會驗證目前穩定版本與雙 TFM；找不到 backend 時，
只有明確要求 `RequireLibreOffice` 的驗收路徑才應失敗。

獨立 ODC／OTC／FODC、OTF／FDF、ODI／OTI／FODI 等格式若不被 LibreOffice 當作主文件開啟，
改以 package、schema 與 round-trip 證據驗收，且不得將原樣複製或誤判輸出算成轉換成功。

相關文件：[LibreOffice 互通矩陣](../libreoffice-interop-matrix.md)、
[渲染後端部署](../rendering-backend-deployment.md)、[ODF Toolkit 對標線](../odf-toolkit-parity.md)。
