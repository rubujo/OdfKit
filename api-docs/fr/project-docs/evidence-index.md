---
title: Index des déclarations de capacité et des preuves
_lang: fr
translation_source: docs/evidence-index.md
translation_source_sha256: d99bcf07e600d948fde4bf3629b9b2781999ba303ec05c568075d83bd48762a2
---

# Index des déclarations de capacité et des preuves

> Traduction informative ; les identifiants et valeurs lisibles par machine ne sont pas traduits.

Cet index sépare trois dimensions qui ne s’impliquent pas mutuellement. La source lisible par machine est
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Format | Dimension | Niveau | Limitation |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | Un aller-retour du paquet n’implique ni recalcul des formules ni sémantique complète du tableur. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Lit les valeurs et formules enregistrées sans recalculer les formules. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Aucun moteur de mise en page ou de rendu. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | Charge de travail DOM/paquet, sans API de diapositives en continu. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Ni disposition SmartArt ni rendu au pixel près. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Une version testée de LibreOffice ne garantit pas un résultat identique au pixel dans toutes les suites. |

`PackageFidelity` concerne le traitement sûr du paquet, `SemanticApiDepth` la compréhension et la
modification de la sémantique, et `InteropEvidence` les logiciels et versions testés. Aucune dimension ne
remplace les autres. [`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json)
est la source unique de la couverture sémantique, vérifiée par `eng/Test-SemanticCoverage.ps1` dans la CI.

Le schéma v3 de couverture sémantique exige en outre, pour chaque thème, des preuves concernant
`Create`, `Get`, `Find`, `Set`, `Update`, `Remove`, `Clear`, `RoundTrip` et `Interop`, reliées aux
spécifications, à l’implémentation, aux tests, aux limitations et à la provenance clean-room. Chaque
famille doit aussi disposer de preuves vérifiées automatiquement pour les documents existants, la
préservation du contenu inconnu, ODF 1.1–1.3, les diagnostics de rétrogradation et les entrées non
valides. Voir le [guide de migration](https://github.com/rubujo/OdfKit/blob/main/docs/migration-high-level-api.md)
et la [référence des façades sémantiques des quatre formats](https://github.com/rubujo/OdfKit/blob/main/docs/reference/semantic-facades.md).
