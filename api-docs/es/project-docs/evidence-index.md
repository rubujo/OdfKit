---
title: Índice de declaraciones de capacidad y evidencias
_lang: es
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
---

# Índice de declaraciones de capacidad y evidencias

> Traducción informativa; no se traducen los identificadores ni los valores legibles por máquina.

Este índice separa las capacidades en tres dimensiones que no se deducen entre sí. La fuente legible por
máquina es [`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json); la integración
continua comprueba los identificadores de las declaraciones, las rutas de las evidencias y las limitaciones.

| Declaración | Formato | Dimensión | Nivel | Resumen de la limitación |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | La lectura y escritura de ida y vuelta del paquete no implica volver a calcular fórmulas ni disponer de toda la semántica de una hoja de cálculo. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Lee los valores y las fórmulas almacenados, pero no vuelve a calcular las fórmulas. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | No proporciona un motor de maquetación ni de renderización. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | ODP es una carga DOM y de paquete; no se declara una API de diapositivas en flujo. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | No implementa un motor de diseño SmartArt ni de renderización a nivel de píxel. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Las pruebas con una versión concreta de LibreOffice no implican una representación idéntica en píxeles en todas las suites. |

`PackageFidelity` solo indica si el paquete se puede procesar de forma segura; `SemanticApiDepth` indica
qué parte de la semántica del documento puede comprender y modificar la API; `InteropEvidence` indica qué
software externo y qué versiones se han probado. El nivel máximo de una dimensión no sustituye a las otras.

La fuente única de verdad de las familias semánticas, las operaciones CRUD, las secciones de las normas,
la implementación, las pruebas, las evidencias de interoperabilidad y las limitaciones de los cuatro
formatos principales es
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json).
`eng/Test-SemanticCoverage.ps1` impide en la integración continua las declaraciones incompletas. Consulte
los límites de las fuentes de sala limpia en
[`provenance/semantic-api-clean-room.md`](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/semantic-api-clean-room.md).
