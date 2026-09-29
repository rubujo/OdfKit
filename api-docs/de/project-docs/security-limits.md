---
title: Sicherheitsgrenzen für Laden und Streaming-Reader
_lang: de
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
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

## Weitere Ressourcen- und Ausgabeschutzmaßnahmen

Neben den Paket- und Streaming-Reader-Grenzen schützen auch die folgenden festen Grenzen vor nicht vertrauenswürdigen Eingaben. Diese Werte sind derzeit feste Konstanten im Code und lassen sich noch nicht über `OdfLoadOptions` oder ein Optionsobjekt konfigurieren. Bewerten Sie vor dem Erhöhen oder Entfernen einer Grenze die Auswirkungen auf Speicher und Stapel.

| Bereich | Grenze | Verhalten bei Überschreitung |
|---|---|---|
| Tatsächliche entpackte Größe eines ZIP-Eintrags | Darf die im Header angegebene unkomprimierte Größe nicht überschreiten (MMF-Pfad beim Laden über einen Dateipfad) | `SecurityException` |
| Beschädigtes ZIP-Zentralverzeichnis oder ZIP64 | Der MMF-Schnellpfad unterstützt kein ZIP64, und kein Eintrag, der sich nicht vollständig auswerten lässt, darf stillschweigend übersprungen werden | Weicht auf Prüfung und Lesen über `ZipArchive` aus |
| Verschachtelungstiefe von XML-Elementen | 256 Ebenen (`OdfXmlReader.MaxElementDepth`); gilt für DOM-Laden, Flat-ODF-Laden, Profilregelprüfung und RDF-Analyse | Laden löst `SecurityException` aus; die Validierung meldet `ODF0303` oder `ODF0301` |
| Verschachtelungstiefe beim Formelparsen | Je 256 Ebenen für Klammern, Funktionsargumente, Inline-Arrays und aufeinanderfolgende Präfixoperatoren | `InvalidOperationException` |
| Gesamtzahl der Operatorknoten einer Formel | 4.096 (`FormulaParser.MaxOperatorNodes`); binäre, unäre, Prozent- und Bezugsoperatoren zusammen. Verkettete Formeln bilden linkstiefe Bäume, deren Auswertung und Serialisierung ebenenweise rekursiv erfolgen; diese Grenze stellt sicher, dass ein gewöhnlicher Thread-Stapel ausreicht | `InvalidOperationException` |
| Stapelreserve bei Formelrekursion | Parsen, Auswertung, Bereichsermittlung und Serialisierung prüfen vor jeder Rekursionsebene den verbleibenden Stapel (`RuntimeHelpers.EnsureSufficientExecutionStack`); selbst Formeln nahe den Grenzen bringen den Prozess auf Threads mit kleinem 128–256-KB-Stapel nicht zum Absturz | `InsufficientExecutionStackException`; `EvaluateFormulas` wandelt sie in eine Formelauswertungsausnahme oder `#VALUE!` um |
| Länge des Zeichenfolgenergebnisses einer Formel | 1.048.576 Zeichen (`&`, Funktionsergebnisse, `SUBSTITUTE`, `REPT`) | Gibt `#VALUE!` zurück |
| Formelfunktionen, deren Schleifengrenze ein Argument ist | `BINOMDIST` kumulativ 100.000; `CRITBINOM`, `HYPGEOMDIST` kumulativ 100.000; `POISSON` kumulativ 1.000.000; `DB`, `DDB`, `VDB`, `CUMIPMT` Perioden 1.000.000 | Gibt `#NUM!` zurück |
| Zeilen-/Spaltenindex der Tabelle | Zeile 1.048.575, Spalte 16.383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Dokument anhängen | Ein Dokument darf nicht an sich selbst angehängt werden | `ArgumentException` |
| Collaboration `addColumns` | Begrenzt durch Spaltenanzahl und Gesamtzahl der Zellen in `OdtOperationSafetyOptions` | Protokolliert die Sicherheitsgrenze und überspringt den Vorgang |
| Diagramm-Ersatzbild | 4.096 px pro Seite | Wird auf die Grenze begrenzt |
| LibreOffice-Konvertierungsformat | Der Erweiterungsteil vor dem Doppelpunkt darf nicht leer sein und weder `..`, NUL, CR noch LF enthalten | `ArgumentException` |
| SPARQL-Abfrage | `SERVICE`-Klauseln sind nicht zulässig (verhindert, dass die Abfrage-Engine Netzwerkanfragen an beliebige Endpunkte sendet) | `ArgumentException` |

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
