---
title: Guia de uso, conformidade, segurança e evidências do OdfKit
_lang: pt-BR
---

# Guia de uso, conformidade, segurança e evidências

## Escopo da documentação da API

A referência da API é gerada a partir dos assemblies públicos `net10.0` e da documentação XML. As APIs principais escritas manualmente e as extensões públicas são exibidas em páginas individuais. A ampla superfície `OdfKit.DOM` gerada por esquema continua sendo controlada pelas linhas de base da API pública de ambos os TFM e pela cobertura de Typed DOM. Os resumos dos membros estão disponíveis em inglês e chinês tradicional; as demais entradas de idioma não afirmam que os membros da API estejam traduzidos.

## Licença e produção com IA

O código e a documentação originais do OdfKit usam CC0 1.0 Universal. Pacotes, esquemas, ferramentas e dados de teste de terceiros mantêm suas próprias licenças. O conteúdo público é escrito, organizado ou produzido com ferramentas de IA. Este site não constitui aconselhamento jurídico e não fornece SLA nem indenização comercial.

## Limites de segurança e interoperabilidade

Mantenha os limites de recursos do leitor e do pacote ativados para arquivos não confiáveis e execute validação ou sanitização quando apropriado. Esses controles reduzem o risco, mas não garantem segurança absoluta contra documentos maliciosos.

## Recursos e evidências

As afirmações são separadas em `PackageFidelity`, `SemanticApiDepth` e `InteropEvidence`; uma dimensão não comprova as demais.

- [Referência da API [en + zh-TW]](xref:OdfKit)
- [Índice de afirmações e evidências](project-docs/evidence-index.md)
- [Limites de segurança](project-docs/security-limits.md)
- [Propriedade intelectual e conformidade](project-docs/ip-compliance.md)
- [Licença](articles/license.md)
- [Avisos de terceiros](project-docs/THIRD-PARTY-NOTICES.md)
