---
title: OdfKit 使用、合規、安全與證據指南
_lang: zh-TW
---

# 使用、合規、安全與證據指南

## API 文件範圍

API 參考文件由 `net10.0` 公開組件與 XML 文件產生。手寫核心 API 與公開擴充套件會逐頁呈現；大量由結構描述產生的 `OdfKit.DOM` 包裝型別，則由雙目標框架（TFM）公開 API 基準與具型別 DOM 涵蓋率管理。API 成員摘要目前提供英文與正體中文，不代表其餘語系的成員文件已完成翻譯。

## 授權與 AI 產製

OdfKit 原創程式碼與網站原創文件採 CC0-1.0 Universal。第三方套件、結構描述、工具與測試資料維持各自授權。本專案公開內容使用 AI 工具撰寫、整理或產製；本站內容不構成法律意見，亦不提供服務等級協定（SLA）或商業賠償保障。OdfKit 並非 OASIS、The Document Foundation、LibreOffice 或 Apache 的官方專案，亦未受其認證或背書。

## 安全與互通邊界

處理不可信文件時，應保留讀取器與封裝的資源限制，並執行適當的驗證或內容清理。這些措施可降低風險，但不保證惡意文件絕對安全。符合結構描述、往返儲存成功或通過特定 LibreOffice 版本測試，均不代表所有辦公套件皆能達到像素級一致。

## 能力與證據

能力分為 `PackageFidelity`、`SemanticApiDepth` 與 `InteropEvidence`，三者不能互相推導。效能數字必須附上提交版本、執行環境、測試環境與可重現方法；目前效能預算仍在累積固定樣本。

- [開啟 API 參考文件](xref:OdfKit)
- [能力宣稱與證據索引](../../docs/evidence-index.md)
- [安全限制](../../docs/security-limits.md)
- [智慧財產與合規](../../docs/ip-compliance.md)
- [授權](../articles/license.md)
- [第三方聲明](../../THIRD-PARTY-NOTICES.md)
