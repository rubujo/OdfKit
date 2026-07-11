---
title: Guia de utilização, conformidade, segurança e evidências
_lang: pt
---

# Utilização, conformidade, segurança e evidências

## Âmbito da documentação da API

A referência da API é gerada a partir dos assemblies públicos `net10.0` e da documentação XML. As APIs principais escritas manualmente e as extensões públicas são apresentadas em páginas individuais. A extensa superfície `OdfKit.DOM` gerada por esquemas continua a ser controlada pelas baselines Public API dos dois TFM e pela cobertura Typed DOM. Os resumos dos membros estão atualmente disponíveis em inglês e chinês tradicional; esta entrada em português não afirma que todos os membros da API estão traduzidos.

## Licença e produção por IA

O código original do OdfKit e a documentação original do site utilizam CC0-1.0 Universal. Pacotes, esquemas, ferramentas e fixtures de terceiros mantêm as suas licenças. O conteúdo público do projeto é escrito, organizado ou produzido com ferramentas de IA. Este site não constitui aconselhamento jurídico e não fornece SLA nem indemnização comercial. OdfKit não é um projeto oficial nem endossado pela OASIS, The Document Foundation, LibreOffice ou Apache.

## Limites de segurança e interoperabilidade

Mantenha os limites de recursos de reader e package para ficheiros não confiáveis e execute validação ou sanitização adequada. Estes controlos reduzem o risco, mas não garantem segurança absoluta contra documentos maliciosos. A validade do esquema, um round-trip ou testes com uma versão do LibreOffice não implicam resultados idênticos ao píxel em todas as suites de escritório.

## Capacidades e evidências

As afirmações são separadas em `PackageFidelity`, `SemanticApiDepth` e `InteropEvidence`; uma dimensão não comprova as restantes. Os resultados de desempenho publicados devem identificar o commit, o runtime, o ambiente e o método reproduzível. Os orçamentos de desempenho continuam na fase de recolha de amostras.

- [Abrir a referência da API [en + zh-TW]](xref:OdfKit)
- [Índice de afirmações e evidências](project-docs/evidence-index.md)
- [Limites de segurança](project-docs/security-limits.md)
- [Propriedade intelectual e conformidade](project-docs/ip-compliance.md)
- [Licença](articles/license.md)
- [Avisos de terceiros](project-docs/THIRD-PARTY-NOTICES.md)
