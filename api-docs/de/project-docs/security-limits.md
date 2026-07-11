---
title: Sicherheitsgrenzen der Streaming-Reader
_lang: de
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Sicherheitsgrenzen der Streaming-Reader

> Diese Übersetzung dient nur zur Information; bei Abweichungen gilt die maßgebliche zh-TW-Quelle.

`OdsStreamReader` und `OdtStreamReader` erstellen kein vollständiges Dokument-DOM, benötigen aber
Puffer für aktuelle Zeilen, Knotentext, ZIP-Dekomprimierung und den XML-Reader. Geringer Speicherbedarf
bedeutet nicht, dass die Eingabegröße ohne Einfluss bleibt.

| Reader | Grenze | Standardwert |
|---|---|---:|
| ODS | XML-Zeichen | 64 MiB |
| ODS | Zeilen pro Arbeitsblatt | 1,048,576 |
| ODS | Spalten pro Zeile | 16,384 |
| ODS | Einzelne repeat-Angabe | Zeilen 1,048,576; Spalten 16,384 |
| ODS | Text einer Zelle | 16 MiB |
| ODT | XML-Zeichen | 64 MiB |
| ODT | Zurückgegebene Textknoten | 1,000,000 |
| ODT | Text eines Knotens | 16 MiB |

Beim Überschreiten schlägt das Lesen fehl; repeat-Daten werden nicht abgeschnitten und als scheinbar
vollständig zurückgegeben. Wiederholen Sie den Vorgang nicht automatisch ohne Grenzen.

`LeaveOpen` ist standardmäßig `false`. Bei `true` werden XML-Entry-Stream und ZIP-Reader geschlossen,
der äußerste vom Aufrufer bereitgestellte Stream bleibt jedoch geöffnet.

Behalten Sie für nicht vertrauenswürdige Dokumente die Standardgrenzen bei und validieren Sie Paket und
Schema. Höhere XML- oder Textgrenzen erhöhen auch Speicher- und CPU DoS-Risiken.
`MaxXmlCharactersInDocument = 0` deaktiviert nur die XML-Zeichengrenze. Grenzen, Validierung und
Bereinigung verringern Risiken, garantieren aber keine absolute Sicherheit vor bösartigen Dokumenten.
