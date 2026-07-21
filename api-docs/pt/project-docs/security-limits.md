---
title: Limites de segurança do carregamento e dos leitores de fluxo
_lang: pt
translation_source: docs/security-limits.md
translation_source_sha256: 717f100dcc24a436ae8dd8c64601585f59320489dc452918dd8d97837e4fd8f0
---

# Limites de segurança do carregamento e dos leitores de fluxo

> Tradução informativa; em caso de divergência, prevalece a fonte zh-TW.

O carregamento de pacotes e `OdsStreamReader`/`OdtStreamReader` processam entradas ZIP/XML não fiáveis. Os leitores não criam o DOM completo, mas alocam buffers para a linha atual,
texto dos nós, descompressão ZIP e leitor XML. Baixa residência não elimina o efeito do tamanho da entrada.

## Limites do pacote principal

`OdfDocument.Load`, as fachadas `Load` e `OdfPackage.Open` partilham os orçamentos de `OdfLoadOptions`.

| Limite | Predefinição | Proteção |
|---|---:|---|
| Entradas ZIP | 5,000 | Evita esgotar CPU e memória com muitas entradas pequenas |
| Tamanho descomprimido de uma entrada | 500 MiB | Limita a expansão de uma entrada ZIP |
| Tamanho descomprimido total | 1 GiB | Limita a expansão total do pacote |
| Entrada bruta não pesquisável | 1 GiB | Limita o buffer antes da expansão ZIP |
| Caracteres num documento XML | 64 MiB | Limita a análise XML e a criação do DOM |

Os quatro limites ZIP têm de ser positivos; zero ou valores negativos geram imediatamente `ArgumentOutOfRangeException`. Apenas `MaxXmlCharactersInDocument = 0` desativa o limite XML. Todos os leitores XML devem proibir DTD e resolvers externos. Novos caminhos devem reutilizar `OdfLoadOptions`. Os caminhos de validação de pacotes e Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` e análise das regras de perfil) também aplicam `MaxXmlCharactersInDocument`: a validação de pacotes usa `package.LoadOptions`, enquanto a validação Flat usa `OdfValidationOptions.LoadOptions` (a predefinição de 64 MiB de `OdfLoadOptions` quando omitida). Assinaturas, carimbos de data e hora, dados de revogação de certificados e respostas de rede externas têm limites próprios mais pequenos; o limite do pacote principal não os substitui. Para políticas de conteúdo use `OdfPackageValidator`, `SanitizeMacros`, validação de assinaturas ou `pwsh eng/Test-OdfPolicy.ps1`.

## Limites dos leitores de fluxo

| Leitor | Limite | Predefinição |
|---|---|---:|
| ODS | Caracteres XML | 64 MiB |
| ODS | Linhas por folha | 1,048,576 |
| ODS | Colunas por linha | 16,384 |
| ODS | Uma declaração repeat | linhas 1,048,576; colunas 16,384 |
| ODS | Texto de uma célula | 16 MiB |
| ODT | Caracteres XML | 64 MiB |
| ODT | Nós de texto devolvidos | 1,000,000 |
| ODT | Texto de um nó | 16 MiB |

Exceder um limite faz a leitura falhar; repeat não é truncado para devolver dados aparentemente completos.
Não repita automaticamente sem limites. `LeaveOpen` é `false`; com `true`, o fluxo XML e o leitor ZIP
são fechados, mas o fluxo exterior do chamador permanece aberto.

Mantenha os limites para documentos não fiáveis e valide pacote e esquema. Aumentá-los eleva riscos de
memória e CPU DoS. `MaxXmlCharactersInDocument = 0` desativa apenas o limite XML. Limites, validação e
sanitização reduzem riscos, mas não garantem segurança absoluta.

As opções dos leitores ODS e ODT validam as regras ao atribuir propriedades: o limite XML aceita zero, enquanto os limites de linhas, colunas, repeat, nós e texto devem ser superiores a zero.
