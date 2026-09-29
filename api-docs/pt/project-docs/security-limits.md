---
title: Limites de segurança do carregamento e dos leitores de fluxo
_lang: pt
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
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

## Outras proteções de recursos e de saída

Além dos limites do pacote e dos leitores de streaming, os seguintes limites fixos também protegem contra entradas não confiáveis. Estes valores são atualmente constantes fixas no código e ainda não podem ser configurados através de `OdfLoadOptions` ou de um objeto de opções. Avalie o impacto na memória e na pilha antes de aumentar ou remover um limite.

| Aspeto | Limite | Comportamento ao exceder |
|---|---|---|
| Tamanho descomprimido real de uma entrada ZIP | Não pode exceder o tamanho não comprimido declarado no cabeçalho (caminho MMF do carregamento por caminho de ficheiro) | `SecurityException` |
| Diretório central ZIP danificado ou ZIP64 | O caminho rápido MMF não suporta ZIP64 e nenhum registo que não possa ser totalmente analisado pode ser ignorado em silêncio | Recorre à validação e leitura através de `ZipArchive` |
| Profundidade de aninhamento de elementos XML | 256 níveis (`OdfXmlReader.MaxElementDepth`); aplica-se ao carregamento DOM, ao carregamento de Flat ODF, à validação de regras de perfil e à análise RDF | O carregamento lança `SecurityException`; a validação comunica `ODF0303` ou `ODF0301` |
| Profundidade de aninhamento na análise de fórmulas | 256 níveis cada para parênteses, argumentos de função, matrizes em linha e operadores prefixo consecutivos | `InvalidOperationException` |
| Total de nós de operador de uma fórmula | 4.096 (`FormulaParser.MaxOperatorNodes`); operadores binários, unários, de percentagem e de referência em conjunto. As fórmulas encadeadas formam árvores profundas à esquerda cuja avaliação e serialização recursam nível a nível; este limite garante que uma pilha de thread normal é suficiente | `InvalidOperationException` |
| Margem de pilha na recursão de fórmulas | A análise, a avaliação, a obtenção de intervalos e a serialização verificam a pilha restante (`RuntimeHelpers.EnsureSufficientExecutionStack`) antes de entrar em cada nível de recursão; mesmo as fórmulas próximas dos limites não bloqueiam o processo em threads com pilha pequena de 128–256 KB | `InsufficientExecutionStackException`; `EvaluateFormulas` converte-a numa exceção de avaliação de fórmula ou em `#VALUE!` |
| Comprimento do resultado de texto de uma fórmula | 1.048.576 carateres (`&`, resultados de funções, `SUBSTITUTE`, `REPT`) | Devolve `#VALUE!` |
| Funções de fórmula cujo limite de ciclo é um argumento | `BINOMDIST` acumulado 100.000; `CRITBINOM`, `HYPGEOMDIST` acumulado 100.000; `POISSON` acumulado 1.000.000; `DB`, `DDB`, `VDB`, `CUMIPMT` períodos 1.000.000 | Devolve `#NUM!` |
| Índice de linha/coluna da folha de cálculo | Linha 1.048.575, coluna 16.383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Anexação de documentos | Um documento não pode ser anexado a si próprio | `ArgumentException` |
| Collaboration `addColumns` | Limitado pelo número de colunas e pelo total de células de `OdtOperationSafetyOptions` | Regista o limite de segurança e ignora a operação |
| Imagem alternativa do gráfico | 4.096 px por lado | Limitada ao valor máximo |
| Formato de conversão do LibreOffice | A parte da extensão antes dos dois pontos não pode estar vazia nem conter `..`, NUL, CR ou LF | `ArgumentException` |
| Consulta SPARQL | Não são permitidas cláusulas `SERVICE` (impede que o motor de consultas envie pedidos de rede para pontos finais arbitrários) | `ArgumentException` |

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
