---
title: 読み込みとストリーミングリーダーのセキュリティ制限
_lang: ja
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
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

## その他のリソースおよび出力の保護

パッケージおよびストリーミングリーダーの制限に加えて、次の固定制限も信頼できない入力から保護します。これらの値は現在コード内の固定定数であり、`OdfLoadOptions` やオプションオブジェクトではまだ設定できません。制限を引き上げたり解除したりする前に、メモリとスタックへの影響を評価してください。

| 項目 | 制限 | 超過時の動作 |
|---|---|---|
| ZIP エントリの実際の展開サイズ | ヘッダーで宣言された非圧縮サイズを超えてはならない（ファイルパス読み込みの MMF パス） | `SecurityException` |
| ZIP セントラルディレクトリの破損または ZIP64 | MMF 高速パスは ZIP64 をサポートせず、完全に解析できないレコードを黙ってスキップしてはならない | `ZipArchive` による検証と読み取りにフォールバックする |
| XML 要素のネスト深度 | 256 階層（`OdfXmlReader.MaxElementDepth`）。DOM 読み込み、Flat ODF 読み込み、プロファイル規則の検証、RDF 解析に適用される | 読み込みは `SecurityException` をスローし、検証は `ODF0303` または `ODF0301` を報告する |
| 数式解析のネスト深度 | 括弧、関数引数、インライン配列、連続する前置演算子のそれぞれ 256 階層 | `InvalidOperationException` |
| 数式の演算子ノード総数 | 4,096（`FormulaParser.MaxOperatorNodes`）。二項、単項、パーセント、参照の各演算子の合計。連鎖した数式は左に深い木になり、評価とシリアル化は階層ごとに再帰するため、この上限により通常のスレッドスタックで足りることを保証する | `InvalidOperationException` |
| 数式の再帰に対するスタック余裕 | 解析、評価、範囲取得、シリアル化は、再帰の各階層に入る前に残りスタック（`RuntimeHelpers.EnsureSufficientExecutionStack`）を確認する。128〜256 KB の小さなスタックのスレッドでも、上限に近い数式でプロセスがクラッシュしない | `InsufficientExecutionStackException`。`EvaluateFormulas` はこれを数式評価例外または `#VALUE!` に変換する |
| 数式の文字列結果の長さ | 1,048,576 文字（`&`、関数の結果、`SUBSTITUTE`、`REPT`） | `#VALUE!` を返す |
| 引数がループ上限になる数式関数 | `BINOMDIST` 累積 100,000、`CRITBINOM`・`HYPGEOMDIST` 累積 100,000、`POISSON` 累積 1,000,000、`DB`・`DDB`・`VDB`・`CUMIPMT` 期間 1,000,000 | `#NUM!` を返す |
| スプレッドシートの行／列インデックス | 行 1,048,575、列 16,383（`OdfSpreadsheetLimits`） | `ArgumentOutOfRangeException` |
| ドキュメントの追加 | ドキュメントを自分自身に追加してはならない | `ArgumentException` |
| Collaboration `addColumns` | `OdtOperationSafetyOptions` の列数とセル総数の制限を受ける | 安全上限を記録し、その操作をスキップする |
| グラフのフォールバック画像 | 1 辺 4,096 px | 上限に丸められる |
| LibreOffice の変換形式 | コロンより前の拡張子部分は空であってはならず、`..`、NUL、CR、LF を含んではならない | `ArgumentException` |
| SPARQL クエリ | `SERVICE` 句は許可されない（クエリエンジンが任意のエンドポイントへネットワーク要求を送るのを防ぐ） | `ArgumentException` |

## 信頼境界

信頼できないドキュメントには既定の制限を維持し、最初に package および schema の検証を実行して
ください。信頼できる大きなドキュメントを処理する必要がある場合は、個々の制限を引き上げられます。
ただし、XML またはテキストの上限を引き上げると、メモリおよび CPU DoS のリスクも増加します。
`MaxXmlCharactersInDocument = 0` が無効にするのは XML 文字数の制限だけであり、Reader のその他の
制限は引き続き有効です。

ODS／ODT Reader の options はプロパティ設定時に同じ規則を検証します。XML 制限は 0 を許可しますが、行、列、repeat、ノード、テキストの制限は 0 より大きい必要があります。

セキュリティ制限、検証、およびサニタイズはリスクを軽減するための措置であり、悪意のあるドキュメントに
対する絶対的な安全性を保証するものではありません。
