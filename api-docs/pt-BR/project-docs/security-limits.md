---
title: Limites de segurança do carregamento e dos leitores em fluxo
_lang: pt-BR
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
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

## Outras proteções de recursos e de saída

Além dos limites do pacote e dos leitores de streaming, os seguintes limites fixos também protegem contra entradas não confiáveis. Esses valores são atualmente constantes fixas no código e ainda não podem ser configurados por meio de `OdfLoadOptions` ou de um objeto de opções. Avalie o impacto na memória e na pilha antes de aumentar ou remover um limite.

| Aspecto | Limite | Comportamento ao exceder |
|---|---|---|
| Tamanho descompactado real de uma entrada ZIP | Não pode exceder o tamanho não compactado declarado no cabeçalho (caminho MMF do carregamento por caminho de arquivo) | `SecurityException` |
| Diretório central ZIP danificado ou ZIP64 | O caminho rápido MMF não oferece suporte a ZIP64 e nenhum registro que não possa ser totalmente analisado pode ser ignorado silenciosamente | Recorre à validação e leitura por meio de `ZipArchive` |
| Profundidade de aninhamento de elementos XML | 256 níveis (`OdfXmlReader.MaxElementDepth`); aplica-se ao carregamento DOM, ao carregamento de Flat ODF, à validação de regras de perfil e à análise RDF | O carregamento lança `SecurityException`; a validação informa `ODF0303` ou `ODF0301` |
| Profundidade de aninhamento na análise de fórmulas | 256 níveis cada para parênteses, argumentos de função, matrizes em linha e operadores prefixo consecutivos | `InvalidOperationException` |
| Total de nós de operador de uma fórmula | 4.096 (`FormulaParser.MaxOperatorNodes`); operadores binários, unários, de porcentagem e de referência somados. Fórmulas encadeadas formam árvores profundas à esquerda cuja avaliação e serialização recursam nível a nível; esse limite garante que uma pilha de thread comum seja suficiente | `InvalidOperationException` |
| Margem de pilha na recursão de fórmulas | A análise, a avaliação, a obtenção de intervalos e a serialização verificam a pilha restante (`RuntimeHelpers.EnsureSufficientExecutionStack`) antes de entrar em cada nível de recursão; mesmo fórmulas próximas dos limites não derrubam o processo em threads com pilha pequena de 128–256 KB | `InsufficientExecutionStackException`; `EvaluateFormulas` a converte em uma exceção de avaliação de fórmula ou em `#VALUE!` |
| Comprimento do resultado de texto de uma fórmula | 1.048.576 caracteres (`&`, resultados de funções, `SUBSTITUTE`, `REPT`) | Retorna `#VALUE!` |
| Funções de fórmula cujo limite de laço é um argumento | `BINOMDIST` acumulado 100.000; `CRITBINOM`, `HYPGEOMDIST` acumulado 100.000; `POISSON` acumulado 1.000.000; `DB`, `DDB`, `VDB`, `CUMIPMT` períodos 1.000.000 | Retorna `#NUM!` |
| Índice de linha/coluna da planilha | Linha 1.048.575, coluna 16.383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Anexação de documentos | Um documento não pode ser anexado a si mesmo | `ArgumentException` |
| Collaboration `addColumns` | Limitado pelo número de colunas e pelo total de células de `OdtOperationSafetyOptions` | Registra o limite de segurança e ignora a operação |
| Imagem alternativa do gráfico | 4.096 px por lado | Limitada ao valor máximo |
| Formato de conversão do LibreOffice | A parte da extensão antes dos dois-pontos não pode estar vazia nem conter `..`, NUL, CR ou LF | `ArgumentException` |
| Consulta SPARQL | Cláusulas `SERVICE` não são permitidas (impede que o mecanismo de consulta envie solicitações de rede a endpoints arbitrários) | `ArgumentException` |

## Limite de confiança

Mantenha os limites padrão para documentos não confiáveis e execute primeiro a validação de package e schema.
É possível aumentar limites específicos para documentos grandes confiáveis que realmente precisem ser
processados, mas o aumento dos limites de XML ou texto também eleva o risco de ataques à memória e de CPU DoS.
`MaxXmlCharactersInDocument = 0` desativa apenas o limite de caracteres XML; os demais limites do Reader
continuam válidos.

As opções dos Readers ODS e ODT validam as regras ao definir propriedades: o limite XML aceita zero, enquanto os limites de linhas, colunas, repeat, nós e texto devem ser maiores que zero.

Os limites de segurança, a validação e a sanitização reduzem o risco, mas não garantem segurança absoluta
contra documentos maliciosos.
