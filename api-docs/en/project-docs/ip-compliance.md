---
title: Intellectual property and compliance
_lang: en
translation_source: docs/ip-compliance.md
translation_source_sha256: 02ec7aa4649cae3c94cd515424f1c787d21909239c98c0fedffca85214a7eb6c
---

# Intellectual property and compliance

> Translation notice: this page translates the authoritative Traditional Chinese (Taiwan) project
> document for information only. It is not legal advice. The original legal texts prevail.

This document supports adopter compliance and procurement due diligence as well as contributors. See
the [provenance overview](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md) and
[clean-room source index](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md).

## 1. Composite licensing model

Original OdfKit code uses [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/).
Build and runtime dependencies retain their MIT, BSD, or other licenses. OASIS ODF RELAX NG schemas
retain OASIS copyright, and corpus or collaboration fixtures retain the licenses recorded in their
manifests. Redistribution must satisfy both the repository `LICENSE` and
[THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md). Do not claim
that the entire distributed product is public domain.

### Patent and trademark boundaries of CC0

CC0 covers copyright and related rights only. Under CC0 1.0 section 4(a), patent and trademark rights
are not granted, waived, or otherwise affected. OdfKit therefore provides no express or implied patent
license, patent non-infringement warranty, patent search, or indemnity. Adopters should perform due
diligence for their jurisdiction, use, and integrated technologies. If this summary conflicts with the
[CC0 legal code](https://creativecommons.org/publicdomain/zero/1.0/legalcode), the legal code prevails.

## 2. Rights holders and AI-produced content

Public source, documentation, examples, and tests are currently written, organized, or produced
mostly with AI tools. A CC0 Affirmer must have authority to waive the relevant rights, and contributors
must ensure they may submit content under the project license. Treatment of purely machine-generated
work differs among jurisdictions. The project provides no commercial indemnity; adopters requiring an
identified copyright holder and an infringement indemnity should assess commercial alternatives or a
separate support agreement.

## 3. Clean-room and prohibited sources

Authoritative and prohibited sources for high-risk areas are listed in the clean-room source index.
Public OASIS, ISO, RFC, and W3C specifications, public wire shapes, redistributable fixtures, behavioral
comparison, and independent regression tests are permitted. Copying LibreOffice C++, Java ODF Toolkit,
Apache POI, NPOI, commercial SDK source, or decompiled closed-source binaries is prohibited. JSON
Collaboration is a compatible extension implementing a public TDF operation subset, not a source-code port.

## 4. Standards and trademarks

Descriptive references to OpenDocument, ODF, OpenFormula, OOXML, and LibreOffice compatibility tests are
permitted. Do not imply certification, endorsement, or official affiliation with OASIS, The Document
Foundation, LibreOffice, or Apache. “ODF Toolkit parity” describes capability and evidence comparisons,
not an official port or co-branded product.

## 5. Developer Certificate of Origin (DCO)

Contributors must be able to state that they created the contribution or may submit it under the
project license; knowingly included no non-redistributable third-party source; followed the clean-room
index when implementing public specifications; and updated third-party notices and package metadata for
new dependencies. Use `Signed-off-by: Name <email>` in commits or pull requests where appropriate; the
project also requires GPG-signed commits.

## 6. Adopter due diligence

Review `LICENSE`, third-party notices, SBOM and license scanning, version and compatibility commitments,
format support and non-goals, resource limits and validation, provenance, and operational support.
OdfKit is currently `0.x`, provides no SLA, and critical systems should retain fallback and maintenance plans.

## 7. Vulnerability and security reporting

The project currently provides neither a public issue tracker nor a private security-reporting channel.
Until a channel is announced, it does not claim to receive, track, or resolve reports under an SLA. If a
public tracker is later opened, complete exploit details should not be posted publicly. Security and
licensing or infringement matters must be handled separately.

## 8. Related documents

- [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md)
- [Provenance overview](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md)
- [Clean-room source index](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md)
- [ODF Toolkit parity](https://github.com/rubujo/OdfKit/blob/main/docs/odf-toolkit-parity.md)
- [Foreign extension policy](https://github.com/rubujo/OdfKit/blob/main/docs/foreign-extension-policy.md)
- [Corpus manifest rules](https://github.com/rubujo/OdfKit/blob/main/docs/corpus-manifest.md)
