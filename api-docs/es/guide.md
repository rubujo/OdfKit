---
title: Guía de uso, conformidad, seguridad y evidencias de OdfKit
_lang: es
---

# Guía de uso, conformidad, seguridad y evidencias

## Alcance de la documentación de la API

La referencia de la API se genera a partir de los ensamblados públicos `net10.0` y de la documentación XML. Las API principales escritas a mano y las extensiones públicas se muestran en páginas individuales. La amplia superficie `OdfKit.DOM` generada desde esquemas sigue controlada por las referencias de API pública de ambos TFM y la cobertura de Typed DOM. Los resúmenes de miembros están disponibles en inglés y chino tradicional; las demás entradas de idioma no afirman que los miembros de la API estén traducidos.

## Licencia y producción con IA

El código y la documentación originales de OdfKit usan CC0 1.0 Universal. Los paquetes, esquemas, herramientas y datos de prueba de terceros conservan sus propias licencias. El contenido público se redacta, organiza o produce con herramientas de IA. Este sitio no constituye asesoramiento jurídico y no proporciona ningún SLA ni indemnización comercial.

## Límites de seguridad e interoperabilidad

Mantenga activados los límites de recursos del lector y del paquete para archivos no confiables, y ejecute la validación o el saneamiento cuando corresponda. Estos controles reducen el riesgo, pero no garantizan una protección absoluta frente a documentos maliciosos. La validez del esquema, los ciclos de lectura y escritura o las pruebas con una versión de LibreOffice no implican un resultado idéntico en todos los paquetes ofimáticos.

## Capacidades y evidencias

Las afirmaciones se separan en `PackageFidelity`, `SemanticApiDepth` e `InteropEvidence`; una dimensión no demuestra las demás. Los resultados de rendimiento deben indicar el commit, el runtime, el entorno y el método reproducible.

- [Referencia de la API [en + zh-TW]](xref:OdfKit)
- [Índice de afirmaciones y evidencias](project-docs/evidence-index.md)
- [Límites de seguridad](project-docs/security-limits.md)
- [Propiedad intelectual y conformidad](project-docs/ip-compliance.md)
- [Licencia](articles/license.md)
- [Avisos de terceros](project-docs/THIRD-PARTY-NOTICES.md)
