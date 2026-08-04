---
title: Propriété intellectuelle et conformité
_lang: fr
translation_source: docs/ip-compliance.md
translation_source_sha256: bccec797a382b4bf3fae941a34d0dd406fdc97cac84a38d6c20dc09109164b6f
---

# Propriété intellectuelle et conformité

> Traduction informative, sans valeur de conseil juridique. Les textes juridiques originaux prévalent.

Ce document aide les vérifications de conformité, les achats et les contributeurs. Voir l’
[index de provenance](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md).

## 1. Modèle de licences composé

Le code original utilise CC0 1.0 Universal ; les dépendances conservent leurs licences MIT, BSD ou autres,
les schémas OASIS leur OASIS Copyright et les fixtures la licence de leur manifeste. Une redistribution
doit respecter `LICENSE` et [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md).
Il ne faut pas présenter l’ensemble distribué comme appartenant au domaine public.

### Limites de CC0 concernant les brevets et les marques

Selon la section 4(a) de CC0 1.0, aucun droit de brevet ou de marque n’est accordé ni abandonné. OdfKit ne
fournit aucune licence de brevet, garantie de non-contrefaçon, recherche de brevets ou indemnisation. Les
utilisateurs doivent effectuer leur propre diligence. Le
[texte juridique CC0](https://creativecommons.org/publicdomain/zero/1.0/legalcode) prévaut.

## 2. Titulaires des droits et contenu produit par IA

Le code public, la documentation, les exemples et les tests ont majoritairement été rédigés ou organisés
avec des outils d’IA. L’Affirmer CC0 et les contributeurs doivent disposer des droits concernés. Le statut
des productions purement automatiques varie selon les juridictions. Le projet ne fournit aucune indemnisation commerciale.

## 3. Clean-room et sources interdites

Les spécifications publiques OASIS, ISO, RFC et W3C, les formats publics, fixtures redistribuables,
comparaisons de comportement et régressions indépendantes sont permis. Copier LibreOffice C++, Java ODF
Toolkit, Apache POI, NPOI, un SDK commercial ou un binaire fermé décompilé est interdit. JSON Collaboration
est compatible avec un sous-ensemble TDF public, sans être un portage de code source.

## 4. Normes et marques

Les références descriptives à OpenDocument, ODF, OpenFormula, OOXML et aux essais LibreOffice sont permises.
Il est interdit de suggérer une certification, une approbation ou un statut officiel auprès d’OASIS, TDF,
LibreOffice ou Apache.

## 5. Developer Certificate of Origin (DCO)

Les contributeurs doivent pouvoir confirmer l’origine ou le droit de soumission, l’absence de code tiers
non redistribuable, le respect de l’index clean-room et la mise à jour des avis de tiers. Utiliser au besoin
`Signed-off-by: Name <email>` ; les commits doivent aussi être signés par GPG.

## 6. Vérifications des utilisateurs

Examinez licences et SBOM, statut `0.x`, limites fonctionnelles et de ressources, provenance et support.
Il n’existe aucun SLA ; un système critique doit prévoir repli et maintenance.

## 7. Signalement de sécurité

Il n’existe actuellement ni outil public de suivi ni canal privé, et aucune promesse de traitement. Les
détails complets d’exploitation ne doivent pas être publiés. Sécurité et questions de licence doivent être séparées.

## 8. Documents associés

Voir l’[index des sources clean-room](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md),
la [politique d’extensions étrangères](https://github.com/rubujo/OdfKit/blob/main/docs/foreign-extension-policy.md)
et les [règles du corpus](https://github.com/rubujo/OdfKit/blob/main/docs/corpus-manifest.md).
