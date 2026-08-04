---
title: OdfKit usage, compliance, security, and evidence guide
_lang: en
---

# Usage, compliance, security, and evidence guide

## API documentation scope

The API reference is generated from the `net10.0` public assemblies and XML documentation. Handwritten core APIs and public extensions are rendered as individual pages. The large schema-generated `OdfKit.DOM` surface remains governed by the dual-TFM Public API baselines and Typed DOM coverage. Member summaries are currently available in English and Traditional Chinese; other locale entries do not claim translated API members.

## License and AI production

Original OdfKit code and original site documentation use CC0 1.0 Universal. Third-party packages, schemas, tools, and fixtures retain their own licenses. Public project content is written, organized, or produced with AI tools. This site is not legal advice and provides no SLA or commercial indemnity. OdfKit is not an official or endorsed project of OASIS, The Document Foundation, LibreOffice, or Apache.

## Security and interoperability boundaries

Keep reader and package resource limits enabled for untrusted files, and run validation or sanitization where appropriate. These controls reduce risk but do not guarantee absolute safety from malicious documents. Schema validity, round trips, or tests against one LibreOffice version do not imply pixel-identical behavior in every office suite.

## Capabilities and evidence

Claims are separated into `PackageFidelity`, `SemanticApiDepth`, and `InteropEvidence`; one dimension cannot prove another. Published performance results must identify the commit, runtime, environment, and reproducible method. Performance budgets remain in the fixed-sample collection phase.

- [Open the API reference [en + zh-TW]](xref:OdfKit)
- [Claims and evidence index](project-docs/evidence-index.md)
- [Security limits](project-docs/security-limits.md)
- [Intellectual property and compliance](project-docs/ip-compliance.md)
- [License](articles/license.md)
- [Third-party notices](project-docs/THIRD-PARTY-NOTICES.md)
