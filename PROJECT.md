# Project: OdfKit Phase 2

## Architecture
- Namespace structure: `OdfKit.Text` (ODT), `OdfKit.Spreadsheet` (ODS), `OdfKit.Presentation` (ODP), `OdfKit.Drawing` (ODG), `OdfKit.Formula` (OpenFormula).
- DOM Layer: wrappers located in `OdfKit/DOM/Generated/GeneratedDomWrappers.g.cs` (generated) and partial classes beside it.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|---|---|---|---|
| 1 | M7. ODT (Text) Completeness | TOC, track changes, CJK layout, MathML preservation | None | DONE |
| 2 | M8. ODS & OpenFormula | Named range, sorting/filtering, pivot table structure, Formula Evaluator (F3-F5) | None | DONE |
| 3 | M9. ODP/ODG (Presentation) | Slide layouts, transitions, SMIL timing, embedded chart & formula packages | None | DONE |
| 4 | M12. Final E2E and Audit | Verification of all requirements, warning/compile check, forensic audit | M7, M8, M9 | DONE |

## Interface Contracts
### ODT ↔ ODS ↔ ODP/ODG
- Shared package and styling base classes.
- MathML formula insertion utilizes `OdfPackage` entry manipulation.
