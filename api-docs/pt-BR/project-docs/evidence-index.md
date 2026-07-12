---
title: Índice de afirmações de capacidade e evidências
_lang: pt-BR
translation_source: docs/evidence-index.md
translation_source_sha256: d99bcf07e600d948fde4bf3629b9b2781999ba303ec05c568075d83bd48762a2
---

# Índice de afirmações de capacidade e evidências

> Tradução informativa; identificadores e valores legíveis por máquina não são traduzidos.

Este índice separa as capacidades em três dimensões que não podem ser deduzidas umas das outras. A fonte
legível por máquina é [`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json); o CI
verifica os IDs das afirmações, os caminhos das evidências e as descrições das limitações.

| Afirmação | Formato | Dimensão | Nível | Resumo da limitação |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | A leitura e a gravação de ida e volta do pacote não implicam recálculo de fórmulas nem semântica completa de planilha. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Lê valores e fórmulas armazenados, mas não recalcula as fórmulas. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Não fornece um mecanismo de layout nem de renderização. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | O ODP é carregado como DOM e pacote; não se afirma a existência de uma API de slides em fluxo. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Não implementa layout de SmartArt nem renderização no nível de pixels. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Testes com uma versão específica do LibreOffice não garantem igualdade de pixels em todos os pacotes de escritório. |

`PackageFidelity` responde apenas se o pacote pode ser processado com segurança; `SemanticApiDepth` indica
quanto da semântica do documento a API consegue compreender e modificar; `InteropEvidence` indica quais
programas externos e versões foram efetivamente testados. O nível máximo em uma dimensão não substitui as
outras duas.

A fonte única da verdade para os grupos semânticos, as operações CRUD, as seções das normas, a implementação,
os testes, as evidências de interoperabilidade e as limitações dos quatro formatos principais é
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json).
`eng/Test-SemanticCoverage.ps1` bloqueia afirmações incompletas no CI. Os limites das fontes do processo
clean-room estão descritos em
[`provenance/semantic-api-clean-room.md`](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/semantic-api-clean-room.md).

O schema v3 de cobertura semântica também exige que cada tópico tenha evidências para `Create`, `Get`,
`Find`, `Set`, `Update`, `Remove`, `Clear`, `RoundTrip` e `Interop`, vinculadas às especificações,
implementação, testes, limitações e proveniência clean-room. Cada família também deve ter evidências
verificadas por máquina para documentos existentes, preservação de conteúdo desconhecido, ODF 1.1–1.3,
diagnósticos de downgrade e entradas inválidas. Consulte o [guia de migração](https://github.com/rubujo/OdfKit/blob/main/docs/migration-high-level-api.md)
e a [referência das fachadas semânticas dos quatro formatos](https://github.com/rubujo/OdfKit/blob/main/docs/reference/semantic-facades.md).
