---
title: Limites de sécurité des lecteurs en continu
_lang: fr
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Limites de sécurité des lecteurs en continu

> Traduction informative : en cas de divergence, la source zh-TW fait foi.

`OdsStreamReader` et `OdtStreamReader` ne construisent pas le DOM complet, mais allouent des tampons pour
la ligne courante, le texte des nœuds, la décompression ZIP et le lecteur XML. Une faible mémoire
résidente ne rend pas l’utilisation des ressources indépendante de la taille d’entrée.

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
