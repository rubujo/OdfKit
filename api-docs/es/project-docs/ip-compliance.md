---
title: Propiedad intelectual y conformidad
_lang: es
translation_source: docs/ip-compliance.md
translation_source_sha256: cc7487b322d8fa5796abdafeb883ded43da72adf294d95f25fa2af21f0ade967
---

# Propiedad intelectual y conformidad (IP Compliance)

> Traducción informativa; no constituye asesoramiento jurídico ni sustituye la consulta de la legislación aplicable.

Este documento está dirigido a los **responsables de conformidad y diligencia debida de compras de quienes
adopten el proyecto**, así como a los **colaboradores**. No es asesoramiento jurídico ni sustituye la consulta
de la legislación de la jurisdicción correspondiente.

Consulte las auditorías de fuentes relacionadas en
[provenance/README.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md) y
[clean-room-source-index.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md).

## 1. Modelo de licencias (licencia compuesta)

| Ámbito | Licencia | Descripción |
|---|---|---|
| Código original del proyecto OdfKit | [CC0-1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) | El proyecto intenta renunciar a los derechos de autor en la medida de lo posible; consulte `LICENSE` en la raíz |
| Dependencias de compilación y ejecución | Principalmente MIT, BSD y similares | **No pasan al dominio público por el uso de CC0**; al redistribuirlas deben conservarse sus avisos y declaraciones de derechos de autor |
| Esquemas OASIS ODF RELAX NG | OASIS Copyright | Se encuentran en `tools/OdfSchemaGenerator/schemas/`; consulte `THIRD-PARTY-NOTICES.md` |
| Casos de prueba de Corpus y Collaboration | Campo `license` de cada caso | Consulte `docs/corpus-manifest.md` y cada archivo `manifest.json` |

**Importante:** al distribuir una aplicación o un paquete que incluya OdfKit y sus dependencias, se deben
cumplir simultáneamente:

1. los efectos de la `LICENSE` del proyecto (CC0) sobre el código original; y
2. las obligaciones de las licencias de terceros indicadas en
   [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md).

No se debe afirmar públicamente que «todo el producto está en el dominio público».

## 2. Titulares de derechos y declaración sobre contenido generado con IA

- El README declara que gran parte del código fuente, la documentación, los ejemplos y las pruebas publicados se han redactado, organizado o generado mediante herramientas de IA.
- Quien actúa como Affirmer de CC0 debe poder disponer de los derechos a los que renuncia. Antes de enviar una contribución, cada colaborador debe comprobar que tiene derecho a incorporarla bajo la licencia del proyecto; consulte la sección sobre DCO.
- El reconocimiento de derechos de autor sobre contenido generado exclusivamente por máquinas varía entre jurisdicciones. Quien necesite un titular de derechos claramente identificado y un compromiso de indemnización por infracción debe evaluar alternativas comerciales o solicitar un contrato de soporte independiente. **Este proyecto de código abierto no proporciona indemnity comercial de forma predeterminada**.

## 3. Sala limpia y fuentes prohibidas

Las fuentes autorizadas, las actuaciones permitidas y las **fuentes que no se pueden copiar** para módulos de
alto riesgo —evaluación de OpenFormula, validación de patrones de esquema, cifrado OpenPGP, JSON Collaboration
y conversiones controladas de formatos, entre otros— se enumeran en
[clean-room-source-index.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md).

Resumen de los principios:

- **Permitido:** normas públicas de OASIS, ISO, RFC, W3C y otras organizaciones; wire shapes públicos; archivos JSON y casos de prueba de referencia redistribuibles; comparaciones de comportamiento y regresiones creadas por el proyecto.
- **Prohibido:** copiar código fuente de LibreOffice C++, Java ODF Toolkit, Apache POI, NPOI o SDK comerciales; utilizar binarios cerrados descompilados como fuente de implementación.
- **Compatible, no portado:** JSON Collaboration es solo un subconjunto compatible de las operations públicas de TDF dentro del ámbito de la extensión; no es un port del código fuente del Toolkit.

## 4. Implementación de normas y marcas comerciales

- ODF, OpenFormula y OOXML son formatos de documentos abiertos o públicamente documentados; implementar readers, writers y validators conforme a sus especificaciones es una práctica normal de interoperabilidad.
- Se permite el uso descriptivo de expresiones como «OpenDocument», «ODF» y «pruebas de compatibilidad con LibreOffice».
- **No se debe** sugerir que el proyecto sea un proyecto oficial, una certificación o un producto avalado por OASIS, The Document Foundation, LibreOffice o Apache.
- «Comparación con ODF Toolkit» se refiere a una comparación de capacidades y evidencias de pruebas; **no** significa que sea un port oficial ni un producto conjunto.

## 5. Certificado de origen del desarrollador para colaboradores (DCO)

Al enviar código o documentación sustancial, el colaborador debe poder declarar, siguiendo el modelo de
Developer Certificate of Origin:

1. que la contribución es de su autoría o que tiene derecho a enviarla bajo la licencia del proyecto;
2. que no ha incluido deliberadamente código fuente de terceros que no tenga derecho a redistribuir;
3. que, si la implementación se basa en normas o documentos públicos, ha respetado el índice de fuentes de sala limpia;
4. que, al añadir una dependencia de terceros, ha actualizado `THIRD-PARTY-NOTICES.md` y los metadatos de paquete necesarios.

Se recomienda incluir `Signed-off-by: Nombre <correo>` en el mensaje del commit o en la descripción del PR.
Las normas de Git del proyecto también exigen la firma GPG.

## 6. Lista de diligencia debida para quienes adopten el proyecto

| Elemento | Acción recomendada |
|---|---|
| Licencias | Lea `LICENSE` y `THIRD-PARTY-NOTICES.md`; incorpore el SBOM y el análisis de licencias a la integración continua |
| Versión | La versión actual es `0.x`; consulte los compromisos de compatibilidad en `CHANGELOG` y [version-delivery.md](https://github.com/rubujo/OdfKit/blob/main/docs/version-delivery.md) |
| Límites funcionales | Use [odf-format-support.md](https://github.com/rubujo/OdfKit/blob/main/docs/odf-format-support.md) y las evidencias de pruebas; no se base solo en afirmaciones comerciales |
| Objetivos excluidos | Consulte [udx-non-goals.md](https://github.com/rubujo/OdfKit/blob/main/docs/udx-non-goals.md), que incluye el motor de maquetación completo y el recálculo de tablas dinámicas |
| Seguridad | Use los límites de recursos de `OdfLoadOptions`; ejecute `Validate` y el saneamiento sobre entradas que no sean de confianza |
| Fuentes | Revise `docs/provenance/`; cuando proceda, compare los directorios de alto riesgo con los proyectos de origen para detectar similitudes |
| Soporte | El proyecto de código abierto no ofrece SLA; los sistemas críticos deben disponer de redundancia y un plan de mantenimiento propio |

## 7. Notificación de vulnerabilidades y problemas de seguridad

Actualmente, el proyecto no ofrece un sistema público de seguimiento de incidencias ni un canal privado para
notificar problemas de seguridad. Hasta que los mantenedores anuncien un canal formal, el proyecto no afirma
que pueda recibir, seguir o procesar notificaciones de seguridad conforme a un nivel de servicio. Si en el
futuro se habilita un sistema público, no se deberán incluir en él todos los detalles de explotación. Los
problemas de seguridad deben tratarse por separado de los asuntos de licencias e infracción.

## 8. Documentos relacionados

- [THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md)
- [provenance/README.md](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md)
- [Índice de fuentes de sala limpia](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md)
- [Comparación con ODF Toolkit](https://github.com/rubujo/OdfKit/blob/main/docs/odf-toolkit-parity.md)
- [Directiva de extensiones externas](https://github.com/rubujo/OdfKit/blob/main/docs/foreign-extension-policy.md)
- [Reglas del manifiesto del corpus](https://github.com/rubujo/OdfKit/blob/main/docs/corpus-manifest.md)
