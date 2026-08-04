---
title: Propriedade intelectual e conformidade
_lang: pt-BR
translation_source: docs/ip-compliance.md
translation_source_sha256: bccec797a382b4bf3fae941a34d0dd406fdc97cac84a38d6c20dc09109164b6f
---

# Propriedade intelectual e conformidade (IP Compliance)

> Tradução informativa; não constitui aconselhamento jurídico nem substitui a consulta à legislação da jurisdição aplicável.

Este documento destina-se à **conformidade e à devida diligência de compras dos adotantes** e aos
**colaboradores**. Ele não constitui aconselhamento jurídico nem substitui a consulta à legislação da
jurisdição aplicável.

Consulte as auditorias de fontes relacionadas em
[provenance/README.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md) e
[clean-room-source-index.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md).

## 1. Modelo de licenciamento (licenças compostas)

| Escopo | Licença | Descrição |
|---|---|---|
| Código original do projeto OdfKit | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) | O projeto procura renunciar aos direitos autorais na máxima extensão possível; consulte o arquivo `LICENSE` na raiz |
| Dependências de compilação e execução | Principalmente MIT, BSD e similares | **Não se tornam domínio público em razão da CC0**; os respectivos avisos e declarações de direitos autorais devem ser preservados na redistribuição |
| Esquemas OASIS ODF RELAX NG | OASIS Copyright | Localizados em `tools/OdfSchemaGenerator/schemas/`; consulte `THIRD-PARTY-NOTICES.md` |
| Casos de teste de Corpus e Collaboration | Campo `license` de cada caso | Consulte `docs/corpus-manifest.md` e cada arquivo `manifest.json` |

**Importante:** ao distribuir um aplicativo ou pacote que contenha o OdfKit e suas dependências, é necessário
cumprir simultaneamente:

1. os efeitos da licença `LICENSE` do projeto (CC0) sobre o código original; e
2. as obrigações das licenças de terceiros listadas em
   [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md).

Não se deve afirmar publicamente que “todo o produto resultante está em domínio público”.

### Limites da CC0 para patentes e marcas

Nos termos da seção 4(a) da CC0 1.0, direitos de patente e marca não são concedidos nem renunciados. OdfKit
não oferece licença de patente, garantia de não violação, pesquisa de patentes ou indenização. Os adotantes
devem realizar sua própria diligência. Prevalece o
[texto jurídico da CC0](https://creativecommons.org/publicdomain/zero/1.0/legalcode).

## 2. Titulares de direitos e declaração sobre conteúdo produzido com IA

- O README declara que grande parte do código-fonte, da documentação, dos exemplos e dos testes publicados foi escrita, organizada ou produzida com o uso de ferramentas de IA.
- O Affirmer da CC0 deve ter autoridade para dispor dos direitos aos quais renuncia. Antes de enviar uma contribuição, o colaborador deve confirmar que tem o direito de incorporá-la sob a licença do projeto; consulte a seção sobre DCO abaixo.
- O reconhecimento de direitos autorais sobre conteúdo produzido exclusivamente por máquinas varia entre jurisdições. Um adotante que necessite de um titular de direitos claramente identificado e de um compromisso de indenização por violação deve avaliar alternativas comerciais ou negociar um contrato de suporte separado. **Este projeto de código aberto não fornece indenização comercial por padrão**.

## 3. Processo clean-room e fontes proibidas

As fontes autorizadas, as ações permitidas e as **fontes que não podem ser copiadas** para módulos de alto
risco, como avaliação de OpenFormula, validação de schema pattern, criptografia OpenPGP, JSON Collaboration e
conversões controladas de formatos, estão listadas em
[clean-room-source-index.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md).

Resumo dos princípios:

- **Permitido:** normas públicas da OASIS, ISO, RFC, W3C e outras organizações; wire shapes públicos; reference JSON e casos de teste redistribuíveis; comparações de comportamento e regressões criadas pelo projeto.
- **Proibido:** copiar código-fonte do LibreOffice C++, Java ODF Toolkit, Apache POI, NPOI ou SDKs comerciais; usar binários fechados descompilados como fonte de implementação.
- **Compatível, não portado:** JSON Collaboration é apenas um subconjunto compatível das operations públicas da TDF dentro do escopo da extensão; não é um port do código-fonte do Toolkit.

## 4. Implementação de normas e marcas comerciais

- ODF, OpenFormula e OOXML são formatos de documentos abertos ou documentados publicamente; implementar readers, writers e validators de acordo com suas especificações é uma prática normal de interoperabilidade.
- É permitido o uso descritivo de expressões como “OpenDocument”, “ODF” e “testes de compatibilidade com o LibreOffice”.
- **Não se deve** sugerir que o projeto seja um projeto oficial, um produto certificado ou um produto endossado pela OASIS, The Document Foundation, LibreOffice ou Apache.
- “Comparação com o ODF Toolkit” significa uma comparação de recursos e evidências de teste; **não** significa um port oficial nem um produto conjunto.

## 5. Developer Certificate of Origin para colaboradores (DCO)

Ao enviar código ou documentação substancial, o colaborador deve poder declarar, segundo o modelo do
Developer Certificate of Origin:

1. que a contribuição é de sua autoria ou que tem o direito de enviá-la sob a licença do projeto;
2. que não incluiu deliberadamente código-fonte de terceiros que não tenha o direito de redistribuir;
3. que, se a implementação se basear em normas ou documentos públicos, respeitou o índice de fontes do processo clean-room;
4. que, ao adicionar uma dependência de terceiros, atualizou `THIRD-PARTY-NOTICES.md` e os metadados de pacote necessários.

Recomenda-se incluir `Signed-off-by: Name <email>` na mensagem do commit ou na descrição do PR. As regras de
Git do projeto também exigem assinatura GPG.

## 6. Lista de devida diligência para adotantes

| Item | Ação recomendada |
|---|---|
| Licenças | Leia `LICENSE` e `THIRD-PARTY-NOTICES.md`; inclua o SBOM e a verificação de licenças no CI |
| Versão | A versão atual é `0.x`; consulte os compromissos de compatibilidade em `CHANGELOG` e [version-delivery.md](https://github.com/rubujo/OdfKit/blob/main/docs/version-delivery.md) |
| Limites funcionais | Use [odf-format-support.md](https://github.com/rubujo/OdfKit/blob/main/docs/odf-format-support.md) e as evidências de teste; não dependa apenas de alegações de marketing |
| Objetivos fora do escopo | Consulte [udx-non-goals.md](https://github.com/rubujo/OdfKit/blob/main/docs/udx-non-goals.md), incluindo o mecanismo completo de layout e recursos interativos de suítes de escritório, como cache de tabelas dinâmicas e segmentações de dados |
| Segurança | Use os limites de recursos de `OdfLoadOptions`; execute `Validate` e a sanitização em entradas não confiáveis |
| Fontes | Revise `docs/provenance/`; quando necessário, compare diretórios de alto risco com projetos upstream para detectar semelhanças |
| Suporte | O projeto de código aberto não oferece SLA; sistemas críticos devem ter redundância e um plano próprio de manutenção |

## 7. Relato de vulnerabilidades e problemas de segurança

Atualmente, o projeto não oferece um issue tracker público nem um canal privado para relatar problemas de
segurança. Até que os mantenedores anunciem um canal formal, o projeto não afirma ser capaz de receber,
acompanhar ou processar relatos de segurança segundo um nível de serviço. Se um tracker público for aberto no
futuro, os detalhes completos de exploração não deverão ser publicados nele. Problemas de segurança devem ser
tratados separadamente de questões de licenciamento e violação de direitos.

## 8. Documentos relacionados

- [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md)
- [provenance/README.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md)
- [Índice de fontes do processo clean-room](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md)
- [Comparação com o ODF Toolkit](https://github.com/rubujo/OdfKit/blob/main/docs/odf-toolkit-parity.md)
- [Política de extensões externas](https://github.com/rubujo/OdfKit/blob/main/docs/foreign-extension-policy.md)
- [Regras do Corpus Manifest](https://github.com/rubujo/OdfKit/blob/main/docs/corpus-manifest.md)
