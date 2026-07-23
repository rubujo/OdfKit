# ODF 1.4 Coverage Status

本文件摘要目前 ODF 1.4 規格覆蓋狀態，搭配 `docs/odf14-coverage-contract.md`
與 `docs/odf14-gap-audit.md` 使用。

## Current Status

- Part 3 schema：官方 ODF 1.4 內容 schema 元素與屬性已可由 provider 盤點。
- Part 2 package：ODF 1.0～1.4 manifest 與 ODF 1.2～1.4 dsig 已由官方 RNG
  產生版本化 metadata；文件驗證會另外檢查核心 XML 的 `office:version` 一致性。
- Typed DOM audit：`OdfTypedDomCoverage.Build()` 可輸出 schema element / attribute count、
  typed element count 與 attribute datatype coverage。
- Part 4 OpenFormula：已補 `EASTERSUNDAY` 標準名稱、`ISFORMULA`、`ISNONTEXT` 與 `CONVERT`。
- High-level facade：只追實務工作流，不追每個 schema 元素都有一級 C# API。

## Tracking Commands

```powershell
dotnet run --project tools/OdfKit.Cli --framework net10.0 -- typed-dom-coverage
dotnet test OdfKit.Tests/OdfKit.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~OdfCoverageContractTests"
```

## Known Boundaries

- Part 2 的高階 manifest／signature／encryption 物件模型仍由受控解析與寫入 API 維護；
  結構驗證則已使用官方 manifest／dsig RNG 產生的 metadata。
- 冷門元素優先保留在 typed DOM、round-trip preservation 與 validator 層。
- 完整 layout / rendering / calculation engine 不屬於 schema coverage 目標。

## 持續驗收契約

- Coverage audit 必須穩定輸出可比較摘要，且不得含未分類契約差異。
- 新增 facade 時同步補 cookbook 或 scenario test。
- 若官方 ODF schema 更新，先更新 generator 與 coverage status，再評估是否需要高階 API。
- `main` 必須維持本文件與 coverage contract 一致，不等待未來版本補必要缺口。
