---
title: 知的財産とコンプライアンス
_lang: ja
translation_source: docs/ip-compliance.md
translation_source_sha256: bccec797a382b4bf3fae941a34d0dd406fdc97cac84a38d6c20dc09109164b6f
---

# 知的財産とコンプライアンス (IP Compliance)

> この翻訳は参考情報です。法律上の助言ではなく、適用される法域の専門家への相談に代わるものではありません。

この文書は、**導入者によるコンプライアンスおよび調達のデューデリジェンス**と、
**コントリビューター**のために提供されます。法律上の助言ではなく、適用される法域の法律に
関する相談に代わるものではありません。

関連する情報源の監査については、
[provenance/README.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md) および
[clean-room-source-index.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md)
を参照してください。

## 1. ライセンスモデル (複合ライセンス)

| 対象 | ライセンス | 説明 |
|---|---|---|
| OdfKit プロジェクト独自のコード | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) | プロジェクトは可能な限り著作権を放棄します。ルートの `LICENSE` を参照してください |
| ビルド時および実行時の依存パッケージ | 主に MIT、BSD など | **CC0 によってパブリックドメインになることはありません**。再配布時には各 NOTICE および著作権表示を保持する必要があります |
| OASIS ODF RELAX NG schema | OASIS Copyright | `tools/OdfSchemaGenerator/schemas/` にあります。`THIRD-PARTY-NOTICES.md` を参照してください |
| Corpus および Collaboration のフィクスチャ | 各フィクスチャの `license` フィールド | `docs/corpus-manifest.md` および各 `manifest.json` を参照してください |

**重要:** OdfKit とその依存関係を含むアプリケーションまたはパッケージを配布する場合は、次の
両方を満たす必要があります。

1. 独自コードに対するプロジェクトの `LICENSE` (CC0) の効力。
2. [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md)
   に記載された第三者ライセンスの義務。

「成果物全体がパブリックドメインである」と対外的に表明してはいけません。

### CC0 における特許権および商標権の範囲

CC0 1.0 第 4(a) 項により、特許権および商標権は許諾も放棄もされません。OdfKit は特許ライセンス、
非侵害保証、特許調査、補償を提供しません。採用者は自ら確認してください。相違がある場合は
[CC0 の法的条項](https://creativecommons.org/publicdomain/zero/1.0/legalcode) が優先します。

## 2. 権利者と AI による生成に関する声明

- README では、公開されているソースコード、ドキュメント、例、およびテストの多くが、AI ツールを使用して作成、整理、または生成されたことを明示しています。
- CC0 の Affirmer は、放棄する権利を処分できる立場でなければなりません。コントリビューターは、提出前に、その内容をプロジェクトのライセンスの下で提供する権利があることを確認する必要があります。後述の DCO を参照してください。
- 機械のみが生成した内容の著作権に関する判断は法域によって異なります。「明確な著作権者と侵害に対する補償の約束」が必要な導入者は、商用の代替製品を評価するか、別途サポート契約を相談してください。**このオープンソースプロジェクトは、既定では商用の補償を提供しません**。

## 3. クリーンルーム方式と禁止される情報源

OpenFormula の評価、schema pattern の検証、OpenPGP 暗号化、JSON Collaboration、管理された
形式変換など、リスクの高いモジュールについて、権威ある情報源、許可される行為、および
**コピーしてはならない情報源**を
[clean-room-source-index.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md)
に記載しています。

原則の概要:

- **許可:** OASIS、ISO、RFC、W3C などの公開規格、公開された wire shape、再配布可能な reference JSON およびフィクスチャ、動作の比較、独自に作成した回帰テスト。
- **禁止:** LibreOffice C++、Java ODF Toolkit、Apache POI、NPOI、または商用 SDK のソースコードのコピー。逆コンパイルしたクローズドソースのバイナリを実装の情報源として使用すること。
- **互換性はあるが移植ではない:** JSON Collaboration は、拡張機能の範囲内で TDF が公開する operations と互換性のあるサブセットにすぎず、Toolkit ソースコードの移植ではありません。

## 4. 規格の実装と商標

- ODF、OpenFormula、OOXML などは、オープンな、または一般公開されたドキュメント形式です。規格に従って reader、writer、validator を実装することは、通常の相互運用性の取り組みです。
- 「OpenDocument」、「ODF」、「LibreOffice 互換性テスト」などの語句は、説明目的で使用できます。
- このプロジェクトが OASIS、The Document Foundation、LibreOffice、または Apache の公式プロジェクト、認証、もしくは推奨を受けた製品であるかのように**示唆してはいけません**。
- 「ODF Toolkit との比較」とは機能およびテストの根拠を比較することであり、**公式な移植や共同ブランド製品ではありません**。

## 5. コントリビューター向け Developer Certificate of Origin (DCO)

コードまたは大幅なドキュメントを提出するコントリビューターは、Developer Certificate of
Origin の形式に従って、次のことを表明できる必要があります。

1. コントリビューションが本人の著作物であるか、プロジェクトのライセンスの下で提出する権利があること。
2. 再配布する権利のない第三者のソースコードを故意に含めていないこと。
3. 公開規格または公開文書に基づいて実装した場合、クリーンルーム方式の情報源索引に従ったこと。
4. 第三者の依存関係を追加した場合、`THIRD-PARTY-NOTICES.md` と必要なパッケージメタデータを更新したこと。

commit メッセージまたは PR の説明に `Signed-off-by: Name <email>` を含めることを推奨します。
プロジェクトの Git 規則では GPG 署名も必須です。

## 6. 導入者向けデューデリジェンスチェックリスト

| 項目 | 推奨される対応 |
|---|---|
| ライセンス | `LICENSE` と `THIRD-PARTY-NOTICES.md` を読み、SBOM とライセンススキャンを CI に組み込む |
| バージョン | 現在は `0.x` です。互換性に関する方針は `CHANGELOG` と [version-delivery.md](https://github.com/rubujo/OdfKit/blob/main/docs/version-delivery.md) を参照する |
| 機能の境界 | [odf-format-support.md](https://github.com/rubujo/OdfKit/blob/main/docs/odf-format-support.md) とテストの根拠を基準とし、宣伝上の表現だけに依存しない |
| 対象外 | 完全なレイアウトエンジンや、pivot cache／slicer などオフィススイートの対話型機能については [udx-non-goals.md](https://github.com/rubujo/OdfKit/blob/main/docs/udx-non-goals.md) を参照する |
| セキュリティ | `OdfLoadOptions` のリソース上限を使用し、信頼できない入力には `Validate` とサニタイズを実行する |
| 情報源 | `docs/provenance/` を確認し、必要に応じてリスクの高いディレクトリを上流プロジェクトと比較して類似性を検査する |
| サポート | オープンソースプロジェクトには SLA がありません。重要なシステムには冗長性と独自の保守計画を用意する |

## 7. 脆弱性とセキュリティに関する報告

このプロジェクトは現在、公開の issue tracker や、セキュリティ問題を非公開で報告するための
チャネルを提供していません。メンテナーが正式なチャネルを発表するまで、セキュリティ報告を
受信、追跡、またはサービスレベルに従って処理できるとは表明しません。将来公開の tracker を
開設した場合でも、悪用方法の完全な詳細を公開情報に記載してはいけません。セキュリティ問題と
ライセンスまたは侵害に関する問題は、分けて扱う必要があります。

## 8. 関連文書

- [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md)
- [provenance/README.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md)
- [クリーンルーム方式の情報源索引](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md)
- [ODF Toolkit との比較](https://github.com/rubujo/OdfKit/blob/main/docs/odf-toolkit-parity.md)
- [外部拡張機能のポリシー](https://github.com/rubujo/OdfKit/blob/main/docs/foreign-extension-policy.md)
- [Corpus Manifest の規則](https://github.com/rubujo/OdfKit/blob/main/docs/corpus-manifest.md)
