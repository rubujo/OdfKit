---
title: Vejledning om brug, overholdelse, sikkerhed og evidens
_lang: da
---

# Brug, overholdelse, sikkerhed og evidens

## Omfanget af API-dokumentationen

API-referencen genereres fra de offentlige `net10.0`-assemblies og XML-dokumentationen. Håndskrevne kerne-API'er og offentlige udvidelser vises på egne sider. Den store schema-genererede `OdfKit.DOM`-overflade styres fortsat af Public API-baselines for begge TFM'er og Typed DOM coverage. Medlemsbeskrivelser findes aktuelt på engelsk og traditionelt kinesisk; denne danske indgang hævder ikke, at API-medlemmerne er oversat.

## Licens og AI-produktion

Oprindelig OdfKit-kode og webstedsdokumentation anvender CC0 1.0 Universal. Tredjepartspakker, schemaer, værktøjer og fixtures beholder deres egne licenser. Offentligt projektindhold er skrevet, organiseret eller produceret med AI-værktøjer. Webstedet er ikke juridisk rådgivning og tilbyder ingen SLA eller kommerciel skadesløsholdelse. OdfKit er ikke et officielt eller godkendt projekt fra OASIS, The Document Foundation, LibreOffice eller Apache.

## Sikkerheds- og interoperabilitetsgrænser

Behold Reader- og package-ressourcegrænser for filer, der ikke er tillid til, og anvend validering eller rensning. Kontrollerne reducerer risiko, men garanterer ikke fuldstændig sikkerhed mod skadelige dokumenter. Schema-validitet, round-trip eller test med én LibreOffice-version betyder ikke pixelidentisk adfærd i alle kontorpakker.

## Funktioner og evidens

Påstande opdeles i `PackageFidelity`, `SemanticApiDepth` og `InteropEvidence`; én dimension beviser ikke en anden. Offentliggjorte ydelsestal skal angive commit, runtime, miljø og reproducerbar metode. Ydelsesbudgetterne er fortsat i indsamlingsfasen.

- [Åbn API-referencen [en + zh-TW]](xref:OdfKit)
- [Påstande og evidens](project-docs/evidence-index.md)
- [Sikkerhedsgrænser](project-docs/security-limits.md)
- [Immaterielle rettigheder og overholdelse](project-docs/ip-compliance.md)
- [Licens](articles/license.md)
- [Tredjepartsmeddelelser](project-docs/THIRD-PARTY-NOTICES.md)
