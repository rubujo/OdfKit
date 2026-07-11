---
title: OdfKit 使用、合規、安全與證據指南
_lang: zh-TW
---

# 使用、合規、安全與證據指南

## API 文件範圍

API Reference 由 `net10.0` 公開組件與 XML 文件產生。手寫核心 API 與公開擴充套件會逐頁呈現；大量 schema-generated `OdfKit.DOM` wrapper 由雙 TFM Public API baseline 與 Typed DOM coverage 管理。API 成員摘要目前提供英文與正體中文，不代表其餘語系成員已完成翻譯。

## 授權與 AI 產製

OdfKit 原創程式碼與網站原創文件採 CC0-1.0 Universal。第三方套件、schema、工具與 fixture 維持各自授權。本專案公開內容使用 AI 工具撰寫、整理或產製；本站不是法律意見，不提供 SLA 或商業 indemnity。OdfKit 並非 OASIS、The Document Foundation、LibreOffice 或 Apache 的官方或背書專案。

## 安全與互通邊界

處理不可信文件時應保留 Reader 與 package 資源限制，並執行驗證或 sanitize。這些措施降低風險，但不保證惡意文件絕對安全。Schema valid、round-trip 或特定 LibreOffice 版本測試不代表所有辦公套件皆能像素級一致。

## 能力與證據

能力分為 `PackageFidelity`、`SemanticApiDepth` 與 `InteropEvidence`，三者不能互相推導。效能數字必須附提交、runtime、環境與可重現方法；目前效能預算仍在累積固定樣本。

- [開啟 API Reference](xref:OdfKit)
- [能力宣稱與證據索引](https://github.com/rubujo/OdfKit/blob/main/docs/evidence-index.md)
- [安全限制](https://github.com/rubujo/OdfKit/blob/main/docs/security-limits.md)
- [智慧財產與合規](https://github.com/rubujo/OdfKit/blob/main/docs/ip-compliance.md)
