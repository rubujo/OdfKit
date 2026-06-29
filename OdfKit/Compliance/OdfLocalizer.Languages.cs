using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

public static partial class OdfLocalizer
{
    static OdfLocalizer()
    {
        // 註冊 12 種語言的字典工廠委派 (Lazy Loading)
        FactoryRegistrations["en"] = CreateEnDictionary;
        FactoryRegistrations["zh-TW"] = CreateZhTwDictionary;
        FactoryRegistrations["de"] = CreateDeDictionary;
        FactoryRegistrations["fr"] = CreateFrDictionary;
        FactoryRegistrations["nl"] = CreateNlDictionary;
        FactoryRegistrations["nb"] = CreateNbDictionary;
        FactoryRegistrations["pt"] = CreatePtDictionary;
        FactoryRegistrations["it"] = CreateItDictionary;
        FactoryRegistrations["sk"] = CreateSkDictionary;
        FactoryRegistrations["da"] = CreateDaDictionary;
        FactoryRegistrations["ms"] = CreateMsDictionary;
        FactoryRegistrations["ko"] = CreateKoDictionary;
    }

    private static Dictionary<string, string> CreateEnDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf10Compatibility"] = "Correct the document structure to remain compatible with ISO/IEC 26300:2006 (ODF 1.0) baseline.",
        ["RequireIso26300Odf12Compatibility"] = "Correct the document structure to remain compatible with ISO/IEC 26300:2015 (ODF 1.2) baseline.",
        ["RequireIso26300Odf13Compatibility"] = "Correct the document structure to remain compatible with ISO/IEC 26300:2025 (ODF 1.3) baseline.",
        ["RequireDeutschlandStackCompatibility"] = "Correct document content and extensions to comply with Germany's Deutschland-Stack compatibility standard.",
        ["RequireGovernmentToolCompatibility"] = "Correct document content to ensure compatibility with Republic of China (Taiwan) government ODF application tools.",
        ["RequireTraditionalChineseMetadataSupport"] = "Correct Traditional Chinese metadata, language tags, and font configurations.",
        ["PreserveCjkLayoutFeatures"] = "Ensure CJK vertical layout, ruby, grid, and font substitution settings are preserved.",
        ["RequireMachineReadableMetadata"] = "Add machine-readable metadata such as title, language, dates, author, and document type.",
        ["RequireAccessibilityMetadata"] = "Add alternative text (svg:title or svg:desc) for images, or table-header-rows for tables.",
        ["RequireOpenStandardDocumentFormat"] = "Ensure editable documents remain based on publicly implementable open standards.",
        ["RequireCrossBorderInteroperability"] = "Ensure core document content does not depend on a single vendor-private extension.",
        ["DisallowInvalidOdfNamespaceExtensions"] = "Remove or correct elements or attributes in ODF namespaces not defined in the schema.",
        ["RequireForeignExtensionIsolation"] = "Place extensions in non-ODF namespaces and ensure they are removable.",
        ["DisallowMacroByDefault"] = "Remove macros, scripts, and event listeners, or use a policy allowing macros.",
        ["RequireSafeExternalResourcePolicy"] = "Use embedded resources or ensure external references comply with deployment policy.",
        ["ODF0001"] = "Add a valid mimetype entry.",
        ["ODF0003"] = "Place the mimetype entry as the first item in the ZIP package.",
        ["ODF0004"] = "Store the mimetype entry without compression.",
        ["ODF0100"] = "Add META-INF/manifest.xml and describe the package content.",
        ["ODF0200"] = "Remove unsafe or non-conforming ZIP entry paths.",
        ["ODF0201"] = "Remove unsafe or non-conforming ZIP entry paths.",
        ["ODF0300"] = "Correct the XML structure according to ODF schemas.",
        ["ODF3000"] = "Correct the XML structure according to ODF schemas.",
        ["ODF3100"] = "Correct the XML structure according to ODF schemas.",
        ["ODF3101"] = "Correct the XML structure according to ODF schemas.",
        ["ODF3102"] = "Correct the XML structure according to ODF schemas.",
        ["ODF0400"] = "Add or correct the office:version attribute in {0}.",
        ["ODF1002"] = "Add or correct the office:version attribute in {0}.",
        ["ODF0500"] = "Correct the office:body content type to match the MIME type and extension.",
        ["ODF0501"] = "Correct the office:body content type to match the MIME type and extension.",
        ["ODF2006"] = "Correct the office:body content type to match the MIME type and extension.",
        ["ODF3002"] = "Correct the office:body content type to match the MIME type and extension.",
        ["ODF1000"] = "Verify that the document version matches the selected compliance profile.",
        ["ODF1001"] = "Verify that the document version matches the selected compliance profile.",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "The formula could not be parsed: {0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "The default evaluator does not support function {0}; preserve the original formula when saving.",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "The formula contains an unrecognized character: '{0}'.",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "Unsupported OpenPGP public key algorithm: {0}. Currently supported: RSA, ElGamal, and ECDH."
    };

    private static Dictionary<string, string> CreateZhTwDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf10Compatibility"] = "修正文件結構，使其與 ISO/IEC 26300:2006 (ODF 1.0) 基準相容。",
        ["RequireIso26300Odf12Compatibility"] = "修正文件結構，使其與 ISO/IEC 26300:2015 (ODF 1.2) 基準相容。",
        ["RequireIso26300Odf13Compatibility"] = "修正文件結構，使其與 ISO/IEC 26300:2025 (ODF 1.3) 基準相容。",
        ["RequireDeutschlandStackCompatibility"] = "修正文件內容與擴充，以符合德國 Deutschland-Stack 辦公軟體相容性規範。",
        ["RequireGovernmentToolCompatibility"] = "修正文件內容，以符合中華民國政府 ODF 文件應用工具相容性規範。",
        ["RequireTraditionalChineseMetadataSupport"] = "修正 Traditional Chinese 相關之中介資料、語系標記與字型名稱設定。",
        ["PreserveCjkLayoutFeatures"] = "確保 CJK 直排佈局、Ruby 旁註、稿紙格線與字型替代設定正確保存。",
        ["RequireMachineReadableMetadata"] = "加入機器可讀之標題、語言、日期、作者與文件類型等中介資料。",
        ["RequireAccessibilityMetadata"] = "為圖片加入 svg:title/svg:desc 替代文字，或為表格加入 table:table-header-rows 等無障礙中介資料。",
        ["RequireOpenStandardDocumentFormat"] = "確保可編輯文件基於公開可實作之開放標準格式。",
        ["RequireCrossBorderInteroperability"] = "確保核心文件內容不依賴單一廠商私有擴充即可讀取。",
        ["DisallowInvalidOdfNamespaceExtensions"] = "移除或更正 ODF 命名空間中未被 schema 定義的元素或屬性。",
        ["RequireForeignExtensionIsolation"] = "將擴充內容放在非 ODF 命名空間，並確保可安全移除。",
        ["DisallowMacroByDefault"] = "移除巨集、指令碼與相關事件監聽器，或改用允許巨集的政策設定。",
        ["RequireSafeExternalResourcePolicy"] = "改用內嵌資源或確認外部連結符合部署政策。",
        ["ODF0001"] = "加入正確的 mimetype entry。",
        ["ODF0003"] = "將 mimetype entry 放在 ZIP 封裝第一個項目。",
        ["ODF0004"] = "將 mimetype entry 設為未壓縮儲存。",
        ["ODF0100"] = "加入 META-INF/manifest.xml 並描述封裝內容。",
        ["ODF0200"] = "移除不安全或不符合 ODF 路徑規則的 ZIP entry。",
        ["ODF0201"] = "移除不安全或不符合 ODF 路徑規則的 ZIP entry。",
        ["ODF0300"] = "依 ODF schema 修正 XML 結構。",
        ["ODF3000"] = "依 ODF schema 修正 XML 結構。",
        ["ODF3100"] = "依 ODF schema 修正 XML 結構。",
        ["ODF3101"] = "依 ODF schema 修正 XML 結構。",
        ["ODF3102"] = "依 ODF schema 修正 XML 結構。",
        ["ODF0400"] = "加入或修正 {0} 的 office:version。",
        ["ODF1002"] = "加入或修正 {0} 的 office:version。",
        ["ODF0500"] = "修正 office:body 內的文件種類，使其與 MIME 類型和副檔名一致。",
        ["ODF0501"] = "修正 office:body 內的文件種類，使其與 MIME 類型和副檔名一致。",
        ["ODF2006"] = "修正 office:body 內的文件種類，使其與 MIME 類型和副檔名一致。",
        ["ODF3002"] = "修正 office:body 內的文件種類，使其與 MIME 類型和副檔名一致。",
        ["ODF1000"] = "確認文件版本與選取的相容性設定檔一致。",
        ["ODF1001"] = "確認文件版本與選取的相容性設定檔一致。",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "公式無法剖析：{0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "預設評估器尚未支援函式 {0}，保存時應保留原公式。",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "公式包含無法識別的字元：'{0}'。",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "不支援的 OpenPGP 公鑰演算法：{0}。目前支援 RSA、ElGamal 與 ECDH。"
    };

    private static Dictionary<string, string> CreateDeDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf12Compatibility"] = "Korrigieren Sie die Dokumentstruktur, um die Kompatibilität mit ISO/IEC 26300:2015 (ODF 1.2) zu wahren.",
        ["RequireDeutschlandStackCompatibility"] = "Korrigieren Sie Dokumentinhalte und Erweiterungen gemäß dem deutschen Deutschland-Stack-Standard.",
        ["RequireAccessibilityMetadata"] = "Fügen Sie Alternativtext (svg:title/svg:desc) für Bilder oder Tabellenkopfzeilen hinzu.",
        ["RequireForeignExtensionIsolation"] = "Platzieren Sie Erweiterungen in Nicht-ODF-Namensräumen und stellen Sie sicher, dass sie entfernbar sind.",
        ["DisallowMacroByDefault"] = "Entfernen Sie Makros, Skripte und Ereignis-Listener oder verwenden Sie eine andere Richtlinie.",
        ["RequireSafeExternalResourcePolicy"] = "Verwenden Sie eingebettete Ressourcen oder überprüfen Sie externe Links.",
        ["ODF0001"] = "Fügen Sie einen gültigen Mimetype-Eintrag hinzu.",
        ["ODF0100"] = "Fügen Sie META-INF/manifest.xml hinzu und beschreiben Sie den Paketinhalt.",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "Die Formel konnte nicht analysiert werden: {0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "Der Standardauswerter unterstützt Funktion {0} nicht; beim Speichern die ursprüngliche Formel beibehalten.",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "Die Formel enthält ein unbekanntes Zeichen: '{0}'.",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "Nicht unterstützter OpenPGP-Algorithmus für öffentliche Schlüssel: {0}. Derzeit unterstützt: RSA, ElGamal und ECDH."
    };

    private static Dictionary<string, string> CreateFrDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf12Compatibility"] = "Corrigez la structure du document pour maintenir la compatibilité avec ISO/IEC 26300:2015 (ODF 1.2).",
        ["RequireAccessibilityMetadata"] = "Ajoutez un texte alternatif (svg:title/svg:desc) pour les images ou les en-têtes de tableau.",
        ["RequireForeignExtensionIsolation"] = "Placez les extensions dans des espaces de noms non-ODF et assurez-vous qu'elles sont amovibles.",
        ["DisallowMacroByDefault"] = "Supprimez les macros, scripts et écouteurs d'événements, ou modifiez la politique de sécurité.",
        ["RequireSafeExternalResourcePolicy"] = "Utilisez des ressources intégrées ou validez les références externes.",
        ["ODF0001"] = "Ajoutez une entrée mimetype valide.",
        ["ODF0100"] = "Ajoutez META-INF/manifest.xml et décrivez le contenu du paquet.",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "La formule n'a pas pu être analysée : {0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "L'évaluateur par défaut ne prend pas en charge la fonction {0}; conservez la formule d'origine lors de l'enregistrement.",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "La formule contient un caractère non reconnu : '{0}'.",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "Algorithme de clé publique OpenPGP non pris en charge : {0}. Actuellement pris en charge : RSA, ElGamal et ECDH."
    };

    private static Dictionary<string, string> CreateNlDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf12Compatibility"] = "Corrigeer de documentstructuur om compatibiliteit met ISO/IEC 26300:2015 (ODF 1.2) te behouden.",
        ["RequireAccessibilityMetadata"] = "Voeg alternatieve tekst (svg:title of svg:desc) toe voor afbeeldingen of tabelkoppen.",
        ["RequireOpenStandardDocumentFormat"] = "Zorg ervoor dat bewerkbare documenten gebaseerd blijven op open standaarden.",
        ["DisallowMacroByDefault"] = "Verwijder macro's, scripts en gebeurtenis-listeners, of pas het beleid aan.",
        ["ODF0001"] = "Voeg een geldige mimetype-vermelding toe.",
        ["ODF0100"] = "Voeg META-INF/manifest.xml toe en beschrijf de pakketinhoud.",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "De formule kon niet worden geparseerd: {0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "De standaardevaluator ondersteunt functie {0} niet; behoud de oorspronkelijke formule bij opslaan.",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "De formule bevat een onbekend teken: '{0}'.",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "Niet-ondersteund OpenPGP-algoritme voor openbare sleutels: {0}. Momenteel ondersteund: RSA, ElGamal en ECDH."
    };

    private static Dictionary<string, string> CreateNbDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf12Compatibility"] = "Korriger dokumentstrukturen for å opprettholde kompatibilitet med ISO/IEC 26300:2015 (ODF 1.2).",
        ["RequireAccessibilityMetadata"] = "Legg til alternativ tekst (svg:title eller svg:desc) for bilder, eller tabelloverskrifter for tabeller.",
        ["DisallowMacroByDefault"] = "Fjern makroer, skripter og hendelseslyttere, eller endre sikkerhetspolicyen.",
        ["RequireSafeExternalResourcePolicy"] = "Bruk innebygde ressurser eller verifiser eksterne referanser.",
        ["ODF0001"] = "Legg til en gyldig mimetype-oppføring.",
        ["ODF0100"] = "Legg til META-INF/manifest.xml og beskriv pakkeinnholdet.",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "Formelen kunne ikke analyseres: {0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "Standard-evaluatoren støtter ikke funksjonen {0}; behold den opprinnelige formelen ved lagring.",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "Formelen inneholder et ukjent tegn: '{0}'.",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "Ustøttet OpenPGP-algoritme for offentlig nøkkel: {0}. Støttes nå: RSA, ElGamal og ECDH."
    };

    private static Dictionary<string, string> CreatePtDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf12Compatibility"] = "Corrija a estrutura do documento para manter a compatibilidade com ISO/IEC 26300:2015 (ODF 1.2).",
        ["RequireAccessibilityMetadata"] = "Adicione texto alternativo (svg:title ou svg:desc) para imagens, ou cabeçalhos para tabelas.",
        ["RequireForeignExtensionIsolation"] = "Coloque as extensões em namespaces não-ODF e garanta que sejam removíveis.",
        ["DisallowMacroByDefault"] = "Remova macros, scripts e ouvintes de eventos, ou altere a política de segurança.",
        ["ODF0001"] = "Adicione uma entrada mimetype válida.",
        ["ODF0100"] = "Adicione META-INF/manifest.xml e descreva o conteúdo do pacote.",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "A fórmula não pôde ser analisada: {0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "O avaliador padrão não oferece suporte à função {0}; preserve a fórmula original ao salvar.",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "A fórmula contém um caractere não reconhecido: '{0}'.",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "Algoritmo de chave pública OpenPGP não suportado: {0}. Atualmente suportado: RSA, ElGamal e ECDH."
    };

    private static Dictionary<string, string> CreateItDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf12Compatibility"] = "Correggi la struttura del documento per mantenere la compatibilità con ISO/IEC 26300:2015 (ODF 1.2).",
        ["RequireAccessibilityMetadata"] = "Aggiungi testo alternativo (svg:title o svg:desc) per le immagini, o intestazioni per le tabelle.",
        ["DisallowMacroByDefault"] = "Rimuovi macro, script e listener di eventi, o modifica la politica di sicurezza.",
        ["ODF0001"] = "Aggiungi una voce mimetype valida.",
        ["ODF0100"] = "Aggiungi META-INF/manifest.xml e descrivi il contenuto del pacchetto.",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "Impossibile analizzare la formula: {0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "Il valutatore predefinito non supporta la funzione {0}; mantenere la formula originale durante il salvataggio.",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "La formula contiene un carattere non riconosciuto: '{0}'.",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "Algoritmo di chiave pubblica OpenPGP non supportato: {0}. Attualmente supportati: RSA, ElGamal ed ECDH."
    };

    private static Dictionary<string, string> CreateSkDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf12Compatibility"] = "Opravte štruktúru dokumentu pre zachovanie kompatibility s ISO/IEC 26300:2015 (ODF 1.2).",
        ["RequireAccessibilityMetadata"] = "Pridajte alternatívny text (svg:title alebo svg:desc) pre obrázky, alebo hlavičky pre tabuľky.",
        ["DisallowMacroByDefault"] = "Odstráňte makrá, skripty a poslucháče udalostí, alebo zmeňte bezpečnostnú politiku.",
        ["ODF0001"] = "Pridajte platný záznam mimetype.",
        ["ODF0100"] = "Pridajte META-INF/manifest.xml a popíšte obsah balíka.",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "Vzorec sa nepodarilo analyzovať: {0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "Predvolený vyhodnocovač nepodporuje funkciu {0}; pri ukladaní zachovajte pôvodný vzorec.",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "Vzorec obsahuje nerozpoznaný znak: '{0}'.",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "Nepodporovaný algoritmus verejného kľúča OpenPGP: {0}. Aktuálne podporované: RSA, ElGamal a ECDH."
    };

    private static Dictionary<string, string> CreateDaDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf12Compatibility"] = "Korriger dokumentstrukturen for at bevare kompatibilitet med ISO/IEC 26300:2015 (ODF 1.2).",
        ["RequireAccessibilityMetadata"] = "Tilføj alternativ tekst (svg:title eller svg:desc) for billeder, eller tabeloverskrifter for tabeller.",
        ["DisallowMacroByDefault"] = "Fjern makroer, scripts og hændelseslyttere, eller skift sikkerhedspolitik.",
        ["ODF0001"] = "Tilføj en gyldig mimetype-indgang.",
        ["ODF0100"] = "Tilføj META-INF/manifest.xml og beskriv pakkeindholdet.",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "Formlen kunne ikke parses: {0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "Standard-evaluatoren understøtter ikke funktionen {0}; bevar den oprindelige formel ved lagring.",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "Formlen indeholder et ukendt tegn: '{0}'.",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "Ikke-understøttet OpenPGP-algoritme for offentlig nøgle: {0}. Understøttes aktuelt: RSA, ElGamal og ECDH."
    };

    private static Dictionary<string, string> CreateMsDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf12Compatibility"] = "Betulkan struktur dokumen untuk mengekalkan keserasian dengan standard ISO/IEC 26300:2015 (ODF 1.2).",
        ["RequireAccessibilityMetadata"] = "Tambah teks alternatif (svg:title atau svg:desc) untuk imej, atau baris pengepala untuk jadual.",
        ["DisallowMacroByDefault"] = "Buang makro, skrip dan pendengar peristiwa, atau tukar dasar keselamatan.",
        ["ODF0001"] = "Tambah entri mimetype yang sah.",
        ["ODF0100"] = "Tambah META-INF/manifest.xml dan terangkan kandungan pakej.",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "Formula tidak dapat dihuraikan: {0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "Penilai lalai tidak menyokong fungsi {0}; kekalkan formula asal semasa menyimpan.",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "Formula mengandungi aksara yang tidak dikenali: '{0}'.",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "Algoritma kunci awam OpenPGP tidak disokong: {0}. Disokong sekarang: RSA, ElGamal dan ECDH."
    };

    private static Dictionary<string, string> CreateKoDictionary() => new(StringComparer.Ordinal)
    {
        ["RequireIso26300Odf12Compatibility"] = "ISO/IEC 26300:2015(ODF 1.2) 기준과의 호환성을 유지하도록 문서 구조를 수정하십시오.",
        ["RequireAccessibilityMetadata"] = "이미지에 대체 텍스트(svg:title 또는 svg:desc)를 추가하거나 테이블에 헤더 행을 추가하십시오.",
        ["DisallowMacroByDefault"] = "매크로, 스크립트 및 이벤트 리스너를 제거하거나 매크로를 허용하는 정책으로 변경하십시오.",
        ["ODF0001"] = "올바른 mimetype 엔트리를 추가하십시오.",
        ["ODF0100"] = "META-INF/manifest.xml을 추가하고 패키지 콘텐츠를 설명하십시오.",
        ["Diag_OdfFormulaSupport_ParseFailed"] = "수식을 구문 분석할 수 없습니다: {0}",
        ["Diag_OdfFormulaSupport_UnsupportedFunction"] = "기본 평가기는 {0} 함수를 지원하지 않습니다. 저장할 때 원래 수식을 유지하세요.",
        ["Diag_OdfFormulaSupport_UnknownCharacter"] = "수식에 인식할 수 없는 문자가 포함되어 있습니다: '{0}'.",
        ["Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm"] = "지원되지 않는 OpenPGP 공개 키 알고리즘: {0}. 현재 지원: RSA, ElGamal 및 ECDH."
    };
}
