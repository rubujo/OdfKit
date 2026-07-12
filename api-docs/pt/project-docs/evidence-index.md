---
title: Índice de afirmações de capacidade e evidências
_lang: pt
translation_source: docs/evidence-index.md
translation_source_sha256: 931370015f608c7efcf929a70e06c14f533aa58a4869d98b67f6a62debd20b9e
---

# Índice de afirmações de capacidade e evidências

> Tradução informativa; IDs e valores legíveis por máquina não são traduzidos.

As três dimensões não se implicam mutuamente. A fonte legível por máquina é
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Formato | Dimensão | Nível | Limitação |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | round-trip-verified | A ida e volta do pacote não implica recálculo nem semântica completa. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-contract-verified | Lê valores e fórmulas guardados, mas não recalcula. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-contract-verified | Sem motor de paginação ou renderização. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-contract-verified | Carga DOM/pacote; sem API de diapositivos em fluxo. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-contract-verified | Sem layout SmartArt nem renderização ao píxel. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | interop-tested | Uma versão testada do LibreOffice não garante igualdade de píxeis em todas as suítes. |

`PackageFidelity` cobre o pacote, `SemanticApiDepth` a semântica e `InteropEvidence` os programas e versões
testados; nenhuma dimensão substitui outra. A fonte única da cobertura é
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json), verificada
por `eng/Test-SemanticCoverage.ps1`.

O schema v4 de cobertura semântica exige ainda que cada tópico tenha evidências para `Create`, `Get`,
`Find`, `Set`, `Update`, `Remove`, `Clear`, `RoundTrip` e `Interop`, ligadas às especificações,
implementação, testes, limitações e proveniência clean-room. Cada família também deve ter evidências
verificadas automaticamente para documentos existentes, preservação de conteúdo desconhecido,
ODF 1.1–1.3, diagnósticos de retrocesso de versão e entradas inválidas. Consulte o [guia de migração](https://github.com/rubujo/OdfKit/blob/main/docs/migration-high-level-api.md)
e a [referência das fachadas semânticas dos quatro formatos](https://github.com/rubujo/OdfKit/blob/main/docs/reference/semantic-facades.md).
