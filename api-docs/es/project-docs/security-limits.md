---
title: Límites de seguridad de carga y lectores en flujo
_lang: es
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
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

Los cuatro límites ZIP deben ser positivos; cero o valores negativos producen inmediatamente `ArgumentOutOfRangeException`. Solo `MaxXmlCharactersInDocument = 0` desactiva el límite XML. Todos los XML Reader deben prohibir DTD y resolvers externos. Las rutas nuevas deben reutilizar `OdfLoadOptions`. Las rutas de validación de paquetes y Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` y el análisis de reglas de perfil) también aplican `MaxXmlCharactersInDocument`: la validación de paquetes usa `package.LoadOptions` y la validación Flat usa `OdfValidationOptions.LoadOptions` (el valor predeterminado de 64 MiB de `OdfLoadOptions` si se omite). Las firmas, las marcas de tiempo, los datos de revocación de certificados y las respuestas de red externas tienen límites propios más pequeños; el límite del paquete principal no los sustituye. Para políticas de contenido use `OdfPackageValidator`, `SanitizeMacros`, la validación de firmas o `pwsh eng/Test-OdfPolicy.ps1`.

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

## Otras protecciones de recursos y de salida

Además de los límites del paquete y de los lectores de streaming, los siguientes límites fijos también protegen frente a entradas no fiables. Por ahora son constantes fijas en el código y todavía no se pueden configurar mediante `OdfLoadOptions` ni un objeto de opciones. Evalúe el impacto en memoria y pila antes de aumentar o quitar un límite.

| Aspecto | Límite | Comportamiento al superarlo |
|---|---|---|
| Tamaño descomprimido real de una entrada ZIP | No debe superar el tamaño sin comprimir declarado en la cabecera (ruta MMF al cargar desde una ruta de archivo) | `SecurityException` |
| Directorio central ZIP dañado o ZIP64 | La ruta rápida MMF no admite ZIP64 y ningún registro que no pueda analizarse por completo puede omitirse en silencio | Recurre a la validación y lectura mediante `ZipArchive` |
| Profundidad de anidamiento de elementos XML | 256 niveles (`OdfXmlReader.MaxElementDepth`); se aplica a la carga DOM, la carga de Flat ODF, la validación de reglas de perfil y el análisis RDF | La carga lanza `SecurityException`; la validación informa `ODF0303` u `ODF0301` |
| Profundidad de anidamiento al analizar fórmulas | 256 niveles cada uno para paréntesis, argumentos de función, matrices en línea y operadores prefijo consecutivos | `InvalidOperationException` |
| Total de nodos de operador de una fórmula | 4.096 (`FormulaParser.MaxOperatorNodes`); operadores binarios, unarios, de porcentaje y de referencia en conjunto. Las fórmulas encadenadas forman árboles profundos por la izquierda cuya evaluación y serialización recursan nivel a nivel; este límite garantiza que baste una pila de hilo normal | `InvalidOperationException` |
| Margen de pila en la recursión de fórmulas | El análisis, la evaluación, la obtención de rangos y la serialización comprueban la pila restante (`RuntimeHelpers.EnsureSufficientExecutionStack`) antes de entrar en cada nivel de recursión; incluso las fórmulas cercanas a los límites no bloquean el proceso en hilos con pila pequeña de 128–256 KB | `InsufficientExecutionStackException`; `EvaluateFormulas` la convierte en una excepción de evaluación de fórmula o en `#VALUE!` |
| Longitud del resultado de cadena de una fórmula | 1.048.576 caracteres (`&`, resultados de funciones, `SUBSTITUTE`, `REPT`) | Devuelve `#VALUE!` |
| Funciones de fórmula cuyo límite de bucle es un argumento | `BINOMDIST` acumulado 100.000; `CRITBINOM`, `HYPGEOMDIST` acumulado 100.000; `POISSON` acumulado 1.000.000; `DB`, `DDB`, `VDB`, `CUMIPMT` periodos 1.000.000 | Devuelve `#NUM!` |
| Índice de fila/columna de la hoja de cálculo | Fila 1.048.575, columna 16.383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Anexado de documentos | Un documento no puede anexarse a sí mismo | `ArgumentException` |
| Collaboration `addColumns` | Limitado por el número de columnas y el total de celdas de `OdtOperationSafetyOptions` | Registra el límite de seguridad y omite la operación |
| Imagen alternativa de gráfico | 4.096 px por lado | Se ajusta al límite |
| Formato de conversión de LibreOffice | La parte de extensión anterior a los dos puntos no puede estar vacía ni contener `..`, NUL, CR o LF | `ArgumentException` |
| Consulta SPARQL | No se permiten cláusulas `SERVICE` (evita que el motor de consultas envíe solicitudes de red a puntos de conexión arbitrarios) | `ArgumentException` |

## Límite de confianza

Mantenga los límites predeterminados para documentos que no sean de confianza y ejecute primero la validación
de package y schema. Puede aumentar límites concretos para documentos grandes que sean de confianza y deban
procesarse, pero al aumentar los límites de XML o texto también aumenta el riesgo de ataques de denegación de
servicio contra la memoria y de ataques CPU DoS. `MaxXmlCharactersInDocument = 0` solo desactiva el límite de caracteres
XML; los demás límites del Reader continúan vigentes.

Las opciones de los Reader ODS y ODT validan al asignar propiedades: el límite XML admite cero, pero los límites de filas, columnas, repeat, nodos y texto deben ser mayores que cero.

Los límites de seguridad, la validación y el saneamiento reducen el riesgo, pero no garantizan una seguridad
absoluta frente a documentos maliciosos.
