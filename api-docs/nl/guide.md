---
title: Handleiding voor gebruik, naleving, beveiliging en bewijs
_lang: nl
---

# Gebruik, naleving, beveiliging en bewijs

## Bereik van de API-documentatie

De API-referentie wordt gegenereerd uit de openbare `net10.0`-assemblies en XML-documentatie. Handgeschreven kern-API's en openbare uitbreidingen krijgen afzonderlijke pagina's. Het grote, uit schema's gegenereerde `OdfKit.DOM`-oppervlak blijft onder beheer van de Public API-baselines voor beide TFM's en de Typed DOM-dekking. Samenvattingen van leden zijn momenteel beschikbaar in het Engels en traditioneel Chinees; deze Nederlandse ingang beweert niet dat alle API-leden zijn vertaald.

## Licentie en AI-productie

Oorspronkelijke OdfKit-code en oorspronkelijke websitedocumentatie gebruiken CC0 1.0 Universal. Pakketten, schema's, hulpmiddelen en fixtures van derden behouden hun eigen licenties. Openbare projectinhoud is geschreven, geordend of geproduceerd met AI-hulpmiddelen. Deze website is geen juridisch advies en biedt geen SLA of commerciële vrijwaring. OdfKit is geen officieel of onderschreven project van OASIS, The Document Foundation, LibreOffice of Apache.

## Grenzen van beveiliging en interoperabiliteit

Laat resourcegrenzen voor reader en package ingeschakeld bij niet-vertrouwde bestanden en voer passende validatie of opschoning uit. Deze maatregelen verminderen risico's, maar garanderen geen absolute veiligheid tegen kwaadaardige documenten. Schemavaliditeit, round-trips of tests met één LibreOffice-versie betekenen geen pixelidentiek resultaat in elke kantoorsuite.

## Mogelijkheden en bewijs

Claims zijn gescheiden in `PackageFidelity`, `SemanticApiDepth` en `InteropEvidence`; één dimensie bewijst de andere niet. Gepubliceerde prestatieresultaten moeten commit, runtime, omgeving en reproduceerbare methode noemen. De prestatiebudgetten bevinden zich nog in de verzamelingsfase.

- [API-referentie openen [en + zh-TW]](xref:OdfKit)
- [Claims- en bewijsindex](project-docs/evidence-index.md)
- [Beveiligingsgrenzen](project-docs/security-limits.md)
- [Intellectueel eigendom en naleving](project-docs/ip-compliance.md)
- [Licentie](articles/license.md)
- [Kennisgevingen van derden](project-docs/THIRD-PARTY-NOTICES.md)
