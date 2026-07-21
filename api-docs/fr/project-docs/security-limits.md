---
title: Limites de sécurité du chargement et des lecteurs en continu
_lang: fr
translation_source: docs/security-limits.md
translation_source_sha256: 717f100dcc24a436ae8dd8c64601585f59320489dc452918dd8d97837e4fd8f0
---

# Limites de sécurité du chargement et des lecteurs en continu

> Traduction informative : en cas de divergence, la source zh-TW fait foi.

Le chargement des paquets et `OdsStreamReader`/`OdtStreamReader` traitent des entrées ZIP/XML non fiables. Les lecteurs ne construisent pas le DOM complet, mais allouent des tampons pour
la ligne courante, le texte des nœuds, la décompression ZIP et le lecteur XML. Une faible mémoire
résidente ne rend pas l’utilisation des ressources indépendante de la taille d’entrée.

## Limites du paquet principal

`OdfDocument.Load`, les façades `Load` et `OdfPackage.Open` partagent les budgets de `OdfLoadOptions`.

| Limite | Valeur par défaut | Protection |
|---|---:|---|
| Entrées ZIP | 5,000 | Évite l’épuisement du processeur et de la mémoire par de nombreuses petites entrées |
| Taille décompressée d’une entrée | 500 MiB | Limite l’expansion d’une entrée ZIP |
| Taille décompressée totale | 1 GiB | Limite l’expansion totale du paquet |
| Entrée brute non recherchable | 1 GiB | Limite la mise en mémoire tampon avant l’expansion ZIP |
| Caractères d’un document XML | 64 MiB | Limite l’analyse XML et la construction du DOM |

Les quatre limites ZIP doivent être positives ; zéro ou une valeur négative déclenche immédiatement `ArgumentOutOfRangeException`. Seul `MaxXmlCharactersInDocument = 0` désactive la limite XML. Tous les lecteurs XML doivent interdire les DTD et resolvers externes. Les nouveaux chemins doivent réutiliser `OdfLoadOptions`. Les chemins de validation des paquets et du Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` et l’analyse des règles de profil) appliquent également `MaxXmlCharactersInDocument` : la validation des paquets utilise `package.LoadOptions`, tandis que la validation Flat utilise `OdfValidationOptions.LoadOptions` (la valeur par défaut de 64 MiB de `OdfLoadOptions` en cas d’omission). Les signatures, horodatages, données de révocation de certificat et réponses réseau externes ont leurs propres limites, plus petites ; la limite du paquet principal ne les remplace pas. Pour les règles de contenu, utilisez `OdfPackageValidator`, `SanitizeMacros`, la validation des signatures ou `pwsh eng/Test-OdfPolicy.ps1`.

## Limites des lecteurs en continu

| Lecteur | Limite | Valeur par défaut |
|---|---|---:|
| ODS | Caractères XML | 64 MiB |
| ODS | Lignes par feuille | 1,048,576 |
| ODS | Colonnes par ligne | 16,384 |
| ODS | Une déclaration repeat | lignes 1,048,576 ; colonnes 16,384 |
| ODS | Texte extrait d’une cellule | 16 MiB |
| ODT | Caractères XML | 64 MiB |
| ODT | Nœuds texte renvoyés | 1,000,000 |
| ODT | Texte extrait d’un nœud | 16 MiB |

La lecture échoue lorsqu’une limite est dépassée ; elle ne tronque pas repeat pour renvoyer des données
apparemment complètes. Ne relancez pas automatiquement sans limites.

`LeaveOpen` vaut `false` par défaut. Avec `true`, la suppression du lecteur ferme le flux d’entrée XML et
le lecteur ZIP, mais laisse ouvert le flux externe fourni par l’appelant.

Conservez les limites par défaut pour les documents non fiables et validez d’abord le paquet et le schéma.
Augmenter les limites XML ou de texte accroît aussi les risques mémoire et CPU DoS.
`MaxXmlCharactersInDocument = 0` ne désactive que la limite de caractères XML. Les limites, la validation
et l’assainissement réduisent les risques sans garantir une sécurité absolue contre les documents malveillants.

Les options des lecteurs ODS et ODT valident les règles lors de l’affectation : la limite XML accepte zéro, tandis que les limites de lignes, colonnes, repeat, nœuds et texte doivent être supérieures à zéro.
