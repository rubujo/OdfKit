---
title: 第三者に関する通知
_lang: ja
translation_source: THIRD-PARTY-NOTICES.md
translation_source_sha256: 1f6420f237bd28ad1fd71200b41661ef336448631262196c5601841160e7b2ce
---

# 第三者に関する通知

> この翻訳は参考情報です。パッケージ名とライセンス名は原文のまま記載しています。

OdfKit プロジェクトは [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) の下で提供されます。以下のビルド時および実行時の依存パッケージには、それぞれのライセンスが引き続き適用されます。

| パッケージ | 用途 | ライセンス |
|---|---|---|
| [BouncyCastle.Cryptography](https://github.com/bcgit/bc-csharp) | 暗号化、ハッシュ、およびキー導出アルゴリズムのサポート | [MIT](https://github.com/bcgit/bc-csharp/blob/master/LICENSE.html) |
| [CommunityToolkit.HighPerformance](https://github.com/CommunityToolkit/dotnet) | 高性能なメモリおよびバッファーツール | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) |
| [System.Security.Cryptography.Xml](https://github.com/dotnet/runtime) | XML デジタル署名の処理 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Security.Cryptography.Pkcs](https://github.com/dotnet/runtime) | PKCS7 / CMS 署名の処理 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Sylvan.Data.Csv](https://github.com/MarkPelf/Sylvan) | ODS ファイルの CSV インポートおよびエクスポート | [MIT](https://github.com/MarkPelf/Sylvan/blob/main/LICENSE) |
| [CSharpMath](https://github.com/verybadcat/CSharpMath) | LaTeX ↔ MathML 数式変換エンジン | [MIT](https://github.com/verybadcat/CSharpMath/blob/master/LICENSE) |
| [System.Text.Json](https://github.com/dotnet/runtime) | JSON シリアル化。コアパッケージと OdfKit.Extensions.Collaboration の netstandard2.0 ターゲットで使用 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Numerics.Tensors](https://github.com/dotnet/runtime) | 数式集計関数のベクトル化された数値演算。net10.0 ターゲットのみ | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.IO.Hashing](https://github.com/dotnet/runtime) | CRC-32 チェックサムの計算。net10.0 ターゲットのみ | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [System.Memory](https://github.com/dotnet/runtime) / [System.Buffers](https://github.com/dotnet/runtime) / [System.Threading.Tasks.Extensions](https://github.com/dotnet/runtime) / [Microsoft.Bcl.AsyncInterfaces](https://github.com/dotnet/runtime) / [Microsoft.Bcl.HashCode](https://github.com/dotnet/runtime) / [System.Text.Encoding.CodePages](https://github.com/dotnet/runtime) | net10.0 に組み込まれている型と API を補う、netstandard2.0 プラットフォーム互換性のサポート | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Microsoft.Win32.Registry](https://github.com/dotnet/runtime) | Windows EUDC レジストリソースを解決するための netstandard2.0 互換性サポート | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [Markdig](https://github.com/xoofx/markdig) | OdfKit.Extensions.Html で使用する Markdown AST 解析バックエンド | [BSD-2-Clause](https://github.com/xoofx/markdig/blob/master/license.txt) |
| [SkiaSharp](https://github.com/mono/SkiaSharp) / [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | OdfKit.Extensions.Imaging で使用するクロスプラットフォームの画像描画と文字組版 | [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md) |
| [ScottPlot](https://github.com/ScottPlot/ScottPlot) | OdfKit.Extensions.Imaging で使用するメモリ内グラフ描画とフォールバック画像の可視化 | [MIT](https://github.com/ScottPlot/ScottPlot/blob/main/LICENSE) |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) / [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) | OdfKit.Extensions.Ooxml で使用する Excel などの OOXML 形式との統合およびインポートとエクスポート | [MIT](https://github.com/ClosedXML/ClosedXML/blob/master/LICENSE) / [MIT](https://github.com/dotnet/Open-XML-SDK/blob/main/LICENSE) |
| [PDFsharp-MigraDoc](https://github.com/empira/PDFsharp) | OdfKit.Extensions.Pdf で使用する PDF の処理、レイアウト、および描画拡張 | [MIT](https://github.com/empira/PDFsharp/blob/master/LICENSE) |
| [dotNetRdf.Core](https://github.com/dotnetrdf/dotnetrdf) | OdfKit.Extensions.Rdf で使用する RDF グラフと SPARQL クエリのブリッジ | [MIT](https://github.com/dotnetrdf/dotnetrdf/blob/master/License.txt) |
| [Microsoft.CSharp](https://github.com/dotnet/runtime) | OdfKit.Extensions.Ooxml の netstandard2.0 ターゲットで使用する `dynamic` 型の実行時バインド | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [OASIS OpenDocument Relax-NG Schemas](https://www.oasis-open.org/committees/office/) | tools/OdfSchemaGenerator/schemas/ に配置された ODF 1.1 / 1.2 / 1.3 / 1.4 XML 構造の検証およびコード生成 | [OASIS Copyright](https://www.oasis-open.org/committees/office/ipr.php) |

上記の依存パッケージを含むアプリケーションを配布する場合は、各パッケージのライセンス条件に従って、必要なライセンスおよび著作権表示を保持してください。

OASIS のスキーマファイル (Relax-NG Schemas) に関する著作権表示:

## WebFont テストの依存関係

- Noto Sans Arabic / Devanagari / CJK — SIL Open Font License 1.1
- Noto Color Emoji — SIL Open Font License 1.1
- IPAmj Mincho — IPA Font License Agreement v1.0
- CNS 11643 fonts — Government Open Data License v1 / OFL-1.1

* Copyright (c) OASIS Open 2021. All Rights Reserved.
* 詳細な知的財産権ポリシーについては、各 schema ファイルのヘッダーおよび [OASIS IPR Policy](https://www.oasis-open.org/committees/office/ipr.php) を参照してください。
