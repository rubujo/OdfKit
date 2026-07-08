# OdfKit API Reference Roadmap

OdfKit 的文件分層以實務工作流為中心，不把 ODF 1.4 全規格元素清單等同於高階 API 文件。
目標是讓 C# / .NET 使用者能快速找到「怎麼做」，再進一步查到相容性限制與底層保留策略。

## 文件分層

| 層級 | 目的 | 主要文件 |
| --- | --- | --- |
| Quick Start | 讓新使用者在數分鐘內建立 ODT / ODS / ODP 與常見圖表 | `README.md`、`docs/cookbook.md` |
| Scenario Cookbook | 任務導向範例，涵蓋資料表、模板、圖表、影像與互通檢查 | `docs/cookbook.md` |
| API Reference | 依文件類型整理 facade、options、report 與 diagnostics | 本文件追蹤，後續拆分到 `docs/reference/` |
| Compatibility Notes | 記錄 LibreOffice / Microsoft Office / portable editing 風險 | `docs/odf-format-support.md`、`docs/rendering-backend-deployment.md` |
| Specification Coverage | 追蹤 schema / typed DOM / validator 覆蓋，而不是高階 facade 100% | `docs/odf14-coverage-roadmap.md`、`docs/odf14-coverage-status.md` |

## 下一批 Reference 章節

- **Spreadsheet data workflows**：`WriteObjects`、`ReadObjects`、`ValidateObjectBinding`、
  `UpdateObjects`、`UpsertObjects`、`OdfObjectColumnMap`、`OdfObjectBindingReport`。
- **Chart workflows**：standalone / embedded chart parity、bubble、stock、3D、marker、axis format、
  practical compatibility validator。
- **Template workflows**：scalar token、collection expansion、image placeholder、unknown placeholder policy、
  dry run 與 `OdfTemplateBindReport`。
- **Interop workflows**：`OdfPracticalCompatibilityValidator` profile、LibreOffice optional test script、
  不承諾像素級一致的邊界。

## 文件驗收規則

- Cookbook 範例必須使用實際存在的 public API 名稱。
- 新增 public options / report / diagnostic code 時，同步補 scenario 或 reference 說明。
- Reference docs 說明實務限制，例如公式位移只支援常見 A1 參照，不宣稱完整 OpenFormula AST rewrite。
- NuGet 發佈與套件上架資訊不納入本專案文件目標。
