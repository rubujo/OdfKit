using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

public static partial class OdfLocalizer
{
    private static readonly Dictionary<string, Dictionary<string, string>> SupplementalDiagnosticsDictionaries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "GDI+ font measurement failed; falling back to SkiaSharp: {0}.",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "XLSX chart parsing failed; chart import was skipped: {0}.",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "XLSX pivot table parsing failed; pivot table import was skipped: {0}.",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "ODS chart specification parsing failed; the chart was skipped: {0}.",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "Font '{0}' resolves to a TrueType Collection (.ttc) file that PDFsharp does not support; a fallback font was used, so glyphs in the PDF may differ from the original font.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "The PDF Catalog does not contain a /Names dictionary.",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "The PDF Names dictionary does not contain an /EmbeddedFiles dictionary.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "Extracted embedded ODF file '{0}' from the PDF.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "No embedded ODF attachment was found in the PDF.",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "Injected ODF '{0}' as a PDF/A-3 attachment into the output PDF.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "The PDF catalog does not contain an existing /Metadata stream. PDF/A XMP metadata injection was skipped.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "The PDF/A-3 schema declaration was injected into XMP metadata.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "Failed to update the PDF/A XMP metadata stream: {0}.",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "Failed to terminate LibreOffice after timeout: {0}.",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "Failed to safely delete sandbox directory '{0}' after {1} attempts: {2}.",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "ODT JSON operations must be an array or an object with a changes array.",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "ODT JSON operation at index {0} must be an object.",
            ["Err_OdtOperationLog_InvalidPosition"] = "ODT JSON operation position '{0}' must be an array of non-negative integers.",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "ODT JSON operation safety limit exceeded for {0}: {1} > {2}."
        },
        ["zh-TW"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "GDI+ 字型量測失敗，回退至 SkiaSharp：{0}。",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "XLSX 圖表解析失敗，已略過圖表匯入：{0}。",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "XLSX 樞紐分析表解析失敗，已略過樞紐分析表匯入：{0}。",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "ODS 圖表規格解析失敗，已略過該圖表：{0}。",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "字型「{0}」對應的檔案為 PDFsharp 不支援的 TrueType Collection（.ttc）格式，已改用替代字型；輸出 PDF 中對應文字的字形可能與原始字型不同。",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "在 PDF Catalog 中未找到 /Names 字典。",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "在 PDF Names 中未找到 /EmbeddedFiles 字典。",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "成功從 PDF 提取內嵌的 ODF 檔案「{0}」。",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "在 PDF 中未找到內嵌的 ODF 附件。",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "成功將 ODF「{0}」作為 PDF/A-3 附件注入至輸出 PDF 中。",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "PDF 目錄中沒有現置的 /Metadata 串流。已略過 PDF/A XMP 中繼資料注入。",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "成功將 PDF/A-3 結構描述宣告注入至 XMP 中繼資料中。",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "更新 PDF/A XMP 中繼資料資料流失敗：{0}。",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "LibreOffice 逾時後終止程序失敗：{0}。",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "安全刪除沙箱目錄「{0}」失敗，已嘗試 {1} 次：{2}。",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "ODT JSON operations 必須是陣列，或是包含 changes 陣列的物件。",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "索引 {0} 的 ODT JSON operation 必須是物件。",
            ["Err_OdtOperationLog_InvalidPosition"] = "ODT JSON operation 的位置「{0}」必須是非負整數陣列。",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "ODT JSON operation 超出 {0} 安全限制：{1} > {2}。"
        },
        ["de"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "GDI+-Schriftmessung fehlgeschlagen; Rückfall auf SkiaSharp: {0}.",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "XLSX-Diagrammanalyse fehlgeschlagen; Diagrammimport wurde übersprungen: {0}.",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "XLSX-PivotTable-Analyse fehlgeschlagen; PivotTable-Import wurde übersprungen: {0}.",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "ODS-Diagrammspezifikation konnte nicht analysiert werden; das Diagramm wurde übersprungen: {0}.",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "Schriftart '{0}' verweist auf eine TrueType-Collection-Datei (.ttc), die PDFsharp nicht unterstützt; es wurde eine Ersatzschrift verwendet, daher können Glyphen im PDF abweichen.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "Der PDF-Katalog enthält kein /Names-Wörterbuch.",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "Das PDF-Names-Wörterbuch enthält kein /EmbeddedFiles-Wörterbuch.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "Eingebettete ODF-Datei '{0}' aus dem PDF extrahiert.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "Im PDF wurde kein eingebetteter ODF-Anhang gefunden.",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "ODF '{0}' wurde als PDF/A-3-Anhang in das Ausgabe-PDF eingefügt.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "Der PDF-Katalog enthält keinen vorhandenen /Metadata-Stream. PDF/A-XMP-Metadateninjektion wurde übersprungen.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "Die PDF/A-3-Schemadeklaration wurde in die XMP-Metadaten eingefügt.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "Aktualisierung des PDF/A-XMP-Metadatenstroms fehlgeschlagen: {0}.",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "LibreOffice konnte nach dem Timeout nicht beendet werden: {0}.",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "Sandbox-Verzeichnis '{0}' konnte nach {1} Versuchen nicht sicher gelöscht werden: {2}.",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "ODT-JSON-Operationen müssen ein Array oder ein Objekt mit einem changes-Array sein.",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "ODT-JSON-Operation bei Index {0} muss ein Objekt sein.",
            ["Err_OdtOperationLog_InvalidPosition"] = "ODT-JSON-Operationsposition '{0}' muss ein Array nicht negativer Ganzzahlen sein.",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "Sicherheitslimit für ODT-JSON-Operation überschritten für {0}: {1} > {2}."
        },
        ["fr"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "La mesure de police GDI+ a échoué ; repli vers SkiaSharp : {0}.",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "L'analyse du graphique XLSX a échoué ; l'import du graphique a été ignoré : {0}.",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "L'analyse du tableau croisé dynamique XLSX a échoué ; son import a été ignoré : {0}.",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "L'analyse de la spécification du graphique ODS a échoué ; le graphique a été ignoré : {0}.",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "La police '{0}' correspond à un fichier TrueType Collection (.ttc) non pris en charge par PDFsharp ; une police de remplacement a été utilisée, les glyphes du PDF peuvent donc différer.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "Le catalogue PDF ne contient pas de dictionnaire /Names.",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "Le dictionnaire PDF Names ne contient pas de dictionnaire /EmbeddedFiles.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "Fichier ODF intégré '{0}' extrait du PDF.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "Aucune pièce jointe ODF intégrée n'a été trouvée dans le PDF.",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "ODF '{0}' injecté comme pièce jointe PDF/A-3 dans le PDF de sortie.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "Le catalogue PDF ne contient pas de flux /Metadata existant. L'injection de métadonnées XMP PDF/A a été ignorée.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "La déclaration de schéma PDF/A-3 a été injectée dans les métadonnées XMP.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "Échec de la mise à jour du flux de métadonnées XMP PDF/A : {0}.",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "Échec de l'arrêt de LibreOffice après expiration du délai : {0}.",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "Échec de la suppression sécurisée du répertoire bac à sable '{0}' après {1} tentatives : {2}.",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "Les opérations JSON ODT doivent être un tableau ou un objet avec un tableau changes.",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "L'opération JSON ODT à l'index {0} doit être un objet.",
            ["Err_OdtOperationLog_InvalidPosition"] = "La position d'opération JSON ODT '{0}' doit être un tableau d'entiers non négatifs.",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "Limite de sécurité des opérations JSON ODT dépassée pour {0} : {1} > {2}."
        },
        ["nl"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "GDI+-lettertypemeting is mislukt; er wordt teruggevallen op SkiaSharp: {0}.",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "Parseren van XLSX-diagram is mislukt; diagramimport is overgeslagen: {0}.",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "Parseren van XLSX-draaitabel is mislukt; draaitabelimport is overgeslagen: {0}.",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "Parseren van ODS-diagramspecificatie is mislukt; het diagram is overgeslagen: {0}.",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "Lettertype '{0}' verwijst naar een TrueType Collection-bestand (.ttc) dat PDFsharp niet ondersteunt; er is een vervangend lettertype gebruikt, waardoor glyphs in de PDF kunnen afwijken.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "De PDF-catalogus bevat geen /Names-woordenboek.",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "Het PDF Names-woordenboek bevat geen /EmbeddedFiles-woordenboek.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "Ingesloten ODF-bestand '{0}' uit de PDF geëxtraheerd.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "Er is geen ingesloten ODF-bijlage in de PDF gevonden.",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "ODF '{0}' is als PDF/A-3-bijlage in de uitvoer-PDF geïnjecteerd.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "De PDF-catalogus bevat geen bestaande /Metadata-stream. PDF/A XMP-metadata-injectie is overgeslagen.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "De PDF/A-3-schemaverklaring is in XMP-metadata geïnjecteerd.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "Bijwerken van de PDF/A XMP-metadatastream is mislukt: {0}.",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "LibreOffice beëindigen na time-out is mislukt: {0}.",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "Sandboxmap '{0}' kon na {1} pogingen niet veilig worden verwijderd: {2}.",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "ODT JSON-bewerkingen moeten een array zijn of een object met een changes-array.",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "ODT JSON-bewerking op index {0} moet een object zijn.",
            ["Err_OdtOperationLog_InvalidPosition"] = "ODT JSON-bewerkingspositie '{0}' moet een array met niet-negatieve gehele getallen zijn.",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "Veiligheidslimiet voor ODT JSON-bewerkingen overschreden voor {0}: {1} > {2}."
        },
        ["nb"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "GDI+-skriftmåling mislyktes; faller tilbake til SkiaSharp: {0}.",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "Analyse av XLSX-diagram mislyktes; diagramimport ble hoppet over: {0}.",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "Analyse av XLSX-pivottabell mislyktes; pivottabellimport ble hoppet over: {0}.",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "Analyse av ODS-diagramspesifikasjon mislyktes; diagrammet ble hoppet over: {0}.",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "Skriften '{0}' peker til en TrueType Collection-fil (.ttc) som PDFsharp ikke støtter; en erstatningsskrift ble brukt, så glyfer i PDF-en kan avvike.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "PDF-katalogen inneholder ingen /Names-ordbok.",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "PDF Names-ordboken inneholder ingen /EmbeddedFiles-ordbok.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "Innebygd ODF-fil '{0}' ble hentet ut fra PDF-en.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "Fant ingen innebygd ODF-vedlegg i PDF-en.",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "ODF '{0}' ble injisert som PDF/A-3-vedlegg i utdata-PDF-en.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "PDF-katalogen inneholder ingen eksisterende /Metadata-strøm. PDF/A XMP-metadatainjeksjon ble hoppet over.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "PDF/A-3-skjemaerklæringen ble injisert i XMP-metadata.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "Kunne ikke oppdatere PDF/A XMP-metadatastrømmen: {0}.",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "Kunne ikke avslutte LibreOffice etter tidsavbrudd: {0}.",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "Kunne ikke slette sandkassekatalogen '{0}' trygt etter {1} forsøk: {2}.",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "ODT JSON-operasjoner må være en matrise eller et objekt med en changes-matrise.",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "ODT JSON-operasjon ved indeks {0} må være et objekt.",
            ["Err_OdtOperationLog_InvalidPosition"] = "ODT JSON-operasjonsposisjon '{0}' må være en matrise med ikke-negative heltall.",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "Sikkerhetsgrense for ODT JSON-operasjon overskredet for {0}: {1} > {2}."
        },
        ["pt"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "A medição de fonte GDI+ falhou; usando SkiaSharp como fallback: {0}.",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "A análise do gráfico XLSX falhou; a importação do gráfico foi ignorada: {0}.",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "A análise da tabela dinâmica XLSX falhou; a importação da tabela dinâmica foi ignorada: {0}.",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "A análise da especificação do gráfico ODS falhou; o gráfico foi ignorado: {0}.",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "A fonte '{0}' resolve para um arquivo TrueType Collection (.ttc) que o PDFsharp não suporta; uma fonte alternativa foi usada, portanto os glifos no PDF podem diferir.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "O catálogo PDF não contém um dicionário /Names.",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "O dicionário PDF Names não contém um dicionário /EmbeddedFiles.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "Arquivo ODF incorporado '{0}' extraído do PDF.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "Nenhum anexo ODF incorporado foi encontrado no PDF.",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "ODF '{0}' foi injetado como anexo PDF/A-3 no PDF de saída.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "O catálogo PDF não contém um fluxo /Metadata existente. A injeção de metadados XMP PDF/A foi ignorada.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "A declaração de esquema PDF/A-3 foi injetada nos metadados XMP.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "Falha ao atualizar o fluxo de metadados XMP PDF/A: {0}.",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "Falha ao encerrar o LibreOffice após o tempo limite: {0}.",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "Falha ao excluir com segurança o diretório de sandbox '{0}' após {1} tentativas: {2}.",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "As operações JSON ODT devem ser uma matriz ou um objeto com uma matriz changes.",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "A operação JSON ODT no índice {0} deve ser um objeto.",
            ["Err_OdtOperationLog_InvalidPosition"] = "A posição da operação JSON ODT '{0}' deve ser uma matriz de inteiros não negativos.",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "Limite de segurança de operação JSON ODT excedido para {0}: {1} > {2}."
        },
        ["it"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "Misurazione del font GDI+ non riuscita; fallback a SkiaSharp: {0}.",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "Analisi del grafico XLSX non riuscita; importazione del grafico ignorata: {0}.",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "Analisi della tabella pivot XLSX non riuscita; importazione della tabella pivot ignorata: {0}.",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "Analisi della specifica del grafico ODS non riuscita; il grafico è stato ignorato: {0}.",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "Il font '{0}' corrisponde a un file TrueType Collection (.ttc) non supportato da PDFsharp; è stato usato un font sostitutivo, quindi i glifi nel PDF potrebbero differire.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "Il catalogo PDF non contiene un dizionario /Names.",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "Il dizionario PDF Names non contiene un dizionario /EmbeddedFiles.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "File ODF incorporato '{0}' estratto dal PDF.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "Nel PDF non è stato trovato alcun allegato ODF incorporato.",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "ODF '{0}' inserito come allegato PDF/A-3 nel PDF di output.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "Il catalogo PDF non contiene uno stream /Metadata esistente. L'iniezione dei metadati XMP PDF/A è stata ignorata.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "La dichiarazione dello schema PDF/A-3 è stata inserita nei metadati XMP.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "Aggiornamento del flusso di metadati XMP PDF/A non riuscito: {0}.",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "Impossibile terminare LibreOffice dopo il timeout: {0}.",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "Impossibile eliminare in sicurezza la directory sandbox '{0}' dopo {1} tentativi: {2}.",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "Le operazioni JSON ODT devono essere un array o un oggetto con un array changes.",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "L'operazione JSON ODT all'indice {0} deve essere un oggetto.",
            ["Err_OdtOperationLog_InvalidPosition"] = "La posizione dell'operazione JSON ODT '{0}' deve essere un array di interi non negativi.",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "Limite di sicurezza dell'operazione JSON ODT superato per {0}: {1} > {2}."
        },
        ["sk"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "Meranie písma GDI+ zlyhalo; používa sa náhradný SkiaSharp: {0}.",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "Analýza grafu XLSX zlyhala; import grafu bol preskočený: {0}.",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "Analýza kontingenčnej tabuľky XLSX zlyhala; jej import bol preskočený: {0}.",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "Analýza špecifikácie grafu ODS zlyhala; graf bol preskočený: {0}.",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "Písmo '{0}' odkazuje na súbor TrueType Collection (.ttc), ktorý PDFsharp nepodporuje; bolo použité náhradné písmo, takže glyfy v PDF sa môžu líšiť.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "Katalóg PDF neobsahuje slovník /Names.",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "Slovník PDF Names neobsahuje slovník /EmbeddedFiles.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "Vložený súbor ODF '{0}' bol extrahovaný z PDF.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "V PDF sa nenašla žiadna vložená príloha ODF.",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "ODF '{0}' bol vložený ako príloha PDF/A-3 do výstupného PDF.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "Katalóg PDF neobsahuje existujúci tok /Metadata. Vloženie metadát XMP PDF/A bolo preskočené.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "Deklarácia schémy PDF/A-3 bola vložená do metadát XMP.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "Aktualizácia toku metadát XMP PDF/A zlyhala: {0}.",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "Ukončenie LibreOffice po časovom limite zlyhalo: {0}.",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "Bezpečné odstránenie adresára sandboxu '{0}' po {1} pokusoch zlyhalo: {2}.",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "Operácie ODT JSON musia byť pole alebo objekt s poľom changes.",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "Operácia ODT JSON na indexe {0} musí byť objekt.",
            ["Err_OdtOperationLog_InvalidPosition"] = "Pozícia operácie ODT JSON '{0}' musí byť pole nezáporných celých čísel.",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "Bezpečnostný limit operácie ODT JSON bol prekročený pre {0}: {1} > {2}."
        },
        ["da"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "GDI+-skriftmåling mislykkedes; falder tilbage til SkiaSharp: {0}.",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "Analyse af XLSX-diagram mislykkedes; diagramimport blev sprunget over: {0}.",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "Analyse af XLSX-pivottabel mislykkedes; pivottabelimport blev sprunget over: {0}.",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "Analyse af ODS-diagramspecifikation mislykkedes; diagrammet blev sprunget over: {0}.",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "Skrifttypen '{0}' peger på en TrueType Collection-fil (.ttc), som PDFsharp ikke understøtter; en erstatningsskrifttype blev brugt, så glyffer i PDF'en kan afvige.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "PDF-kataloget indeholder ikke en /Names-ordbog.",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "PDF Names-ordbogen indeholder ikke en /EmbeddedFiles-ordbog.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "Indlejret ODF-fil '{0}' blev udtrukket fra PDF'en.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "Der blev ikke fundet en indlejret ODF-vedhæftning i PDF'en.",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "ODF '{0}' blev indsat som PDF/A-3-vedhæftning i output-PDF'en.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "PDF-kataloget indeholder ikke en eksisterende /Metadata-strøm. PDF/A XMP-metadataindsættelse blev sprunget over.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "PDF/A-3-skemaerklæringen blev indsat i XMP-metadata.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "Opdatering af PDF/A XMP-metadatastrømmen mislykkedes: {0}.",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "Kunne ikke afslutte LibreOffice efter timeout: {0}.",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "Kunne ikke slette sandbox-mappen '{0}' sikkert efter {1} forsøg: {2}.",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "ODT JSON-operationer skal være en matrix eller et objekt med en changes-matrix.",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "ODT JSON-operation ved indeks {0} skal være et objekt.",
            ["Err_OdtOperationLog_InvalidPosition"] = "ODT JSON-operationsposition '{0}' skal være en matrix af ikke-negative heltal.",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "Sikkerhedsgrænse for ODT JSON-operation overskredet for {0}: {1} > {2}."
        },
        ["ms"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "Pengukuran fon GDI+ gagal; berundur kepada SkiaSharp: {0}.",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "Penghuraian carta XLSX gagal; import carta dilangkau: {0}.",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "Penghuraian jadual pangsi XLSX gagal; import jadual pangsi dilangkau: {0}.",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "Penghuraian spesifikasi carta ODS gagal; carta dilangkau: {0}.",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "Fon '{0}' merujuk kepada fail TrueType Collection (.ttc) yang tidak disokong oleh PDFsharp; fon gantian digunakan, jadi glif dalam PDF mungkin berbeza.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "Katalog PDF tidak mengandungi kamus /Names.",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "Kamus PDF Names tidak mengandungi kamus /EmbeddedFiles.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "Fail ODF terbenam '{0}' diekstrak daripada PDF.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "Tiada lampiran ODF terbenam ditemui dalam PDF.",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "ODF '{0}' disuntik sebagai lampiran PDF/A-3 ke dalam PDF output.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "Katalog PDF tidak mengandungi strim /Metadata sedia ada. Suntikan metadata XMP PDF/A dilangkau.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "Pengisytiharan skema PDF/A-3 telah disuntik ke dalam metadata XMP.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "Gagal mengemas kini strim metadata XMP PDF/A: {0}.",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "Gagal menamatkan LibreOffice selepas tamat masa: {0}.",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "Gagal memadam direktori sandbox '{0}' dengan selamat selepas {1} percubaan: {2}.",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "Operasi JSON ODT mesti berupa tatasusunan atau objek dengan tatasusunan changes.",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "Operasi JSON ODT pada indeks {0} mesti berupa objek.",
            ["Err_OdtOperationLog_InvalidPosition"] = "Kedudukan operasi JSON ODT '{0}' mesti berupa tatasusunan integer bukan negatif.",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "Had keselamatan operasi JSON ODT dilampaui untuk {0}: {1} > {2}."
        },
        ["ko"] = new(StringComparer.Ordinal)
        {
            ["Diag_OdfTextMeasurer_GdiFontMeasurementFallback"] = "GDI+ 글꼴 측정에 실패했습니다. SkiaSharp로 대체합니다: {0}.",
            ["Diag_XlsxToOdfConverter_ChartImportSkipped"] = "XLSX 차트 구문 분석에 실패했습니다. 차트 가져오기를 건너뜁니다: {0}.",
            ["Diag_XlsxToOdfConverter_PivotTableImportSkipped"] = "XLSX 피벗 테이블 구문 분석에 실패했습니다. 피벗 테이블 가져오기를 건너뜁니다: {0}.",
            ["Diag_OdfToXlsxConverter_ChartExportSkipped"] = "ODS 차트 사양 구문 분석에 실패했습니다. 해당 차트를 건너뜁니다: {0}.",
            ["Diag_OdfPdfExporter_TrueTypeCollectionFontFallback"] = "글꼴 '{0}'은 PDFsharp가 지원하지 않는 TrueType Collection(.ttc) 파일로 확인되었습니다. 대체 글꼴을 사용했으므로 PDF의 글리프가 원래 글꼴과 다를 수 있습니다.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"] = "PDF Catalog에 /Names 사전이 없습니다.",
            ["Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"] = "PDF Names 사전에 /EmbeddedFiles 사전이 없습니다.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted"] = "PDF에서 내장 ODF 파일 '{0}'을(를) 추출했습니다.",
            ["Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"] = "PDF에서 내장 ODF 첨부 파일을 찾을 수 없습니다.",
            ["Diag_OdfHybridPdfHelper_OdfAttachmentInjected"] = "ODF '{0}'을(를) 출력 PDF에 PDF/A-3 첨부 파일로 삽입했습니다.",
            ["Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"] = "PDF 카탈로그에 기존 /Metadata 스트림이 없습니다. PDF/A XMP 메타데이터 삽입을 건너뜁니다.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"] = "PDF/A-3 스키마 선언이 XMP 메타데이터에 삽입되었습니다.",
            ["Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed"] = "PDF/A XMP 메타데이터 스트림을 업데이트하지 못했습니다: {0}.",
            ["Diag_LibreOfficeRenderer_KillAfterTimeoutFailed"] = "시간 초과 후 LibreOffice 종료에 실패했습니다: {0}.",
            ["Diag_LibreOfficeRenderer_SandboxDeleteFailed"] = "샌드박스 디렉터리 '{0}'을(를) {1}번 시도 후에도 안전하게 삭제하지 못했습니다: {2}.",
            ["Err_OdtOperationLog_InvalidEnvelope"] = "ODT JSON 작업은 배열이거나 changes 배열을 포함하는 객체여야 합니다.",
            ["Err_OdtOperationLog_OperationMustBeObject"] = "인덱스 {0}의 ODT JSON 작업은 객체여야 합니다.",
            ["Err_OdtOperationLog_InvalidPosition"] = "ODT JSON 작업 위치 '{0}'은 음수가 아닌 정수 배열이어야 합니다.",
            ["Err_OdtOperationLog_SafetyLimitExceeded"] = "ODT JSON 작업 안전 제한을 초과했습니다({0}: {1} > {2})."
        }
    };

    private static void MergeSupplementalDiagnosticsDictionary(string name, Dictionary<string, string> target)
    {
        if (!SupplementalDiagnosticsDictionaries.TryGetValue(name, out Dictionary<string, string>? translations))
        {
            return;
        }

        foreach (var kvp in translations)
        {
            if (!target.ContainsKey(kvp.Key))
            {
                target.Add(kvp.Key, kvp.Value);
            }
        }
    }
}
