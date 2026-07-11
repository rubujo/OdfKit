---
title: Índice de afirmações de capacidade e evidências
_lang: pt
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
---

# Índice de afirmações de capacidade e evidências

> Tradução informativa; IDs e valores legíveis por máquina não são traduzidos.

As três dimensões não se implicam mutuamente. A fonte legível por máquina é
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Formato | Dimensão | Nível | Limitação |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | A ida e volta do pacote não implica recálculo nem semântica completa. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Lê valores e fórmulas guardados, mas não recalcula. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Sem motor de paginação ou renderização. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | Carga DOM/pacote; sem API de diapositivos em fluxo. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Sem layout SmartArt nem renderização ao píxel. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Uma versão testada do LibreOffice não garante igualdade de píxeis em todas as suítes. |

`PackageFidelity` cobre o pacote, `SemanticApiDepth` a semântica e `InteropEvidence` os programas e versões
testados; nenhuma dimensão substitui outra. A fonte única da cobertura é
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json), verificada
por `eng/Test-SemanticCoverage.ps1`.
