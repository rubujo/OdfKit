---
title: Index des déclarations de capacité et des preuves
_lang: fr
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
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
