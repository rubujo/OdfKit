---
title: Leitfaden zu Nutzung, Compliance, Sicherheit und Nachweisen
_lang: de
---

# Nutzung, Compliance, Sicherheit und Nachweise

## Umfang der API-Dokumentation

Die API-Referenz wird aus den öffentlichen `net10.0`-Assemblies und der XML-Dokumentation erzeugt. Handgeschriebene Kern-APIs und öffentliche Erweiterungen erhalten eigene Seiten. Die umfangreiche schema-generierte `OdfKit.DOM`-Oberfläche wird weiterhin durch die Public-API-Baselines beider TFMs und die Typed-DOM-Abdeckung kontrolliert. Mitgliederbeschreibungen liegen derzeit auf Englisch und traditionellem Chinesisch vor; dieser deutsche Einstieg behauptet keine Übersetzung aller API-Mitglieder.

## Lizenz und KI-Erstellung

Ursprünglicher OdfKit-Code und ursprüngliche Website-Dokumentation stehen unter CC0-1.0 Universal. Pakete, Schemas, Werkzeuge und Fixtures Dritter behalten ihre Lizenzen. Öffentliche Projektinhalte wurden mit KI-Werkzeugen geschrieben, organisiert oder erstellt. Die Website ist keine Rechtsberatung und bietet weder SLA noch kommerzielle Freistellung. OdfKit ist kein offizielles oder bestätigtes Projekt von OASIS, The Document Foundation, LibreOffice oder Apache.

## Sicherheits- und Interoperabilitätsgrenzen

Bei nicht vertrauenswürdigen Dateien sollten Reader- und Paket-Ressourcengrenzen aktiv bleiben; außerdem sind Validierung oder Bereinigung einzusetzen. Diese Maßnahmen verringern Risiken, garantieren aber keine absolute Sicherheit vor schädlichen Dokumenten. Schema-Gültigkeit, Round-Trips oder Tests mit einer LibreOffice-Version bedeuten keine pixelidentische Darstellung in jeder Office-Suite.

## Fähigkeiten und Nachweise

Aussagen werden in `PackageFidelity`, `SemanticApiDepth` und `InteropEvidence` getrennt; keine Dimension beweist eine andere. Veröffentlichte Leistungswerte müssen Commit, Runtime, Umgebung und reproduzierbare Methode nennen. Die Leistungsbudgets befinden sich noch in der Erfassungsphase.

- [API-Referenz öffnen [en + zh-TW]](xref:OdfKit)
- [Aussagen und Nachweise [zh-TW]](../../docs/evidence-index.md)
- [Sicherheitsgrenzen [zh-TW]](../../docs/security-limits.md)
- [Geistiges Eigentum und Compliance [zh-TW]](../../docs/ip-compliance.md)
- [Lizenz [zh-TW]](../articles/license.md)
- [Hinweise zu Drittanbietern [zh-TW]](../../THIRD-PARTY-NOTICES.md)
