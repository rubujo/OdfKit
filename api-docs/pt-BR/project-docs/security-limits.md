---
title: Limites de segurança do carregamento e dos leitores em fluxo
_lang: pt-BR
translation_source: docs/security-limits.md
translation_source_sha256: 717f100dcc24a436ae8dd8c64601585f59320489dc452918dd8d97837e4fd8f0
---

# Limites de segurança do carregamento e dos leitores em fluxo

> Tradução informativa; em caso de divergência, prevalece a fonte em chinês tradicional (`zh-TW`).

O carregamento de pacotes e `OdsStreamReader`/`OdtStreamReader` processam entradas ZIP/XML não confiáveis. Os Readers não criam o DOM completo do documento, mas alocam buffers para a linha
atual, o texto dos nós, a descompactação ZIP e o XML Reader. Um projeto de baixa residência não elimina os
efeitos do tamanho da entrada.

## Limites do pacote principal

`OdfDocument.Load`, as fachadas `Load` e `OdfPackage.Open` compartilham os orçamentos de `OdfLoadOptions`.

| Limite | Valor padrão | Proteção |
|---|---:|---|
| Entradas ZIP | 5,000 | Evita esgotamento de CPU e memória por muitas entradas pequenas |
| Tamanho descompactado de uma entrada | 500 MiB | Limita a expansão de uma entrada ZIP |
| Tamanho descompactado total | 1 GiB | Limita a expansão total do pacote |
| Entrada bruta não pesquisável | 1 GiB | Limita o buffer antes da expansão ZIP |
| Caracteres em um documento XML | 64 MiB | Limita a análise XML e a criação do DOM |

Os quatro limites ZIP devem ser positivos; zero ou valores negativos geram imediatamente `ArgumentOutOfRangeException`. Somente `MaxXmlCharactersInDocument = 0` desativa o limite XML. Todos os XML Readers devem proibir DTD e resolvers externos. Novos caminhos devem reutilizar `OdfLoadOptions`. Os caminhos de validação de pacotes e Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` e varreduras de regras de perfil) também aplicam `MaxXmlCharactersInDocument`: a validação de pacotes usa `package.LoadOptions`, enquanto a validação Flat usa `OdfValidationOptions.LoadOptions` (o padrão de 64 MiB de `OdfLoadOptions` quando omitido). Assinaturas, carimbos de data/hora, dados de revogação de certificados e respostas de rede externas têm limites próprios menores; o limite do pacote principal não os substitui. Para políticas de conteúdo use `OdfPackageValidator`, `SanitizeMacros`, validação de assinaturas ou `pwsh eng/Test-OdfPolicy.ps1`.

## Limites dos leitores em fluxo

| Reader | Limite | Valor padrão |
|---|---|---:|
| ODS | Caracteres XML | 64 MiB |
| ODS | Linhas por planilha | 1,048,576 |
| ODS | Colunas por linha | 16,384 |
| ODS | Uma declaração repeat | 1,048,576 linhas; 16,384 colunas |
| ODS | Texto extraído de uma célula | 16 MiB |
| ODT | Caracteres XML | 64 MiB |
| ODT | Nós de texto retornados | 1,000,000 |
| ODT | Texto extraído de um nó | 16 MiB |

A leitura falha quando um limite é excedido; repeat não é truncado para continuar retornando dados que
pareçam completos. Trate essas falhas como resultados da proteção de recursos e não tente novamente de forma
automática com os limites desativados.

## Propriedade dos fluxos

O valor padrão de `LeaveOpen` nas opções é `false`. Quando definido como `true`, o descarte do Reader ainda
fecha o fluxo da entrada XML e o ZIP Reader, mas mantém aberto o fluxo mais externo fornecido pelo chamador.

## Limite de confiança

Mantenha os limites padrão para documentos não confiáveis e execute primeiro a validação de package e schema.
É possível aumentar limites específicos para documentos grandes confiáveis que realmente precisem ser
processados, mas o aumento dos limites de XML ou texto também eleva o risco de ataques à memória e de CPU DoS.
`MaxXmlCharactersInDocument = 0` desativa apenas o limite de caracteres XML; os demais limites do Reader
continuam válidos.

As opções dos Readers ODS e ODT validam as regras ao definir propriedades: o limite XML aceita zero, enquanto os limites de linhas, colunas, repeat, nós e texto devem ser maiores que zero.

Os limites de segurança, a validação e a sanitização reduzem o risco, mas não garantem segurança absoluta
contra documentos maliciosos.
