---
title: 読み込みとストリーミングリーダーのセキュリティ制限
_lang: ja
translation_source: docs/security-limits.md
translation_source_sha256: 717f100dcc24a436ae8dd8c64601585f59320489dc452918dd8d97837e4fd8f0
---

# 読み込みとストリーミングリーダーのセキュリティ制限

> この翻訳は参考情報です。内容に相違がある場合は、正体字中国語 (`zh-TW`) の原文が優先されます。

パッケージ読み込みと `OdsStreamReader`／`OdtStreamReader` は信頼できない ZIP／XML 入力を処理します。Reader はドキュメント全体の DOM を作成しませんが、現在の行、
ノードのテキスト、ZIP の展開、および XML Reader に必要なバッファーは割り当てます。常駐メモリを
抑える設計であっても、入力サイズの影響を受けないわけではありません。

## コアパッケージの制限

`OdfDocument.Load`、各形式の `Load` facade、および `OdfPackage.Open` は `OdfLoadOptions` のリソース予算を共有します。

| 制限 | 既定値 | 保護目的 |
|---|---:|---|
| ZIP エントリ数 | 5,000 | 多数の小さなエントリによる CPU とメモリの枯渇を防止 |
| 1 エントリの展開サイズ | 500 MiB | 1 つの ZIP エントリの展開量を制限 |
| パッケージ全体の展開サイズ | 1 GiB | 全エントリの合計展開量を制限 |
| シーク不能な生入力サイズ | 1 GiB | ZIP 展開前のバッファー量を制限 |
| 1 XML 文書の文字数 | 64 MiB | XML 解析と DOM 構築のコストを制限 |

4 つの ZIP 制限は正の値が必要です。0 または負の値は直ちに `ArgumentOutOfRangeException` を発生させます。`MaxXmlCharactersInDocument = 0` のみが XML 文字数制限を無効化します。すべての XML Reader は外部 DTD と resolver を禁止する必要があります。新しい読み込み経路は `OdfLoadOptions` を再利用してください。パッケージと Flat XML の検証経路（`OdfPackageValidator`、`OdfFlatDocumentValidator`、profile ルールのスキャン）にも `MaxXmlCharactersInDocument` が適用されます。パッケージ検証は `package.LoadOptions`、Flat 検証は `OdfValidationOptions.LoadOptions`（省略時は `OdfLoadOptions` の既定値 64 MiB）を使用します。署名、タイムスタンプ、証明書失効データ、外部ネットワーク応答には、それぞれより小さい固有の制限があり、コアパッケージの制限で置き換えることはできません。内容ポリシーには `OdfPackageValidator`、`SanitizeMacros`、署名検証、または `pwsh eng/Test-OdfPolicy.ps1` を使用してください。

## ストリーミングリーダーの制限

| Reader | 制限 | 既定値 |
|---|---|---:|
| ODS | XML 文字数 | 64 MiB |
| ODS | 1 ワークシートあたりの行数 | 1,048,576 |
| ODS | 1 行あたりの列数 | 16,384 |
| ODS | 1 つの repeat 宣言 | 行 1,048,576、列 16,384 |
| ODS | 1 セルから抽出するテキスト | 16 MiB |
| ODT | XML 文字数 | 64 MiB |
| ODT | 返されるテキストノード数 | 1,000,000 |
| ODT | 1 ノードから抽出するテキスト | 16 MiB |

制限を超えると読み取りは失敗します。repeat を切り詰めて、一見完全なデータを返し続けることは
ありません。この失敗はリソース保護の結果として扱い、制限を無効にして自動的に再試行しないで
ください。

## ストリームの所有権

オプションの `LeaveOpen` の既定値は `false` です。`true` に設定した場合でも、Reader を破棄すると
XML エントリのストリームと ZIP Reader は閉じられますが、呼び出し元が指定した最外層のストリームは
開いたままになります。

## 信頼境界

信頼できないドキュメントには既定の制限を維持し、最初に package および schema の検証を実行して
ください。信頼できる大きなドキュメントを処理する必要がある場合は、個々の制限を引き上げられます。
ただし、XML またはテキストの上限を引き上げると、メモリおよび CPU DoS のリスクも増加します。
`MaxXmlCharactersInDocument = 0` が無効にするのは XML 文字数の制限だけであり、Reader のその他の
制限は引き続き有効です。

ODS／ODT Reader の options はプロパティ設定時に同じ規則を検証します。XML 制限は 0 を許可しますが、行、列、repeat、ノード、テキストの制限は 0 より大きい必要があります。

セキュリティ制限、検証、およびサニタイズはリスクを軽減するための措置であり、悪意のあるドキュメントに
対する絶対的な安全性を保証するものではありません。
