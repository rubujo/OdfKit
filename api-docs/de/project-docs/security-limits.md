---
title: Sicherheitsgrenzen für Laden und Streaming-Reader
_lang: de
translation_source: docs/security-limits.md
translation_source_sha256: 717f100dcc24a436ae8dd8c64601585f59320489dc452918dd8d97837e4fd8f0
---

# Sicherheitsgrenzen für Laden und Streaming-Reader

> Diese Übersetzung dient nur zur Information; bei Abweichungen gilt die maßgebliche zh-TW-Quelle.

Das Laden von Kernpaketen und `OdsStreamReader`/`OdtStreamReader` verarbeiten nicht vertrauenswürdige ZIP/XML-Eingaben. Die Reader erstellen kein vollständiges Dokument-DOM, benötigen aber
Puffer für aktuelle Zeilen, Knotentext, ZIP-Dekomprimierung und den XML-Reader. Geringer Speicherbedarf
bedeutet nicht, dass die Eingabegröße ohne Einfluss bleibt.

## Grenzen für Kernpakete

`OdfDocument.Load`, formatspezifische `Load`-Fassaden und direkte Aufrufe von `OdfPackage.Open` verwenden gemeinsam die Ressourcenbudgets von `OdfLoadOptions`.

| Grenze | Standardwert | Schutzzweck |
|---|---:|---|
| ZIP-Einträge | 5,000 | Verhindert CPU- und Speichererschöpfung durch viele kleine Einträge |
| Entpackte Größe eines Eintrags | 500 MiB | Begrenzt die Expansion eines ZIP-Eintrags |
| Gesamte entpackte Paketgröße | 1 GiB | Begrenzt die gesamte Expansion aller Einträge |
| Rohgröße nicht durchsuchbarer Eingaben | 1 GiB | Begrenzt die Pufferung vor der ZIP-Expansion |
| Zeichen in einem XML-Dokument | 64 MiB | Begrenzt XML-Verarbeitung und DOM-Aufbau |

Eintragsanzahl, Eintragsgröße, Gesamtexpansion und rohe Paketgröße müssen positiv sein. Null oder negative Werte lösen sofort `ArgumentOutOfRangeException` aus. Nur `MaxXmlCharactersInDocument = 0` deaktiviert die XML-Zeichengrenze; negative Werte bleiben ungültig.

Alle XML-Reader des Kerns müssen externe DTDs und Resolver verbieten. Neue Ladepfade müssen `OdfLoadOptions` oder gleichwertige dokumentierte Budgets verwenden. Die Validierungspfade für Pakete und Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` und Profilregelprüfungen) wenden ebenfalls `MaxXmlCharactersInDocument` an: Die Paketvalidierung verwendet `package.LoadOptions`, die Flat-Validierung `OdfValidationOptions.LoadOptions` (bei fehlender Angabe den Standardwert 64 MiB aus `OdfLoadOptions`). Signaturen, Zeitstempel, Zertifikatswiderrufsdaten und externe Netzwerkantworten besitzen eigene kleinere Grenzen; die Kernpaketgrenze ersetzt diese nicht. Diese Grenzen schützen Ressourcen, nicht den Dokumentinhalt; verwenden Sie für Richtlinien `OdfPackageValidator`, `SanitizeMacros`, Signaturprüfung oder `pwsh eng/Test-OdfPolicy.ps1`.

## Grenzen der Streaming-Reader

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

ODS- und ODT-Reader-Optionen prüfen dieselben Regeln bereits beim Zuweisen der Eigenschaften: Die XML-Grenze akzeptiert null, lehnt negative Werte jedoch ab; Zeilen-, Spalten-, repeat-, Knoten- und Textgrenzen müssen größer als null sein.
