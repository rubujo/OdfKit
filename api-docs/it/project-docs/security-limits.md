---
title: Limiti di sicurezza del caricamento e dei lettori streaming
_lang: it
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
---

# Limiti di sicurezza del caricamento e dei lettori streaming

> Traduzione informativa: in caso di divergenza prevale la fonte zh-TW.

Il caricamento dei pacchetti e `OdsStreamReader`/`OdtStreamReader` elaborano input ZIP/XML non attendibili. I lettori non creano il DOM completo, ma allocano buffer per riga corrente,
testo dei nodi, decompressione ZIP e lettore XML. Bassa memoria residente non significa indipendenza
dalla dimensione dell’input.

## Limiti del pacchetto principale

`OdfDocument.Load`, le facade `Load` e `OdfPackage.Open` condividono i budget di `OdfLoadOptions`.

| Limite | Predefinito | Protezione |
|---|---:|---|
| Voci ZIP | 5,000 | Evita l’esaurimento di CPU e memoria con molte voci piccole |
| Dimensione decompressa di una voce | 500 MiB | Limita l’espansione di una voce ZIP |
| Dimensione decompressa totale | 1 GiB | Limita l’espansione complessiva del pacchetto |
| Input grezzo non ricercabile | 1 GiB | Limita il buffering prima dell’espansione ZIP |
| Caratteri in un documento XML | 64 MiB | Limita l’analisi XML e la costruzione del DOM |

I quattro limiti ZIP devono essere positivi; zero o valori negativi generano subito `ArgumentOutOfRangeException`. Solo `MaxXmlCharactersInDocument = 0` disattiva il limite XML. Tutti i lettori XML devono vietare DTD e resolver esterni. I nuovi percorsi devono riutilizzare `OdfLoadOptions`. I percorsi di convalida dei pacchetti e Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` e le scansioni delle regole dei profili) applicano anche `MaxXmlCharactersInDocument`: la convalida dei pacchetti usa `package.LoadOptions`, mentre quella Flat usa `OdfValidationOptions.LoadOptions` (il valore predefinito di 64 MiB di `OdfLoadOptions` se omesso). Firme, marche temporali, dati di revoca dei certificati e risposte di rete esterne hanno limiti propri più piccoli; il limite del pacchetto principale non li sostituisce. Per le regole sul contenuto usare `OdfPackageValidator`, `SanitizeMacros`, la verifica delle firme o `pwsh eng/Test-OdfPolicy.ps1`.

## Altre protezioni di risorse e output

Oltre ai limiti del pacchetto e dei lettori in streaming, anche i seguenti limiti fissi difendono da input non attendibili. Questi valori sono attualmente costanti fisse nel codice e non sono ancora configurabili tramite `OdfLoadOptions` o un oggetto di opzioni. Valutare l'impatto su memoria e stack prima di aumentare o rimuovere un limite.

| Aspetto | Limite | Comportamento in caso di superamento |
|---|---|---|
| Dimensione decompressa effettiva di una voce ZIP | Non deve superare la dimensione non compressa dichiarata nell'intestazione (percorso MMF del caricamento da percorso di file) | `SecurityException` |
| Directory centrale ZIP danneggiata o ZIP64 | Il percorso rapido MMF non supporta ZIP64 e nessun record non analizzabile per intero può essere saltato in silenzio | Ripiega sulla convalida e lettura tramite `ZipArchive` |
| Profondità di annidamento degli elementi XML | 256 livelli (`OdfXmlReader.MaxElementDepth`); si applica al caricamento DOM, al caricamento Flat ODF, alla convalida delle regole di profilo e all'analisi RDF | Il caricamento genera `SecurityException`; la convalida segnala `ODF0303` o `ODF0301` |
| Profondità di annidamento nell'analisi delle formule | 256 livelli ciascuno per parentesi, argomenti di funzione, array inline e operatori prefisso consecutivi | `InvalidOperationException` |
| Totale dei nodi operatore di una formula | 4.096 (`FormulaParser.MaxOperatorNodes`); operatori binari, unari, di percentuale e di riferimento insieme. Le formule concatenate formano alberi profondi a sinistra la cui valutazione e serializzazione ricorrono livello per livello; questo limite garantisce che basti uno stack di thread ordinario | `InvalidOperationException` |
| Margine di stack nella ricorsione delle formule | Analisi, valutazione, recupero degli intervalli e serializzazione controllano lo stack residuo (`RuntimeHelpers.EnsureSufficientExecutionStack`) prima di entrare in ogni livello di ricorsione; anche le formule vicine ai limiti non causano l'arresto anomalo del processo su thread con stack ridotto di 128–256 KB | `InsufficientExecutionStackException`; `EvaluateFormulas` la converte in un'eccezione di valutazione della formula o in `#VALUE!` |
| Lunghezza del risultato stringa di una formula | 1.048.576 caratteri (`&`, risultati di funzione, `SUBSTITUTE`, `REPT`) | Restituisce `#VALUE!` |
| Funzioni di formula con limite di ciclo dato da un argomento | `BINOMDIST` cumulativo 100.000; `CRITBINOM`, `HYPGEOMDIST` cumulativo 100.000; `POISSON` cumulativo 1.000.000; `DB`, `DDB`, `VDB`, `CUMIPMT` periodi 1.000.000 | Restituisce `#NUM!` |
| Indice di riga/colonna del foglio di calcolo | Riga 1.048.575, colonna 16.383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Accodamento di documenti | Un documento non può essere accodato a se stesso | `ArgumentException` |
| Collaboration `addColumns` | Limitato dal numero di colonne e dal totale delle celle di `OdtOperationSafetyOptions` | Registra il limite di sicurezza e salta l'operazione |
| Immagine di riserva del grafico | 4.096 px per lato | Limitata al valore massimo |
| Formato di conversione LibreOffice | La parte di estensione prima dei due punti non deve essere vuota né contenere `..`, NUL, CR o LF | `ArgumentException` |
| Query SPARQL | Le clausole `SERVICE` non sono consentite (impedisce al motore di query di inviare richieste di rete a endpoint arbitrari) | `ArgumentException` |

## Limiti dei lettori streaming

| Lettore | Limite | Predefinito |
|---|---|---:|
| ODS | Caratteri XML | 64 MiB |
| ODS | Righe per foglio | 1,048,576 |
| ODS | Colonne per riga | 16,384 |
| ODS | Una dichiarazione repeat | righe 1,048,576; colonne 16,384 |
| ODS | Testo di una cella | 16 MiB |
| ODT | Caratteri XML | 64 MiB |
| ODT | Nodi di testo restituiti | 1,000,000 |
| ODT | Testo di un nodo | 16 MiB |

Il superamento di un limite causa un errore: repeat non viene troncato restituendo dati apparentemente
completi. Non riprovare automaticamente senza limiti. `LeaveOpen` è `false` per impostazione predefinita;
con `true` vengono chiusi stream XML e lettore ZIP, ma resta aperto lo stream esterno del chiamante.

Per documenti non attendibili mantenere i limiti e validare pacchetto e schema. Limiti XML o testuali più
alti aumentano i rischi di memoria e CPU DoS. `MaxXmlCharactersInDocument = 0` disattiva solo il limite dei
caratteri XML. Limiti, convalida e sanificazione riducono il rischio senza garantire sicurezza assoluta.

Le opzioni dei lettori ODS e ODT convalidano le regole all’assegnazione: il limite XML accetta zero, mentre i limiti di righe, colonne, repeat, nodi e testo devono essere maggiori di zero.
