# DOM Generated（不可手改）

此目錄內 `*.g.cs` 由 `tools/OdfSchemaGenerator` 依 OASIS ODF schema 產生。

- **禁止**手動編輯（含 search-replace、批次修 ctor）。
- 要改公開形狀：只改 `tools/OdfSchemaGenerator/DomWrappersCSharpWriter.cs`，再重產。
- 重產：`pwsh eng/Generate-OdfSchemaProvider.ps1`
- 僅 DOM：`pwsh eng/Generate-OdfSchemaProvider.ps1 -ManifestPath tools/OdfSchemaGenerator/oasis-odf14-dom-wrappers.json`
- 覆蓋率：`pwsh eng/Test-OdfTypedDomCoverage.ps1`
