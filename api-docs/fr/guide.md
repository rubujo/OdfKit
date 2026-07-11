---
title: Guide d'utilisation, de conformité, de sécurité et de preuves
_lang: fr
---

# Utilisation, conformité, sécurité et preuves

## Périmètre de la documentation API

La référence API est générée à partir des assemblies publiques `net10.0` et de la documentation XML. Les API principales manuscrites et les extensions publiques disposent de pages individuelles. La vaste surface `OdfKit.DOM` générée depuis les schémas reste régie par les baselines Public API des deux TFM et par la couverture Typed DOM. Les résumés des membres sont actuellement disponibles en anglais et en chinois traditionnel ; cette entrée française ne prétend pas que tous les membres sont traduits.

## Licence et production par IA

Le code original d'OdfKit et la documentation originale du site utilisent CC0-1.0 Universal. Les paquets, schémas, outils et fixtures tiers conservent leurs licences. Le contenu public du projet est rédigé, organisé ou produit avec des outils d'IA. Ce site ne constitue pas un conseil juridique et ne fournit ni SLA ni indemnisation commerciale. OdfKit n'est ni un projet officiel ni un projet approuvé par OASIS, The Document Foundation, LibreOffice ou Apache.

## Limites de sécurité et d'interopérabilité

Conservez les limites de ressources des readers et des packages pour les fichiers non fiables, et appliquez une validation ou une purification adaptée. Ces contrôles réduisent les risques sans garantir une sécurité absolue face aux documents malveillants. La validité du schéma, un round-trip ou un test avec une version de LibreOffice n'impliquent pas un rendu identique au pixel dans toutes les suites bureautiques.

## Capacités et preuves

Les affirmations sont séparées en `PackageFidelity`, `SemanticApiDepth` et `InteropEvidence` ; une dimension ne prouve pas les autres. Les résultats de performance publiés doivent préciser le commit, le runtime, l'environnement et la méthode reproductible. Les budgets de performance sont encore en phase de collecte.

- [Ouvrir la référence API [en + zh-TW]](xref:OdfKit)
- [Index des affirmations et preuves](project-docs/evidence-index.md)
- [Limites de sécurité](project-docs/security-limits.md)
- [Propriété intellectuelle et conformité](project-docs/ip-compliance.md)
- [Licence](articles/license.md)
- [Avis relatifs aux tiers](project-docs/THIRD-PARTY-NOTICES.md)
