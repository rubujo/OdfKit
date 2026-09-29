---
title: Limites de sécurité du chargement et des lecteurs en continu
_lang: fr
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
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

## Autres protections des ressources et de la sortie

Outre les limites du paquet et des lecteurs en flux, les limites fixes suivantes protègent aussi contre les entrées non fiables. Ces valeurs sont actuellement des constantes fixes dans le code ; elles ne sont pas encore configurables via `OdfLoadOptions` ou un objet d'options. Évaluez l'impact sur la mémoire et la pile avant d'augmenter ou de supprimer une limite.

| Aspect | Limite | Comportement en cas de dépassement |
|---|---|---|
| Taille décompressée réelle d'une entrée ZIP | Ne doit pas dépasser la taille non compressée déclarée dans l'en-tête (chemin MMF du chargement par chemin de fichier) | `SecurityException` |
| Répertoire central ZIP endommagé ou ZIP64 | Le chemin rapide MMF ne prend pas en charge ZIP64, et aucun enregistrement impossible à analyser entièrement ne doit être ignoré en silence | Repli sur la validation et la lecture via `ZipArchive` |
| Profondeur d'imbrication des éléments XML | 256 niveaux (`OdfXmlReader.MaxElementDepth`) ; s'applique au chargement DOM, au chargement Flat ODF, à la validation des règles de profil et à l'analyse RDF | Le chargement lève `SecurityException` ; la validation signale `ODF0303` ou `ODF0301` |
| Profondeur d'imbrication de l'analyse des formules | 256 niveaux chacun pour les parenthèses, les arguments de fonction, les tableaux en ligne et les opérateurs préfixes consécutifs | `InvalidOperationException` |
| Nombre total de nœuds d'opérateur d'une formule | 4 096 (`FormulaParser.MaxOperatorNodes`) ; opérateurs binaires, unaires, de pourcentage et de référence cumulés. Les formules chaînées forment des arbres profonds à gauche dont l'évaluation et la sérialisation récursent niveau par niveau ; cette limite garantit qu'une pile de thread ordinaire suffit | `InvalidOperationException` |
| Marge de pile de la récursion des formules | L'analyse, l'évaluation, la récupération des plages et la sérialisation vérifient la pile restante (`RuntimeHelpers.EnsureSufficientExecutionStack`) avant d'entrer dans chaque niveau de récursion ; même les formules proches des limites ne font pas planter le processus sur des threads à petite pile de 128–256 Ko | `InsufficientExecutionStackException` ; `EvaluateFormulas` la convertit en exception d'évaluation de formule ou en `#VALUE!` |
| Longueur du résultat texte d'une formule | 1 048 576 caractères (`&`, résultats de fonctions, `SUBSTITUTE`, `REPT`) | Renvoie `#VALUE!` |
| Fonctions de formule dont la borne de boucle est un argument | `BINOMDIST` cumul 100 000 ; `CRITBINOM`, `HYPGEOMDIST` cumul 100 000 ; `POISSON` cumul 1 000 000 ; `DB`, `DDB`, `VDB`, `CUMIPMT` périodes 1 000 000 | Renvoie `#NUM!` |
| Indice de ligne/colonne de la feuille de calcul | Ligne 1 048 575, colonne 16 383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Ajout de document | Un document ne peut pas être ajouté à lui-même | `ArgumentException` |
| Collaboration `addColumns` | Limité par le nombre de colonnes et le nombre total de cellules de `OdtOperationSafetyOptions` | Journalise la limite de sécurité et ignore l'opération |
| Image de secours du graphique | 4 096 px par côté | Ramenée à la limite |
| Format de conversion LibreOffice | La partie d'extension avant les deux-points ne doit pas être vide ni contenir `..`, NUL, CR ou LF | `ArgumentException` |
| Requête SPARQL | Les clauses `SERVICE` ne sont pas autorisées (empêche le moteur de requêtes d'envoyer des requêtes réseau vers des points de terminaison arbitraires) | `ArgumentException` |

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
