---
title: Límites de seguridad de carga y lectores en flujo
_lang: es
translation_source: docs/security-limits.md
translation_source_sha256: 09dde6295ea4e123b22dc50b79cabbc8414b1d52ac41e3b1cc8811774341ac95
---

# Límites de seguridad de carga y lectores en flujo

> Traducción informativa; en caso de discrepancia, prevalece la fuente en chino tradicional (`zh-TW`).

La carga de paquetes y `OdsStreamReader`/`OdtStreamReader` procesan entradas ZIP/XML no confiables. Los lectores no crean el DOM completo del documento, pero asignan búferes para la
fila actual, el texto de los nodos, la descompresión ZIP y el XML Reader. Un diseño de baja residencia no
elimina los efectos del tamaño de la entrada.

## Límites del paquete principal

`OdfDocument.Load`, las fachadas `Load` y `OdfPackage.Open` comparten los presupuestos de `OdfLoadOptions`.

| Límite | Valor predeterminado | Protección |
|---|---:|---|
| Entradas ZIP | 5,000 | Evita agotar CPU y memoria con muchas entradas pequeñas |
| Tamaño descomprimido de una entrada | 500 MiB | Limita la expansión de una entrada ZIP |
| Tamaño descomprimido total | 1 GiB | Limita la expansión total del paquete |
| Entrada no buscable sin procesar | 1 GiB | Limita el búfer antes de expandir ZIP |
| Caracteres de un documento XML | 64 MiB | Limita el análisis XML y la creación del DOM |

Los cuatro límites ZIP deben ser positivos; cero o valores negativos producen inmediatamente `ArgumentOutOfRangeException`. Solo `MaxXmlCharactersInDocument = 0` desactiva el límite XML. Todos los XML Reader deben prohibir DTD y resolvers externos. Las rutas nuevas deben reutilizar `OdfLoadOptions`; para políticas de contenido use `OdfPackageValidator`, `SanitizeMacros`, la validación de firmas o `pwsh eng/Test-OdfPolicy.ps1`.

## Límites de lectores en flujo

| Reader | Límite | Valor predeterminado |
|---|---|---:|
| ODS | Caracteres XML | 64 MiB |
| ODS | Filas por hoja de cálculo | 1,048,576 |
| ODS | Columnas por fila | 16,384 |
| ODS | Una declaración repeat | 1,048,576 filas; 16,384 columnas |
| ODS | Texto extraído de una celda | 16 MiB |
| ODT | Caracteres XML | 64 MiB |
| ODT | Nodos de texto devueltos | 1,000,000 |
| ODT | Texto extraído de un nodo | 16 MiB |

La lectura falla cuando se supera un límite; no se trunca repeat para seguir devolviendo datos que parezcan
completos. Trate estos fallos como resultados de la protección de recursos y no vuelva a intentarlo
automáticamente con límites desactivados.

## Propiedad de los flujos

El valor predeterminado de `LeaveOpen` en las opciones es `false`. Cuando se establece en `true`, al desechar
el Reader se cierran el flujo de la entrada XML y el ZIP Reader, pero se mantiene abierto el flujo exterior
proporcionado por el autor de la llamada.

## Límite de confianza

Mantenga los límites predeterminados para documentos que no sean de confianza y ejecute primero la validación
de package y schema. Puede aumentar límites concretos para documentos grandes que sean de confianza y deban
procesarse, pero al aumentar los límites de XML o texto también aumenta el riesgo de ataques de denegación de
servicio contra la memoria y de ataques CPU DoS. `MaxXmlCharactersInDocument = 0` solo desactiva el límite de caracteres
XML; los demás límites del Reader continúan vigentes.

Las opciones de los Reader ODS y ODT validan al asignar propiedades: el límite XML admite cero, pero los límites de filas, columnas, repeat, nodos y texto deben ser mayores que cero.

Los límites de seguridad, la validación y el saneamiento reducen el riesgo, pero no garantizan una seguridad
absoluta frente a documentos maliciosos.
