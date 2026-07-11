---
title: Límites de seguridad de los lectores en flujo
_lang: es
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Límites de seguridad de los lectores en flujo

> Traducción informativa; en caso de discrepancia, prevalece la fuente en chino tradicional (`zh-TW`).

`OdsStreamReader` y `OdtStreamReader` no crean el DOM completo del documento, pero asignan búferes para la
fila actual, el texto de los nodos, la descompresión ZIP y el XML Reader. Un diseño de baja residencia no
elimina los efectos del tamaño de la entrada.

## Límites predeterminados

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

Los límites de seguridad, la validación y el saneamiento reducen el riesgo, pero no garantizan una seguridad
absoluta frente a documentos maliciosos.
