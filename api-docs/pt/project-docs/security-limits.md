---
title: Limites de segurança dos leitores de fluxo
_lang: pt
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Limites de segurança dos leitores de fluxo

> Tradução informativa; em caso de divergência, prevalece a fonte zh-TW.

`OdsStreamReader` e `OdtStreamReader` não criam o DOM completo, mas alocam buffers para a linha atual,
texto dos nós, descompressão ZIP e leitor XML. Baixa residência não elimina o efeito do tamanho da entrada.

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
