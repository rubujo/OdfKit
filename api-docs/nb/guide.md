---
title: Veiledning for bruk, samsvar, sikkerhet og dokumentasjon
_lang: nb
---

# Bruk, samsvar, sikkerhet og dokumentasjon

## Omfanget av API-dokumentasjonen

API-referansen genereres fra de offentlige `net10.0`-assemblyene og XML-dokumentasjonen. Håndskrevne kjerne-API-er og offentlige utvidelser vises på egne sider. Den store, skjemagenererte `OdfKit.DOM`-overflaten styres fortsatt av Public API-baselines for begge TFM-er og Typed DOM coverage. Medlemsbeskrivelser finnes nå på engelsk og tradisjonell kinesisk; denne norske inngangen hevder ikke at alle API-medlemmer er oversatt.

## Lisens og AI-produksjon

Original OdfKit-kode og original nettstedsdokumentasjon bruker CC0-1.0 Universal. Tredjepartspakker, skjemaer, verktøy og fixtures beholder sine egne lisenser. Offentlig prosjektinnhold er skrevet, organisert eller produsert med AI-verktøy. Nettstedet er ikke juridisk rådgivning og gir ingen SLA eller kommersiell skadesløsholdelse. OdfKit er ikke et offisielt eller godkjent prosjekt fra OASIS, The Document Foundation, LibreOffice eller Apache.

## Sikkerhets- og interoperabilitetsgrenser

Behold ressursgrensene for reader og package for filer du ikke stoler på, og bruk validering eller rensing. Tiltakene reduserer risiko, men garanterer ikke absolutt sikkerhet mot skadelige dokumenter. Skjemagyldighet, round-trip eller test mot én LibreOffice-versjon betyr ikke pikselidentisk resultat i alle kontorpakker.

## Funksjoner og dokumentasjon

Påstander deles i `PackageFidelity`, `SemanticApiDepth` og `InteropEvidence`; én dimensjon beviser ikke en annen. Publiserte ytelsesresultater må angi commit, runtime, miljø og reproduserbar metode. Ytelsesbudsjettene er fortsatt i innsamlingsfasen.

- [Åpne API-referansen](xref:OdfKit)
- [Påstands- og dokumentasjonsindeks](https://github.com/rubujo/OdfKit/blob/main/docs/evidence-index.md)
- [Sikkerhetsgrenser](https://github.com/rubujo/OdfKit/blob/main/docs/security-limits.md)
- [Immaterielle rettigheter og samsvar](https://github.com/rubujo/OdfKit/blob/main/docs/ip-compliance.md)
