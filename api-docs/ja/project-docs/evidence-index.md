---
title: 機能に関する表明と根拠の索引
_lang: ja
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
---

# 機能に関する表明と根拠の索引

> この翻訳は参考情報です。機械可読の識別子と値は翻訳していません。

この索引では、機能を相互に推論できない 3 つの次元に分けています。機械可読のソースは
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json) です。CI では、表明 ID、
根拠のパス、および制限事項の説明を検査します。

| 表明 | 形式 | 次元 | レベル | 制限事項の概要 |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | パッケージを往復して読み書きできることは、数式の再計算やスプレッドシートの完全なセマンティクスを意味しません。 |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | 保存済みの値と数式を読み取りますが、数式は再計算しません。 |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | レイアウトエンジンやレンダリングエンジンは提供しません。 |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | ODP は DOM およびパッケージとして読み込まれます。ストリーミングスライド API の提供は表明していません。 |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | SmartArt のレイアウトエンジンやピクセル単位のレンダリングエンジンは実装していません。 |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | 特定バージョンの LibreOffice による実測は、すべてのオフィススイートでピクセル単位の結果が一致することを意味しません。 |

`PackageFidelity` はパッケージを安全に処理できるかだけを示します。`SemanticApiDepth` は API が
ドキュメントのセマンティクスをどこまで理解して変更できるかを示します。`InteropEvidence` は、
実際にテストした外部ソフトウェアとバージョンを示します。いずれか 1 つの次元で最高レベルに
達していても、他の 2 つの次元の代わりにはなりません。

4 つの主要形式について、セマンティック機能群、CRUD 操作、規格の該当箇所、実装、テスト、
相互運用性の根拠、および制限事項をまとめた唯一の信頼できる情報源は、
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json) です。
`eng/Test-SemanticCoverage.ps1` は、不完全な表明を CI で拒否します。クリーンルーム方式の情報源の
境界については、
[`provenance/semantic-api-clean-room.md`](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/semantic-api-clean-room.md)
を参照してください。
