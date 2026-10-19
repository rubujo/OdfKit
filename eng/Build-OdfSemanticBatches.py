"""Compile complete groups of reviewed, parameterized specification sentence forms.

Only full sentence matches inside locked normative body ranges are accepted.
Unmatched text, PDF, errata and opaque objects remain pending. A compiled semantic
classification establishes neither implementation coverage nor conformance.
"""

import argparse
import csv
import hashlib
import importlib.util
import io
import json
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent.parent
TARGET = ROOT / "docs/odf-semantic-batches"
QNAME = r"[A-Za-z_][\w.-]*:[A-Za-z_][\w.-]*"
REF = r"[0-9]+(?:\.[0-9]+)*"
ELEMENT = rf"<{QNAME}>"
SUBJECT = rf"(?:{QNAME}(?: {REF})? (?:attribute(?: value)?|property)|{ELEMENT}(?: {REF})? element)"
DEFINITION_VERB = r"specifies|defines|represents|represent|contains|determines|indicates|identifies|describes|controls|sets|marks|displays|introduces|changes|binds|is used to|acts as"
DEFAULT_VALUE = r"(?:[A-Za-z0-9_#%+*-]+(?:\.[0-9]+[A-Za-z%]*)?|\(-?[0-9]+(?: -?[0-9]+)+\))"
ELEMENT_REF = rf"{ELEMENT} {REF}"
ATTRIBUTE_REF = rf"{QNAME} {REF}"
SOURCE_PARTS = {"1.1": "spec", "1.2": "schema", "1.3": "schema", "1.4": "schema"}
PASSWORD_ATTRIBUTES = {"text:protection-key", "table:protection-key", "text:protection-key-digest-algorithm", "table:protection-key-digest-algorithm"}
RNG = "{http://relaxng.org/ns/structure/1.0}"
RNG_NODES = {"grammar", "start", "define", "ref", "parentRef", "element", "attribute", "optional", "zeroOrMore",
             "oneOrMore", "group", "choice", "interleave", "list", "mixed", "empty", "text", "notAllowed", "value",
             "data", "param", "except", "anyName", "nsName", "name", "include", "externalRef", "div"}


def listed(item):
    return rf"{item}(?:, {item})*(?:,? and {item})?"


# These are reviewed semantic constructions, not searches for modal words.
# Each pattern must consume the complete source sentence. Enum bodies and free
# prose are deliberately absent; an introductory colon cannot close a group.
FAMILIES = {
    "heading": {"kind": "source-heading", "sdkResponsibilities": ["source-context"],
        "exemption": "non-obligation", "assessment": "實際 text:h 標題只定位來源，不獨立施加義務。"},
    "password-utf8": {"kind": "requirement", "sdkResponsibilities": ["producer", "consumer"],
        "exemption": "none", "assessment": "密碼輸入須先表示為 UTF-8 位元組序列；這不是已儲存的 base64 digest 必須可反解為 UTF-8，也不由此單獨指定雜湊演算法。須綁定單一密碼屬性背景及後續推導條款。",
        "pattern": r"The password shall be provided as a sequence of bytes in UTF-8 encoding\."},
    "automatic-style-prohibition": {"kind": "requirement", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "目前單一屬性不可用於 automatic styles；只限制 style 類別，不推論 common style 必填，也不延伸至同名稱的其他元素情境。",
        "pattern": r"It cannot be used within automatic styles\."},
    "datatype": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "屬性型別宣告；型別 lexical space、值域與引用定義納入 SDK 資料驗證及輸出責任。",
        "pattern": rf"The (?P<subject>{QNAME}) attribute has the data type (?P<datatype>[A-Za-z][\w-]*) (?P<reference>{REF})\."},
    "attribute-location": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "屬性的適用元素清單；結構與輸出位置在 SDK 範圍，不推論屬性必填。",
        "pattern": rf"The (?P<subject>{QNAME}) attribute is usable with the following elements?: (?P<locations>{listed(ELEMENT_REF)})\."},
    "element-location": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "元素的適用父元素清單；結構與輸出位置在 SDK 範圍，不推論出現次數。",
        "pattern": rf"The <(?P<subject>{QNAME})> element is usable within the following elements?: (?P<locations>{listed(ELEMENT_REF)})\."},
    "attribute-members": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "元素屬性清單；每個屬性的型別、必填及條件仍需沿引用條款與 schema 展開。",
        "pattern": rf"The <(?P<subject>{QNAME})> element has the following attributes?: (?P<members>{listed(ATTRIBUTE_REF)})\."},
    "child-members": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "子元素清單；順序、基數及條件仍需沿 schema 與所引用條款展開，不以英文單複數推定。",
        "pattern": rf"The <(?P<subject>{QNAME})> element has the following child elements?: (?P<members>{listed(ELEMENT_REF)})\."},
    "empty-attributes": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "原生元素沒有定義屬性；驗證仍須依版本、Strict／Extended 與 foreign 規則判定，不將擴充條件一併略過。",
        "pattern": rf"The <(?P<subject>{QNAME})> element has no attributes\."},
    "empty-children": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "原生元素沒有定義子元素；character data、foreign 內容與 whitespace 的合法性須另外依 schema 及版本裁決。",
        "pattern": rf"The <(?P<subject>{QNAME})> element has no child elements\."},
    "character-data": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "元素具有 character data 內容；資料保存、讀寫及內容模型屬 SDK 範圍，不推論任意原生子元素均合法。",
        "pattern": rf"The <(?P<subject>{QNAME})> element has character data content\."},
    "data-meaning": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "定義欄位表示的資料語意；資料模型、儲存、讀取及適用的驗證屬 SDK 範圍。此分類不將所有呈現語意轉成輸入拒絕規則，也不證明 SDK 已提供實體渲染。",
        "pattern": rf"(?P<article>The|A|An) (?P<subject>{SUBJECT}) (?P<verb>{DEFINITION_VERB}) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "compound-data-meaning": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "完整句子明確定義欄位，但定義內含條件、否定或義務訊號；保留各訊號、引用及 predicate hash，分派為複合資料語意。後續裁決必須展開條件和角色，不能將整句當無條件 schema 錯誤，也不能因 MAY／SHOULD 而豁免啟用後的資料限制。",
        "pattern": rf"(?P<article>The|A|An) (?P<subject>{SUBJECT}) (?P<verb>{DEFINITION_VERB}) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "default-value": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "屬性缺省時的值語意；不推論 producer 必須明寫預設值，也不將缺省本身當作錯誤。",
        "pattern": rf"The default value for (?P<subject>this attribute|the {QNAME} attribute) is (?P<value>{DEFAULT_VALUE})\."},
    "external-definition": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "以外部標準指定資料定義；引用依賴必須追蹤，不因跨標準而豁免，也不宣告該標準所有功能已實作。",
        "pattern": rf"See(?P<punctuation>\.?) (?P<sectionSign>§|\$) ?(?P<section>{REF}) of \[(?P<standard>[A-Za-z0-9-]+)\](?P<purpose> for details)?\."},
    "local-reference": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "以本版條款定義資料語意；沿所列條號追蹤義務，不推論其他版本相同條號亦有相同意義。",
        "pattern": rf"See (?P<section>{REF})\."},
    "same-meaning": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "明示與所列元素的屬性語意相同；須依目前屬性背景解析參照，不以文字相同推論所有適用元素限制均相同。",
        "pattern": rf"Its meaning is the same as for a <(?P<target>{QNAME})> element\."},
    "field-presence-by-value-type": {"kind": "requirement", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "依 field value type 決定屬性 SHALL 省略或存在；是條件式資料驗證與輸出義務，省略與存在兩個分支各自保留。",
        "pattern": r"(?P<subject>This attribute|It) shall be (?P<presence>omitted|present) for fields whose value type is (?P<negation>not )?(?P<valueType>string)\."},
    "nonunique-name": {"kind": "permission", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "目前屬性的名稱不要求唯一；不能直接加上全文件唯一性拒絕規則，其他名稱與識別碼規則仍各自適用。",
        "pattern": r"The name does not have to be unique\."},
    "implementation-dependent": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "值的語意由 implementation／service 決定；需要追蹤 SDK 產品契約與說明，不是 OASIS 授予的全面驗證豁免。",
        "pattern": r"(?P<subject>The value of this attribute|Its value) is (?P<implementation>implementation-dependent|application and service specific)(?P<service> and service-specific)?\."},
    "style-family": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "style:style 的 style:family 定義所列樣式家族；屬資料表示與讀寫語意，不推論家族內全部格式運算已實作。",
        "pattern": r"(?P<familyName>[A-Za-z ]+) styles are represented by <style:style> elements whose style:family attribute has the value (?P<family>[a-z-]+)\."},
    "enumeration-introduction": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "引入所列屬性的值定義；必須在同版同組找到具體值分支，不以冒號引言本身宣告列舉已解釋。",
        "pattern": rf"The defined values for the (?P<subject>{QNAME}) attribute are:"},
    "enumeration-value": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "所列值的資料解讀語意；與明確的屬性列舉引言及版本綁定，否定語意保留，deprecated 不構成豁免。帶條件或 modal 的複合分支不由此句型吸收。",
        "pattern": r"(?P<value>[a-z][\w-]*): (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "rng-fragment": {"kind": "grammar-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "RELAX NG 構造片段：保留 operator、name、type、combine、default annotation 及內容 hash；語法分類不等於已證明 SDK pattern matcher 與整份官方 grammar 等價。"},
    "rng-annotation": {"kind": "grammar-annotation", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "RELAX NG 片段中的註解；仍可能承載 schema 外的 prose 條件，保留內容 hash 與義務訊號供批次裁決，不將 XML comment 一概豁免。"},
    "legacy-property-purpose": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "舊版以 Use the ... property to specify 表達欄位用途；屬資料表示與解讀語意，不推論每份文件都必須寫入該屬性。條件與引用保留待實作裁決。",
        "pattern": rf"Use the (?P<subject>{QNAME}) property to specify (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "legacy-attribute-meaning": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "舊版 The attribute QName 定義資料語意；保留 predicate 與引用，不把完整顯示／播放當作核心 SDK 已提供的能力。",
        "pattern": rf"The attribute (?P<subject>{QNAME}) (?P<verb>{DEFINITION_VERB}) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "legacy-parent-element": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "以所在父元素說明構造的適用位置；實際順序與基數須沿 grammar 核對，不由單一句子推論必填或只能出現一次。",
        "pattern": rf"This element is contained in the <(?P<parent>{QNAME})> element\."},
    "legacy-local-reference": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "舊版的條款參照；保留該版節號及文字目的，不外推新版同號條款，也不把參照當已完成驗證證據。",
        "pattern": rf"See(?P<also> also)? section (?P<section>{REF})(?P<purpose> for (?:details|information (?:[^.!?]|\.(?=[0-9]))+))?\."},
    "implicit-field-meaning": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "This attribute／property 的資料語意，僅在整組只有一個可證明的屬性背景時接受；保留條件、否定與引用供後續裁決。",
        "pattern": rf"This (?P<subjectKind>attribute|property) (?P<verb>{DEFINITION_VERB}) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "implicit-value-meaning": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "單一已辨識屬性的值域或值語意；保留 is／can be／must be 等原始表達，不將可能值清單誤當完整封閉值域。",
        "pattern": rf"The value of (?P<referenceKind>this|the) (?P<subjectKind>attribute|property) (?P<verb>is|can be|must be|shall be|should be|may be|specifies|determines) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "attribute-presence": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "記錄目前屬性為 optional 或 required 的明示定義；保留其他條款與 grammar 的附加適用條件，不推論預設值必須序列化。",
        "pattern": r"This attribute is (?P<presence>optional|required)\."},
    "content-fallback": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "省略此屬性時改用元素內容；缺省不是自動驗證錯誤，consumer 解讀及 producer 資料表示分別保留。",
        "pattern": r"If this attribute is omitted, the element content is used\."},
    "display-name-fallback": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "缺少 display-name 時沿 style name 取得顯示名稱；不要求 producer 必須明寫相同 display-name。",
        "pattern": r"If this attribute is not present, the display name equals the style name\."},
    "display-name-characters": {"kind": "permission", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "顯示名稱可包含比 style name 更廣的字元；XML 與字元編碼本身的條件仍適用，不能一律套用 NCName 限制。",
        "pattern": r"In contrast to the style name itself, this name may contain arbitrary characters\."},
    "language-tag-recommendation": {"kind": "recommendation", "sdkResponsibilities": ["producer"],
        "exemption": "none", "assessment": "依 RFC5646 §2.2.3 或後繼規則建議必要時才使用該屬性；SHOULD 不是無條件輸入拒絕規則，規範依賴仍須追蹤。",
        "pattern": r"The attribute should be used only if necessary according to the rules of §2\.2\.3 of \[RFC5646\], or its successors\."},
    "inline-values": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "明示資料值清單，並分開保留可選的 extension datatype；有 string 等擴充值域時不能只接受列出的內建值。",
        "pattern": rf"The values of the (?P<subject>{QNAME}) attribute are (?P<values>[A-Za-z0-9][\w-]*(?:, [A-Za-z0-9][\w-]*)*(?:,? or [A-Za-z0-9][\w-]*)?)(?: or a value of type (?P<extensionType>[A-Za-z][\w-]*) (?P<reference>{REF}))?\."},
    "attribute-applicability": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "單一明確屬性的適用、忽略或處理條件；ignored／evaluated 說明解讀語意，不直接轉成禁止該屬性存在的錯誤。cannot be used 等資料限制仍待逐條實作裁決。",
        "pattern": r"This attribute (?P<verb>is ignored|is evaluated|can only be applied|cannot be used|can be used|is used|is only interpreted|should be used) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "attribute-conditional-value": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "以目前屬性值作為前提的行為定義；前提和值分別保留。呈現／計算語意不自動等於輸入資料拒絕規則，SDK 提供相應讀取 API 時須追蹤處理責任。",
        "pattern": r"If the value of (?P<subject>this attribute|the attribute) is (?P<value>[A-Za-z0-9_-]+), (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "attribute-absence-default": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "明確屬性缺省時的 fallback 語意；不是 producer 必填要求，也不代表缺省可丟失其他資料或引用。",
        "pattern": rf"If (?P<subject>this attribute|the {QNAME} attribute) is (?P<absence>omitted|not specified), (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "multiple-local-references": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "列出本版多個參照條款及參照目的；每個參照須分別追蹤，不能視為無義務或跨版本等價。",
        "pattern": rf"See sections? (?P<sections>{listed(REF)})(?P<purpose> for (?:[^.!?]|\.(?=[0-9]))+)?\."},
    "multiple-external-references": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "同一外部標準的多條款依賴；完整保留節號，不授予外部依賴豁免。",
        "pattern": rf"See §(?P<first>{REF}) and §(?P<second>{REF}) of \[(?P<standard>[A-Za-z0-9-]+)\]\."},
    "attribute-set-reference": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "元素的屬性集合明示沿用另一元素；只沿用所述集合，不推論其他子元素、基數、資料語意或外部擴充規則也相同。須沿目標定義解析。",
        "pattern": rf"The attributes that may be associated with the <(?P<subject>{QNAME})> element are the same as those that may be associated with the <(?P<target>{QNAME})> element\."},
    "explicit-value-rule": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "已明示屬性的值型別、資料域或處理義務；保留原始 modal 及 predicate，依角色和條件另行裁決，不能將 can be 清單推定為封閉值域。",
        "pattern": rf"The value of (?P<article>the|a) (?P<subject>{QNAME}) attribute (?P<verb>is|shall be|must be|should be|can be|may be) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "value-type-definition": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "明示欄位值型別；所引型別的 lexical space 與資料語意另沿定義核對。",
        "pattern": rf"The value type of the (?P<subject>{QNAME}) attribute is (?P<datatype>[A-Za-z][\w-]*)\."},
    "typed-value-sequence": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "屬性值是具明確個數的空白分隔型別序列；保存個數、分隔方式與本版型別引用，不把整串當單一型別值，也不將序列誤當可任選其中一項。",
        "pattern": rf"The values of the (?P<subject>{QNAME}) attribute are (?P<count>two|three|four) white space separated values of type (?P<datatype>[A-Za-z][\w-]*) (?P<reference>{REF})\."},
    "value-sequence-arity": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "明示屬性具有指定個數的空白分隔值；僅判定序列基數，個別位置的型別、預設與語意仍須沿後續條款裁決。",
        "pattern": rf"The (?P<subject>{QNAME}) attribute has (?P<count>two|three|four) white space separated values\."},
    "value-union": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "值域由具名 literal 與具引用條號的 datatype 聯集構成；逐項保留所有分支，不能只接受列出的 literal。datatype 的邊界與單位仍須沿本版定義裁決。",
        "pattern": rf"The values of the (?P<subject>{QNAME}) attribute are (?P<branches>(?:a value of type [A-Za-z][\w-]* {REF}|[A-Za-z0-9][\w-]*)(?:(?:, |,? or )(?:a value of type [A-Za-z][\w-]* {REF}|[A-Za-z0-9][\w-]*))+)\."},
    "explicit-field-rule": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "完整句子以具名元素或屬性為主詞，定義資料性質、處理要求或使用權限；保留主詞、原始動詞、條件、否定及引用。角色適用、義務拆分與實作證據仍待後續裁決，不能把 MAY 解讀為啟用功能後無須遵守限制，也不能將 MUST render 一律改為資料拒絕。",
        "pattern": rf"The (?P<subject>{SUBJECT}) (?P<verb>is|are|shall be|must be|should be|may be|can be|shall not be|must not be|should not be|may not be|cannot be|does not|shall|must|should|may|can|provides|supports|references|refers to|selects|uses|allows|enables|disables) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "conditional-enumeration-introduction": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "只有前置屬性具有指定值時，所列目標屬性採用下列值域；保留前提屬性、前提值及目標屬性，不外推其他分支也有相同封閉值域。",
        "pattern": rf"If the value of the (?P<conditionAttribute>{QNAME}) attribute is (?P<conditionValue>[A-Za-z0-9_-]+) the (?P<subject>{QNAME}) attribute on the same element has the defined values:"},
    "conditional-enumeration-value": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "具名列舉分支包含條件或 modal；與同版同組的引言綁定，保留 predicate、否定與義務訊號，不能當作無條件顯示要求或簡單字串列舉已完全裁決。",
        "pattern": r"(?P<value>[a-z][\w-]*): (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "enumeration-default": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "只在同版同組緊接具名列舉分支時，將該分支值記為預設；保留前一來源單位 ID，不推論 producer 必須明寫預設值。",
        "pattern": r"This is the default setting\."},
    "element-specific-default": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "同一屬性依所在元素指定缺省值，保留元素與本版條款參照；不是所有適用元素共用單一缺省，也不是要求 producer 必填。",
        "pattern": rf"For a <(?P<element>{QNAME})> (?P<reference>{REF}) element the default value for this attribute is (?P<value>{DEFAULT_VALUE})\."},
    "character-default": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "字元預設值同時保留實際字元、Unicode code point 與原文名稱，須確認字元與 code point 一致；預設語意不等於輸出必填。",
        "pattern": r"The default value for this attribute is “(?P<character>[^\r\n])” \(U\+(?P<codePoint>[0-9A-F]{4,6}), (?P<unicodeName>[A-Z -]+)\)\."},
    "percentage-reference": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "目前屬性百分比的參考尺寸；屬相對值資料語意，不將需要版面配置的換算一律當成輸入合法性檢查，資料保存仍在 SDK 範圍。",
        "pattern": r"Percentage values are relative to (?P<dimension>the height|the width) of the (?P<referenceObject>drawing page|page|drawing shape)\."},
    "percentage-disallowed": {"kind": "requirement", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "目前唯一已辨識屬性不接受百分比；是該欄位的值域限制，不外推其他長度屬性，實作裁決須核對本版 datatype 與 schema。",
        "pattern": r"Percentage values are not supported\."},
    "referenced-field-definition": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "具名欄位定義後附條款參照；保留句尾節號而非將其丟棄，資料表示與解讀仍須沿該版引用裁決。",
        "pattern": rf"The (?P<subject>{SUBJECT}) (?P<verb>{DEFINITION_VERB}) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\. (?P<trailingReference>{REF})"},
    "datatype-enumeration-value": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "列舉清單中的 datatype 聯集分支；必須具有實際 list-item 來源並綁定同版同屬性引言，不視為一個名為 datatype 的字串 literal。",
        "pattern": rf"a value of type (?P<datatype>[A-Za-z][\w-]*) (?P<reference>{REF})\.?"},
    "datatype-enumeration-description": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "列舉的 datatype 分支附帶資料解讀或條件；保留型別、參照與 predicate，須在來源清單及同屬性引言下解讀。",
        "pattern": rf"a value of type (?P<datatype>[A-Za-z][\w-]*)(?: (?P<reference>{REF}))?: (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "literal-enumeration-item": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "實際 list-item 中的單一 literal 分支；只在同版同屬性列舉引言下接受，不能把任意單字段落視為值域。",
        "pattern": r"(?P<value>[a-z][\w-]*|-?[0-9]+)"},
    "implicit-enumeration-introduction": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "以 this attribute 引入值清單；須先唯一解析屬性，再找到同版清單分支。can be 不由此推定 producer 必須啟用該屬性。",
        "pattern": r"The value of this attribute can be(?P<listIntroduction>:| one of the following:)"},
    "inline-value-definition": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "在唯一屬性背景下定義具名值的資料解讀；保留原始動詞、條件與引用，不因句內出現值名稱就推論完整值域。",
        "pattern": rf"The value (?P<value>[a-z][\w-]*) (?P<verb>{DEFINITION_VERB}) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "region-paragraph-sequence": {"kind": "permission", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "已唯一確認的 style:region-left／center／right 可含 text:p 序列；仍須依 grammar 核對順序、基數及其他內容，不推定區域必填。",
        "pattern": r"That region may contain a sequence of <text:p> elements\."},
    "implicit-element-meaning": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "This element 的資料語意，須由具名元素主詞或 grammar 唯一確認指涉；保留 predicate、條件及引用，不能沿用屬性背景推斷元素。",
        "pattern": rf"This element (?P<verb>{DEFINITION_VERB}) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "element-content-type": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "元素 character content 的 datatype 定義；lexical space、值域與空字串等條件沿該版型別定義裁決，不等於允許任意子元素。",
        "pattern": rf"The <(?P<subject>{QNAME})> element has content of data type (?P<datatype>[A-Za-z][\w-]*) (?P<reference>{REF})\."},
    "element-structure-reference": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "元素的結構沿用另一元素；需沿本版目標 grammar 展開，其他處理語意及擴充規則不因結構相同一併視為等價。",
        "pattern": rf"The structure of the <(?P<subject>{QNAME})> element is the same as that of the <(?P<target>{QNAME})> element\."},
    "data-aware-applicability": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "目前屬性只在 data-aware form 適用；未適用不自動代表 XML 中禁止存在，啟用後的資料與 SDK 處理責任仍須裁決。",
        "pattern": r"It applies only if the form is data-aware\."},
    "shape-size-precedence": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "目前欄位指定的形狀定位語意優先於形狀 size 屬性；ignored 是處理優先序，不要求刪除 size 屬性或以其存在拒絕輸入。",
        "pattern": r"The size attributes of the shape are ignored\."},
    "external-note-reference": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "Note 內明示外部 subtype 清單依賴；保留標準與條號追蹤，不因 Note 字樣丟棄所引資料定義。實際義務強度須沿引用裁決。",
        "pattern": rf"Note: See §(?P<section>{REF}) of \[(?P<standard>[A-Za-z0-9-]+)\] for a list of supported subtypes\."},
    "typical-consumer-range": {"kind": "interoperability-observation", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "此 Note 描述典型 consumer 的支援值，不另行定義硬性值域或合法文件上限。保留互通性背景，實際欄位限制仍依 datatype 及規範條款裁決，不據此將其他值一律判錯。",
        "pattern": r"Note: Typical consumers support values between (?P<lower>[0-9]+) and (?P<upper>[0-9]+)\."},
    "foreign-key-applicability": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "目前欄位僅適用 foreign key；是否需拒絕其他 key 型態中出現該欄位，仍須沿 key 定義、條件與 schema 裁決，不能因涉及資料庫而排除 SDK 資料責任。",
        "pattern": r"It is used only with foreign keys\."},
    "attribute-processing-condition": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "目前欄位只有在另一欄位具指定值時參與處理；保留前提屬性、值與本版參照。未評估該欄位不等於禁止其存在。",
        "pattern": rf"It is only evaluated if the value of the (?P<conditionAttribute>{QNAME}) (?P<reference>{REF}) attribute is (?P<conditionValue>[A-Za-z0-9_-]+)\."},
    "attribute-disabled-condition": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "另一欄位取指定值時停用目前欄位效果；屬處理優先序，不能直接當作互斥屬性輸入錯誤。",
        "pattern": rf"The effect of this attribute is disabled when (?P<conditionAttribute>{QNAME}) (?P<reference>{REF}) has the value of (?P<conditionValue>[A-Za-z0-9_-]+)\."},
    "rdf-property-class": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "RDF property 的適用 class URI 與引用；是 RDF 資料語意，不能將其混作 XML 屬性的可用元素清單，也不能因 RDF 執行能力有限而豁免資料保存。",
        "pattern": rf"This property can be used with the following class: (?P<classUri>urn:[^\s]+) (?P<reference>{REF})\."},
    "implicit-field-rule": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "以 This／The attribute 或 property 為主詞的資料、使用或處理條件；必須先唯一辨識該欄位。原始動詞、條件、否定與引用分開保留，不能把 only evaluated 或 no effect 一律當作禁止出現。",
        "pattern": r"(?P<referenceKind>This|The) (?P<subjectKind>attribute|property) (?P<verb>is|has|can be|shall be|must be|should be|may be|can only be|should not be|may not be|shall not be|must not be|shall|must|should|may|cannot|does not|is not|has no|is only evaluated|can be used) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "implicit-value-condition": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "目前唯一欄位具有指定 literal 值時的處理分支；保留前提值、predicate 與參照，後續必須區分資料域限制及處理／呈現效果。",
        "pattern": r"If the value is (?P<value>[A-Za-z0-9_-]+), (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "explicit-attribute-value-condition": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "另一個具名屬性的值構成目前欄位的處理前提；原始前提屬性、條款、運算關係與值不合併。條件成立後的處理及資料責任須另行裁決，不把可選功能誤當全面豁免。",
        "pattern": rf"If the (?P<conditionAttribute>{QNAME}) attribute (?P<operator>has the value|has a value of|is set to|is) (?P<conditionValue>[A-Za-z0-9_-]+), (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "referenced-attribute-value-condition": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "由具名屬性值引出的條件分支；保留條件的否定、值及本版參照，當前欄位須唯一辨識，其他分支不自動套用相同限制。",
        "pattern": rf"If the value of the (?P<conditionAttribute>{QNAME})(?: (?P<reference>{REF}))? attribute is (?P<negation>not )?(?P<conditionValue>[A-Za-z0-9_-]+), (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "joint-attribute-definition": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "多個具名欄位的共同資料定義；保留欄位集合與共同 predicate，不將共同條件自動拆成每個欄位各自必填，也不將多欄位誤解析成唯一代名詞指涉。",
        "pattern": rf"The (?P<subjects>{listed(QNAME)}) (?P<fieldKind>attributes|properties) (?P<verb>specify|define|represent|contain|determine|indicate|identify|describe|control|set|mark|display|are used to) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "legacy-joint-property-purpose": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "舊版以多個具名屬性共同表達資料用途；保留每個欄位與共同 predicate，各語系或方向分支仍須按本版條件裁決。",
        "pattern": rf"Use the (?P<subjects>{listed(QNAME)}) (?P<fieldKind>properties|attributes) to specify (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "replacement-recommendation": {"kind": "recommendation", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "建議改用具名替代欄位；保留來源欄位與替代目標，建議或 deprecated 不等於舊欄位一律非法，也不豁免 SDK 讀取及保存既有資料。",
        "pattern": rf"Instead of this attribute, the (?P<target>{QNAME})(?: (?P<targetReference>{REF}))? attribute should be used to specify (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "section-property-purpose": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "章節導言定義本節屬性的共同資料用途；實際欄位沿本節子條款展開，保留條件與引用，不從產品種類或呈現用途授予整章 SDK 豁免。",
        "pattern": r"The properties described in this section can be used to (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "section-property-applicability": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "章節導言明示物件適用範圍；原文屬性種類及實際標題各自保留，若出現 texture／material 等編輯差異不能默認等價。SDK 仍承擔適用資料格式、保存及其處理 API 的責任。",
        "pattern": r"The (?P<propertyKind>3D [a-z]+|[a-z]+) properties described in this section are applicable to (?P<objectKind>3D drawing objects|drawing objects)\."},
    "cross-application-properties": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "承接同版同節已明示的物件適用範圍，這些資料屬性可用於各類應用程式；並非要求 SDK 具備完整 3D renderer，也不是格式及保存責任的豁免。",
        "pattern": r"These properties are available for (?P<objectKind>3D drawing objects|drawing objects) contained in all kinds of applications\."},
    "chart-property-container": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "承接同版本節屬性用途，指定 chart style 與 style:chart-properties 容器；記錄 style 參照及本版節號，不由此判定每個 chart 都必須產生所有子屬性。",
        "pattern": rf"They can be used within chart styles \(see section (?P<reference>{REF})\) and are contained in a <style:chart-properties> element\."},
    "broken-source-reference": {"kind": "source-reference-gap", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "來源顯示缺少目標節號的參照；分類為待解的來源依賴缺口，不填入猜測條號、不把它當成已裁決要求，也不因此豁免 SDK 資料責任。後續需核對原始欄位與鄰近正式定義。",
        "pattern": r"(?P<referenceText>See section for details|See also section )\."},
    "appendix-guideline-reference": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "參照本版附錄的使用指引；保留附錄字母，不假設不同版本同字母附錄相同，也不以 guidelines 字樣豁免其他規範資料限制。",
        "pattern": r"See appendix (?P<appendix>[A-Z]) for guidelines how to use this attribute\."},
    "element-details-reference": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "以具名元素與本版節號作為資料定義依賴；不只保留字面節號，也保留實際 QName 供後續交叉裁決。",
        "pattern": rf"Details are described in <(?P<element>{QNAME})> (?P<reference>{REF})\."},
    "token-list-introduction": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "以 token list 引入具名欄位的值分支，保留原文 cardinality；不能把多 token 清單當作單一字串列舉。若同組又明示 empty list 合法，須另行核對 grammar 與來源優先序，不能只取前一句拒絕空清單。",
        "pattern": rf"The value of the (?P<subject>{QNAME}) attribute is a (?P<separator>white space separated|white-space-separated) list of (?P<cardinality>one or more|zero or more) of these values:"},
    "case-spelling-enumeration-description": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "From-top 是來源分支敘述的首字母大小寫；只有同版同欄位的明示值清單包含 from-top 才建立對應。保留原始 label，不能將 XML 值域改為大小寫不敏感，也不直接接受 From-top 作為 literal。",
        "pattern": r"(?P<sourceLabel>From-top): (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "attribute-syntax-reference": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "目前屬性的語法沿用具名屬性；只沿用所述 syntax，不推定最大值／最小值的資料意義、缺省、位置或處理行為均相同。",
        "pattern": rf"The syntax for the attribute is the same as for the attribute (?P<target>{QNAME})\."},
    "external-datetime-format": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "以 XML Schema dateTime 指定日期時間表示；原文 YYYY-MM-DDThh:mm:ss 是格式說明，不以此略過所引用標準的完整 lexical space，具體值域、時區與邊界仍待逐項裁決。",
        "pattern": r"To conform with \[(?P<standard>xmlschema-2)\], the date and time format is (?P<format>YYYY-MM-DDThh:mm:ss)\."},
    "legacy-attribute-members-introduction": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "引入目前元素的屬性清單；須由實際 text:list、完整成員及無歧義元素背景共同成立。清單標籤是屬性定義依賴，不自行當作 XML 名稱或必填宣告。",
        "pattern": rf"The attributes (?:that may be )?associated with (?P<subject>the <{QNAME}> element|this element) are:"},
    "legacy-child-members-introduction": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "引入目前元素可包含的子元素清單；保留具名元素或概念參照，基數、順序與混合內容仍沿完整 grammar，不能由清單名稱推論全部子節點必填。",
        "pattern": rf"The elements that may be contained in (?P<subject>the <{QNAME}> element|this element) are:"},
    "attribute-location-list-introduction": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "目前屬性的適用元素清單；須有無歧義屬性背景與完整同一來源清單，不將未列的其他語法或 foreign 條件一併取消。",
        "pattern": r"This attribute is supported for the following elements:"},
    "member-qualified-reference": {"kind": "definition-dependency", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "在經核對的成員／適用位置清單內引用具名 QName；元素與屬性角色依來源清單區分，非全域允許值或任意 XML 範例。目標定義與 cardinality 仍需展開。",
        "pattern": rf"(?:<(?P<elementMember>{QNAME})>|(?P<bareMember>{QNAME})(?P<attributeKeyword> attribute)?)(?: (?P<reference>{REF})| \(see section (?P<parenthesizedReference>{REF})\))?\.?"},
    "member-concept-reference": {"kind": "definition-dependency", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "來源屬性／子元素清單中的概念標籤，保留原詞及本版參照作為待展開依賴；不能拿顯示名稱當 namespace、XML 名稱、列舉 literal 或功能已實作的證明。僅接受沒有條件／處理述詞的標籤。",
        "pattern": rf"(?P<label>Title \(short accessible name\)|Long description \(in support of accessibility\)|[A-Za-z][A-Za-z ,/-]*?)(?: \(see section (?P<reference>{REF})\)| – see section (?P<dashReference>{REF}))?\.?"},
    "conditional-element-parent": {"kind": "conditional-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "元素若出現必須位於所列父元素；保留 If used 條件，不推論元素必填。僅在來源元素背景唯一時接受，順序與基數仍依 grammar。",
        "pattern": rf"If used, this element must be contained in the <(?P<parent>{QNAME})> element\."},
    "child-definition-reference": {"kind": "definition-dependency", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "引用具名 child element 的定義；沿本版參照解析結構與語意，不僅因 Use 或 event-listeners 名稱就要求每個文件必有該元素或執行腳本。",
        "pattern": rf"Use the <(?P<child>{QNAME})> child element as described in section (?P<reference>{REF})\."},
    "unicode-script-applicability": {"kind": "conditional-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "依本版 Unicode script type 區分 latin／asian／complex 的屬性作用範圍，須綁定目前屬性並追蹤所引分類；這不是要求核心自行繪製字形，也不能將屬性無條件套到所有字元。",
        "pattern": rf"This attribute is evaluated for (?P<characters>any \[UNICODE\] character whose|\[UNICODE\] characters whose) script type is (?P<script>latin|asian|complex)\. (?P<reference>{REF})"},
    "modern-attribute-members-introduction": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "元素屬性清單的具名引言；需配對緊接的實際來源清單與全部成員，不推論其必填、預設或 foreign 限制。",
        "pattern": rf"The (?P<subject><{QNAME}> element) has the following attributes?:"},
    "modern-child-members-introduction": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "元素子節點清單的具名引言；需核對來源清單，順序及基數仍依 grammar，不能由清單引言判定任意組合都合法。",
        "pattern": rf"The (?P<subject><{QNAME}> element) has the following child elements?:"},
    "named-attribute-location-list-introduction": {"kind": "definition", "sdkResponsibilities": ["validator", "producer"],
        "exemption": "none", "assessment": "具名屬性的適用元素清單；配對完整來源清單與明確屬性背景，不取消各元素本身的額外限制。",
        "pattern": rf"The (?P<subject>{QNAME} attribute) is usable with the following elements?:"},
    "element-external-conformance": {"kind": "definition-dependency", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "目前元素依 XLink 定義處理；保留該版外部依賴與元素背景，不由此要求核心實作完整外部應用 UI，也不略過 SDK 的資料表示。",
        "pattern": r"This element conforms to the (?P<standard>XLink) Specification\."},
    "standard-citation-reference": {"kind": "definition-dependency", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "本版標準引文的技術依賴；沿本文件 bibliography 解讀其版本與範圍，不自動換成最新標準或宣告全部外部功能已符合。",
        "pattern": r"See \[(?P<standard>[A-Za-z0-9.-]+)\]\."},
    "section-detail-reference": {"kind": "definition-dependency", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "本版詳細定義的具體條號依賴；保留所述屬性／語法主題，不把相同條號跨版套用或將被引用要求當已實作。",
        "pattern": rf"See section (?P<reference>{REF}) for (?P<topic>detailed information about these attributes|detailed information on these attributes|detailed information on this attribute|a full explanation of these properties|information on the number format attributes|details)\."},
    "shared-attribute-set": {"kind": "definition-dependency", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "目前元素使用目標元素相同的屬性集合；只重用所述屬性，不擅自複製整個 child content、元素名稱或其他處理語意。完整集合沿目標及 grammar 展開。",
        "pattern": rf"The attributes that may be associated with this element are the same as those that can be associated with the <(?P<target>{QNAME})> element\."},
    "legacy-element-purpose": {"kind": "compound-definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "使用英文顯示名稱的元素用途定義；只有與來源章節標題一致、且 grammar／具名背景可唯一定位元素時接受。條件、否定、許可及依賴仍須展開，不以用途文字直接證明實作。",
        "pattern": r"The (?P<displayName>[a-z][a-z -]+) element (?P<verb>determines|specifies|contains|defines|represents|provides) (?P<predicate>(?:[^.!?]|\.(?=[0-9]))+)\."},
    "attribute-deprecation-status": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "目前屬性被標為 deprecated；不是一律不合法或可以刪除的許可。實際版本、conformance profile、替代與相容規則仍須另外裁決。",
        "pattern": r"The use of this attribute is deprecated\."},
    "named-attribute-deprecation": {"kind": "definition", "sdkResponsibilities": ["validator", "producer", "consumer"],
        "exemption": "none", "assessment": "具名舊欄位與建議替代目標；保留本版引用，不以 deprecation 單句新增禁止舊欄位、值相等、唯一性或全部必填規則。",
        "pattern": rf"The (?P<subject>{QNAME} attribute) is deprecated in favor of (?P<target>{QNAME})\. (?P<reference>{REF})"},
}
SCOPE_POLICY = {
    "validator": "依原版條文與 conformance class 核對相關資料表示、型別及引用；語意分類不決定錯誤等級。",
    "producer": "SDK 寫入或保留該構造時適用；可選構造不代表每份輸出都必須產生它。",
    "consumer": "SDK 提供該資料的語意讀取、預設值／樣式解析或計算 API 時適用相應處理義務；資料保留仍有獨立責任。",
    "hostBoundary": "完整物理排版與實體渲染依產品範圍交由 renderer；不以此排除資料格式、儲存、語法或 SDK 自身處理能力的義務。",
    "exemptions": "此編譯器不授予 SDK 範圍外豁免。只有實際 text:h 標題標為不獨立施加義務。",
    "evidence": ["docs/udx-non-goals.md", "OdfKit/Compliance/OdfSchemaPatternValidator.cs", "OdfKit/Core/OdfPackage.cs"],
}
PATTERNS = {key: re.compile(value["pattern"]) for key, value in FAMILIES.items() if "pattern" in value}


def digest(value):
    return hashlib.sha256(value).hexdigest()


def code_hash():
    return digest(Path(__file__).read_bytes().replace(b"\r\n", b"\n"))


def family_hash():
    return digest(json.dumps({"families": FAMILIES, "scopePolicy": SCOPE_POLICY}, ensure_ascii=False, sort_keys=True).encode("utf-8"))


def validate_ranges(policy):
    if policy.get("schemaVersion") != 1:
        raise ValueError("Unknown semantic source range schema")
    seen = set()
    for region in policy["ranges"]:
        version = region["version"]
        if version in seen or region["part"] != SOURCE_PARTS.get(version):
            raise ValueError("Duplicate or unsupported semantic source range")
        seen.add(version)
        last_title = "MIME Types and File Name Extensions (Non Normative)" if version == "1.1" else "OpenDocument Relax NG Schema"
        if region["first"]["title"] != "Document Structure" or region["afterLast"]["title"] != last_title:
            raise ValueError("Unreviewed semantic body boundary")
        for marker in (region["first"], region["afterLast"]):
            if not re.fullmatch(rf"ODF-{re.escape(version)}/{region['part']}/B[0-9]{{5}}", marker["id"]) or digest(marker["title"].encode("utf-8")) != marker["textSha256"]:
                raise ValueError("Semantic boundary version or title hash changed")
        if region["first"]["id"] >= region["afterLast"]["id"]:
            raise ValueError("Empty or reversed semantic body range")
        code_styles = region.get("codeStyleRoots", {})
        if code_styles:
            approved_profiles = {"RelaxNG": "document-grammar", "RelaxNG_20_Manifest": "manifest-grammar",
                                 "RelaxNG_20_Strict": "strict-schema-suffix"}
            if version != "1.1" or set(code_styles) != set(approved_profiles) or len(region.get("grammarStyleBasis", [])) != 3:
                raise ValueError("Unreviewed normative grammar style family")
            for name, record in code_styles.items():
                if record["profile"] != approved_profiles[name] or not re.fullmatch(r"[0-9a-f]{64}", record["styleSha256"]):
                    raise ValueError("Normative grammar profile changed")
    for region in policy.get("informativeContexts", []):
        first, last = region["first"], region["afterLast"]
        if first["id"].rsplit("/", 1)[0] != last["id"].rsplit("/", 1)[0] or first["id"] >= last["id"]:
            raise ValueError("Invalid informative context boundary")
        for marker in (first, last):
            if not re.fullmatch(r"ODF-1\.[0-4]/(?:spec|schema)/B[0-9]{5}", marker["id"]) or not marker["title"].endswith("(Non Normative)") or digest(marker["title"].encode("utf-8")) != marker["textSha256"]:
                raise ValueError("Unreviewed informative source marker")


def parse_rng(text):
    closing = re.fullmatch(r"</(?P<name>[A-Za-z][A-Za-z0-9]*)>", text)
    if closing:
        name = closing["name"]
        return ("rng-fragment", {"action": "close", "nodes": [{"kind": name, "attributes": {}, "textSha256": digest(b"")}]}) if name in RNG_NODES else None
    if not text.startswith("<"):
        return None
    comment = re.fullmatch(r"<!--(?P<inner>(?:[^-]|-(?!-))*)-->", text, re.S)
    if comment:
        try:
            ET.fromstring("<root>" + text + "</root>")
        except ET.ParseError:
            return None
        inner = comment["inner"]
        return "rng-annotation", {"commentSha256": digest(inner.encode("utf-8")),
            "scopeSignals": sorted(set(word.lower() for word in re.findall(r"\b(?:shall|must|should|may|if|unless)\b", inner, re.I)))}
    if "<!--" in text:
        return None
    start = re.match(r"<(?P<name>[A-Za-z][A-Za-z0-9]*)\b", text)
    if start is None or start["name"] not in RNG_NODES:
        return None
    prefix = '<root xmlns="http://relaxng.org/ns/structure/1.0" xmlns:a="http://relaxng.org/ns/compatibility/annotations/1.0">'
    action, root = "complete", None
    try:
        root = ET.fromstring(prefix + text + "</root>")
    except ET.ParseError:
        if text.endswith("/>") or text.count("<") != 1 or not text.endswith(">"):
            return None
        try:
            root = ET.fromstring(prefix + text + f"</{start['name']}></root>")
            action = "open"
        except ET.ParseError:
            return None
    if root is None or len(root) != 1 or (root.text or "").strip() or (root[0].tail or "").strip():
        return None
    nodes = []
    for node in root[0].iter():
        if not node.tag.startswith(RNG) or node.tag[len(RNG):] not in RNG_NODES:
            return None
        if (node.tail or "").strip() or ((node.text or "").strip() and node.tag[len(RNG):] not in ("value", "param", "name")):
            return None
        nodes.append({"kind": node.tag[len(RNG):], "attributes": dict(sorted(node.attrib.items())),
                      "textSha256": digest((node.text or "").encode("utf-8"))})
    return "rng-fragment", {"action": action, "nodes": nodes}


PREDICATE_FAMILIES = {"data-meaning", "compound-data-meaning", "enumeration-value", "legacy-property-purpose",
                      "legacy-attribute-meaning", "implicit-field-meaning", "implicit-value-meaning",
                      "attribute-applicability", "attribute-conditional-value", "attribute-absence-default", "explicit-value-rule",
                      "explicit-field-rule", "conditional-enumeration-value", "referenced-field-definition",
                      "datatype-enumeration-description", "inline-value-definition", "implicit-element-meaning",
                      "implicit-field-rule", "implicit-value-condition", "explicit-attribute-value-condition",
                      "referenced-attribute-value-condition", "joint-attribute-definition", "legacy-joint-property-purpose",
                      "replacement-recommendation", "section-property-purpose", "case-spelling-enumeration-description"}
PREDICATE_FAMILIES.add("legacy-element-purpose")
IMPLICIT_ATTRIBUTE_FAMILIES = {"same-meaning", "field-presence-by-value-type", "nonunique-name", "implementation-dependent",
                              "enumeration-value", "implicit-field-meaning", "implicit-value-meaning", "attribute-presence",
                              "content-fallback", "display-name-fallback", "display-name-characters", "language-tag-recommendation",
                              "attribute-applicability", "attribute-conditional-value", "conditional-enumeration-value",
                              "element-specific-default", "character-default", "percentage-reference", "percentage-disallowed",
                              "datatype-enumeration-value", "datatype-enumeration-description", "literal-enumeration-item",
                              "implicit-enumeration-introduction", "inline-value-definition", "data-aware-applicability",
                              "shape-size-precedence", "typical-consumer-range", "foreign-key-applicability",
                              "attribute-processing-condition", "attribute-disabled-condition", "rdf-property-class",
                              "implicit-field-rule", "implicit-value-condition", "explicit-attribute-value-condition",
                              "referenced-attribute-value-condition", "replacement-recommendation", "attribute-syntax-reference",
                              "case-spelling-enumeration-description"}
IMPLICIT_ATTRIBUTE_FAMILIES |= {"attribute-location-list-introduction", "unicode-script-applicability"}
IMPLICIT_ATTRIBUTE_FAMILIES.add("named-attribute-location-list-introduction")
IMPLICIT_ATTRIBUTE_FAMILIES.add("attribute-deprecation-status")
IMPLICIT_ATTRIBUTE_FAMILIES |= {"password-utf8", "automatic-style-prohibition"}
SIGNAL_FAMILIES = {"compound-data-meaning", "implicit-field-meaning", "implicit-value-meaning",
                   "attribute-applicability", "attribute-conditional-value", "attribute-absence-default", "explicit-value-rule",
                   "explicit-field-rule", "conditional-enumeration-value", "referenced-field-definition",
                   "datatype-enumeration-description", "inline-value-definition", "implicit-element-meaning",
                   "implicit-field-rule", "implicit-value-condition", "explicit-attribute-value-condition",
                   "referenced-attribute-value-condition", "joint-attribute-definition", "legacy-joint-property-purpose",
                   "section-property-purpose", "case-spelling-enumeration-description"}
SIGNAL_FAMILIES.add("legacy-element-purpose")
IMPLICIT_ELEMENT_FAMILIES = {"region-paragraph-sequence", "implicit-element-meaning"}
IMPLICIT_ELEMENT_FAMILIES.add("conditional-element-parent")
IMPLICIT_ELEMENT_FAMILIES |= {"legacy-attribute-members-introduction", "legacy-child-members-introduction"}
IMPLICIT_ELEMENT_FAMILIES |= {"modern-attribute-members-introduction", "modern-child-members-introduction",
                             "element-external-conformance", "shared-attribute-set", "legacy-element-purpose"}
MEMBER_INTRODUCTIONS = {"legacy-attribute-members-introduction": "attributes",
                        "legacy-child-members-introduction": "children", "attribute-location-list-introduction": "locations"}
MEMBER_INTRODUCTIONS.update({"modern-attribute-members-introduction": "attributes", "modern-child-members-introduction": "children",
                             "named-attribute-location-list-introduction": "locations"})
MEMBER_ITEMS = {"member-qualified-reference", "member-concept-reference"}
ENUM_INTRODUCTIONS = {"enumeration-introduction", "conditional-enumeration-introduction", "implicit-enumeration-introduction", "token-list-introduction"}
ENUM_VALUES = {"enumeration-value", "conditional-enumeration-value", "datatype-enumeration-value",
               "datatype-enumeration-description", "literal-enumeration-item", "case-spelling-enumeration-description"}
LIST_VALUES = {"datatype-enumeration-value", "datatype-enumeration-description", "literal-enumeration-item",
               "case-spelling-enumeration-description"}


def needs_attribute_context(family, parameters):
    return family in IMPLICIT_ATTRIBUTE_FAMILIES or (
        family in ("default-value", "attribute-absence-default") and parameters.get("subject") == "this attribute")


def parse_sentence(material, text):
    if material == "heading":
        return "heading", {}
    if material != "paragraph":
        return None
    grammar = parse_rng(text)
    if grammar is not None:
        return grammar
    for identifier, pattern in PATTERNS.items():
        match = pattern.fullmatch(text)
        if not match:
            continue
        parameters = match.groupdict()
        if identifier == "member-concept-reference" and not member_label_allowed(parameters["label"]):
            continue
        if identifier == "case-spelling-enumeration-description":
            parameters["canonicalValue"] = "from-top"
        if identifier == "character-default" and ord(parameters["character"]) != int(parameters["codePoint"], 16):
            return None
        if identifier in PREDICATE_FAMILIES:
            # Conditional and modal-bearing clauses require separate review; a
            # field definition cannot absorb them as an unconditional meaning.
            forbidden = r"\b(if|unless|shall|must|should|may|can|will|when|only|otherwise|except|provided|depending)\b"
            signals = sorted(set(word.lower() for word in re.findall(forbidden, parameters["predicate"], re.I)))
            negative = bool(re.search(r"\bnot\b", parameters["predicate"], re.I))
            if identifier == "data-meaning" and (signals or negative):
                continue
            if identifier == "enumeration-value" and signals:
                continue
            if identifier == "conditional-enumeration-value" and not signals:
                continue
            if identifier == "compound-data-meaning":
                if not signals and not negative:
                    continue
                parameters["scopeSignals"] = signals + (["not"] if negative else [])
            if identifier in SIGNAL_FAMILIES - {"compound-data-meaning"}:
                parameters["scopeSignals"] = signals + (["not"] if negative else [])
            parameters["predicateSha256"] = digest(parameters.pop("predicate").encode("utf-8"))
            parameters["referencedQNames"] = sorted(set(re.findall(QNAME, text)))
            parameters["referencedSections"] = re.findall(r"\b[0-9]+(?:\.[0-9]+)+\b", text)
        if needs_attribute_context(identifier, parameters):
            parameters["requiresAttributeContext"] = True
        return identifier, parameters
    return None


def member_label_allowed(label):
    if label in ("Title (short accessible name)", "Long description (in support of accessibility)"):
        return True
    return re.fullmatch(r"[A-Za-z][A-Za-z ,/-]*", label) is not None and not re.search(
        r"\b(?:if|unless|shall|must|should|may|can|will|when|where|which|whose|that|not|only|otherwise|except|provided|depending|is|are|has|have|specifies|contains|returns|see|refer|this|these|it|they)\b", label, re.I)


def bind_member_lists(bindings, locations, bind=False):
    active, declarations, counts, lists, expected = {}, {}, {}, {}, {}
    positions = {binding["unitId"]: index for index, binding in enumerate(bindings)}
    for index, binding in enumerate(bindings):
        family, unit = binding["familyId"], binding["unitId"]
        version = unit.split("/")[0]
        parameters = json.loads(binding["parameters"])
        if family == "heading":
            active.pop(version, None)
        if family in MEMBER_INTRODUCTIONS:
            kind = MEMBER_INTRODUCTIONS[family]
            subject = parameters.get("attributeContext" if kind == "locations" else "elementContext")
            if not subject:
                return False
            active[version] = unit
            declarations[unit] = kind, subject, locations[unit].rsplit("/", 1)[0]
            counts[unit] = 0
        if family not in MEMBER_ITEMS:
            continue
        declaration = active.get(version)
        if declaration is None:
            return False
        kind, subject, parent = declarations[declaration]
        match = re.fullmatch(r"(?P<list>.+/list\[[0-9]+\])/list-item\[[0-9]+\]/p\[[0-9]+\]", locations[unit])
        if (match is None or match["list"].rsplit("/", 1)[0] != parent
                or (declaration in lists and lists[declaration] != match["list"])):
            return False
        if declaration not in lists and index != positions[declaration] + 1:
            return False
        if (kind == "attributes" and parameters.get("elementMember")) or (kind != "attributes" and parameters.get("attributeKeyword")):
            return False
        if family == "member-concept-reference" and (kind == "locations" or not member_label_allowed(parameters["label"])):
            return False
        lists[declaration] = match["list"]
        counts[declaration] += 1
        expected[unit] = {"membershipKind": kind, "membershipSubject": subject,
                          "membershipIntroUnitId": declaration, "membershipListContext": match["list"]}
        if bind:
            parameters.update(expected[unit])
            binding["parameters"] = json.dumps(parameters, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
        elif any(parameters.get(key) != value for key, value in expected[unit].items()):
            return False
    for declaration, context in lists.items():
        actual = {binding["unitId"] for binding in bindings if locations[binding["unitId"]].startswith(context + "/list-item[")}
        members = {unit for unit, properties in expected.items() if properties["membershipIntroUnitId"] == declaration}
        if actual != members:
            return False
    return all(count > 0 for count in counts.values())


def complete_enumerations(bindings):
    active, counts, previous = {}, {}, {}
    for binding in sorted(bindings, key=lambda item: item["unitId"]):
        version = binding["unitId"].split("/")[0]
        family = binding["familyId"]
        parameters = json.loads(binding["parameters"])
        if family == "heading":
            active.pop(version, None)
        elif family in ENUM_INTRODUCTIONS:
            key = binding["unitId"]
            active[version] = (key, parameters["subject"])
            counts[key] = 0
        elif family in ENUM_VALUES:
            key, subject = active.get(version, (None, None))
            if subject != parameters["attributeContext"]:
                return False
            counts[key] += 1
        elif family == "enumeration-default":
            prior = previous.get(version)
            if prior is None or prior["familyId"] not in ENUM_VALUES or parameters.get("valueUnitId") != prior["unitId"]:
                return False
        previous[version] = binding
    return all(count > 0 for count in counts.values())


def complete_section_scopes(bindings):
    prerequisites = {"cross-application-properties": "section-property-applicability",
                     "chart-property-container": "section-property-purpose"}
    seen = {}
    for binding in sorted(bindings, key=lambda item: item["unitId"]):
        version = binding["unitId"].split("/")[0]
        families = seen.setdefault(version, {})
        if binding["familyId"] == "heading":
            families.clear()
        required = prerequisites.get(binding["familyId"])
        if required and required not in families:
            return False
        parameters = json.loads(binding["parameters"])
        if binding["familyId"] == "cross-application-properties" and parameters["objectKind"] != families[required]["objectKind"]:
            return False
        families[binding["familyId"]] = parameters
    return True


def complete_case_labels(bindings):
    values = {}
    for binding in bindings:
        parameters = json.loads(binding["parameters"])
        if binding["familyId"] == "inline-values":
            key = binding["unitId"].split("/")[0], parameters["subject"]
            values.setdefault(key, set()).update(re.split(r", |,? or ", parameters["values"]))
    for binding in bindings:
        if binding["familyId"] != "case-spelling-enumeration-description":
            continue
        parameters = json.loads(binding["parameters"])
        key = binding["unitId"].split("/")[0], parameters["attributeContext"]
        if parameters.get("canonicalValue") != "from-top" or parameters["sourceLabel"] != "From-top" or "from-top" not in values.get(key, set()):
            return False
    return True


def load_inventory(root=ROOT):
    groups = {g["id"]: g for g in json.loads((root / "docs/odf-obligation-classification/groups.json").read_text(encoding="utf-8"))["groups"]}
    rows, classified = {}, {}
    for version in ("1.0", "1.1", "1.2", "1.3", "1.4"):
        for directory, target in (("odf-obligation-inventory", rows), ("odf-obligation-classification", classified)):
            with (root / f"docs/{directory}/odf-{version}.csv").open(encoding="utf-8", newline="") as stream:
                for row in csv.DictReader(stream):
                    if row["id"] in target:
                        raise ValueError("Duplicate source unit")
                    target[row["id"]] = row
    return groups, rows, classified


def eligible_group(group, unit_ids, changed_blocks, changed_headings):
    headings = group["context"]["headings"]
    if any(re.search(r"\b(?:examples?|informative|non[ -]?normative)\b", heading, re.I) for heading in headings):
        return False
    if any(unit.rsplit("/S", 1)[0] in changed_blocks for unit in unit_ids):
        return False
    return not (group["context"]["part"] == "spec" and set(headings) & changed_headings)


def stitch_fragment(prepared, index):
    first = prepared[index]
    if not first[3].startswith("<") or ">" in first[3] or not first[2].get("codeStyleProfile"):
        return None
    pieces, units, previous = [], [], None
    for row, classification, block, text in prepared[index:]:
        if classification["material"] != "paragraph" or row["version"] != first[0]["version"] or (
                block.get("codeStyleRoot") != first[2].get("codeStyleRoot")) or (
                block.get("codeStyleProfile") != first[2].get("codeStyleProfile")):
            return None
        block_number = int(row["id"].split("/B")[1].split("/")[0])
        sentence_number = int(row["id"].rsplit("/S", 1)[1])
        if previous is not None:
            old_block, old_sentence = previous
            if not ((block_number == old_block and sentence_number == old_sentence + 1) or (
                    block_number == old_block + 1 and sentence_number == 1)):
                return None
        previous = block_number, sentence_number
        pieces.append(text)
        units.append(row)
        if ">" in text:
            parsed = parse_rng(" ".join(pieces))
            if parsed is None:
                return None
            family, parameters = parsed
            parameters["sourceFragmentUnits"] = [unit["id"] for unit in units]
            parameters["sourceFragmentSha256"] = digest("\n".join(unit["id"] + ":" + unit["sentenceSha256"] for unit in units).encode("utf-8"))
            return family, parameters, len(units)
    return None


def attribute_context(group, parsed):
    headings = group["context"]["headings"]
    candidates = {title for title in headings if re.fullmatch(QNAME, title)} if headings and headings[0] in (
        "General Attributes", "Formatting Attributes") else set()
    grammar_candidates = set()
    for family, parameters in parsed:
        if family in ("joint-attribute-definition", "legacy-joint-property-purpose"):
            candidates.update(re.findall(QNAME, parameters["subjects"]))
        subject = parameters.get("subject", "")
        match = re.fullmatch(rf"(?:the )?(?P<name>{QNAME})(?: {REF})? (?:attribute(?: value)?|property)", subject)
        if match:
            candidates.add(match["name"])
        if family in {"legacy-attribute-meaning", "legacy-property-purpose", "datatype", "attribute-location",
                      "enumeration-introduction", "conditional-enumeration-introduction", "inline-values", "value-union", "explicit-value-rule",
                      "value-type-definition", "typed-value-sequence", "value-sequence-arity", "token-list-introduction"} and re.fullmatch(QNAME, subject):
            candidates.add(subject)
        if family == "rng-fragment":
            for node in parameters.get("nodes", []):
                name = node["attributes"].get("name", "")
                if node["kind"] == "attribute" and re.fullmatch(QNAME, name):
                    grammar_candidates.add(name)
    if candidates:
        if len(candidates) != 1:
            return None
        candidate = next(iter(candidates))
        return candidate if not grammar_candidates or candidate in grammar_candidates else None
    return next(iter(grammar_candidates)) if len(grammar_candidates) == 1 else None


def element_context(group, parsed):
    headings = group["context"]["headings"]
    heading = re.fullmatch(rf"<(?P<name>{QNAME})>", headings[-1]) if headings else None
    candidates = {heading["name"]} if heading else set()
    bare_element_families = {"element-location", "attribute-members", "child-members", "empty-attributes",
                             "empty-children", "character-data", "element-content-type", "element-structure-reference",
                             "attribute-set-reference"}
    grammar_candidates = set()
    for family, parameters in parsed:
        subject = parameters.get("subject", "")
        match = re.fullmatch(rf"(?:the )?<(?P<name>{QNAME})>(?: {REF})? element", subject)
        if match:
            candidates.add(match["name"])
        if family in bare_element_families and re.fullmatch(QNAME, subject):
            candidates.add(subject)
        if family == "rng-fragment":
            for node in parameters.get("nodes", []):
                name = node["attributes"].get("name", "")
                if node["kind"] == "element" and re.fullmatch(QNAME, name):
                    grammar_candidates.add(name)
    if candidates:
        if len(candidates) != 1:
            return None
        candidate = next(iter(candidates))
        return candidate if not grammar_candidates or candidate in grammar_candidates else None
    return next(iter(grammar_candidates)) if len(grammar_candidates) == 1 else None


def reference_context_hash(parameters, unit, references):
    blocks = {member.rsplit("/S", 1)[0] for member in parameters.get("sourceFragmentUnits", [unit])}
    selected = {block: references[block] for block in sorted(blocks) if block in references}
    return digest(json.dumps(selected, sort_keys=True, separators=(",", ":")).encode()) if selected else None


def reference_contexts(root=ROOT):
    spec = importlib.util.spec_from_file_location("empty_references", root / "eng/Build-OdfEmptyReferences.py")
    references = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(references)
    grouped = {}
    for record in references.check_contracts(root):
        if record["sourceBlockId"]:
            grouped.setdefault(record["sourceBlockId"], []).append(record)
    return {block: digest(json.dumps(records, ensure_ascii=False, sort_keys=True,
                                    separators=(",", ":")).encode()) for block, records in grouped.items()}


def source_archives(root=ROOT):
    sources = json.loads((root / "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"]
    return {f"ODF-{source['version']}/{source['part']}": source["sha256"] for source in sources
            if SOURCE_PARTS.get(source["version"]) == source["part"]}


def compile_group(group, members, contexts, body_bounds, references=None):
    bindings, prepared = [], []
    for row, classification in members:
        version = row["version"]
        if row["part"] != SOURCE_PARTS.get(version) or version not in body_bounds or "/B" not in row["id"]:
            return None
        block_id = row["id"].rsplit("/S", 1)[0]
        block = contexts.get(block_id)
        if block is None:
            return None
        if classification["material"] == "heading" and block["kind"] != "heading":
            raise ValueError("Paragraph was incorrectly promoted to a source heading")
        first, last = body_bounds[version]
        number = int(block_id.rsplit("/B", 1)[1])
        if not first <= number < last:
            return None
        text = block["text"][int(row["start"]):int(row["end"])].strip()
        if digest(text.encode("utf-8")) != row["sentenceSha256"]:
            raise ValueError("Source sentence changed before semantic compilation")
        prepared.append((row, classification, block, text))
    fragments, resolved = {}, []
    for index, (row, classification, block, text) in enumerate(prepared):
        if index in fragments:
            parsed = fragments[index]
        else:
            parsed = parse_sentence(classification["material"], text)
        if parsed is None:
            stitched = stitch_fragment(prepared, index)
            if stitched is not None:
                family, parameters, length = stitched
                for offset in range(length):
                    fragments[index + offset] = family, parameters
                parsed = family, parameters
        if parsed is None:
            return None
        family, parameters = parsed
        parameters = dict(parameters)
        if family in ("rng-fragment", "rng-annotation"):
            if not block.get("codeStyleProfile"):
                return None
            parameters["sourceCodeStyle"] = block["codeStyleRoot"]
            parameters["sourceCodeProfile"] = block["codeStyleProfile"]
        resolved.append((family, parameters))
    context = attribute_context(group, resolved)
    element = element_context(group, resolved)
    for (row, classification, block, text), (family, parameters) in zip(prepared, resolved):
        if family == "legacy-element-purpose" and parameters["displayName"].casefold() != group["context"]["headings"][-1].casefold():
            return None
        if parameters.pop("requiresAttributeContext", False):
            if context is None:
                return None
            parameters["attributeContext"] = context
        if family == "password-utf8" and context not in PASSWORD_ATTRIBUTES:
            return None
        if family in IMPLICIT_ELEMENT_FAMILIES:
            if element is None or (family == "region-paragraph-sequence" and element not in (
                    "style:region-left", "style:region-center", "style:region-right")):
                return None
            parameters["elementContext"] = element
        if family in LIST_VALUES:
            match = re.fullmatch(r"(?P<list>.+/list\[[0-9]+\])/list-item\[[0-9]+\]/p\[[0-9]+\]", block.get("locator", ""))
            if match is None:
                return None
            parameters["listContext"] = match["list"]
        if family == "implicit-enumeration-introduction":
            parameters["subject"] = context
        if family == "enumeration-default":
            if not bindings or bindings[-1]["unitId"].split("/")[0] != row["id"].split("/")[0] or bindings[-1]["familyId"] not in ENUM_VALUES:
                return None
            parameters["valueUnitId"] = bindings[-1]["unitId"]
        reference_hash = reference_context_hash(parameters, row["id"], references or {})
        if reference_hash:
            parameters["sourceReferenceBlockEvidenceSha256"] = reference_hash
        bindings.append({"groupId": group["id"], "unitId": row["id"],
            "sourceUnitSha256": row["sentenceSha256"], "familyId": family,
            "parameters": json.dumps(parameters, ensure_ascii=False, sort_keys=True, separators=(",", ":"))})
    if any(binding["familyId"] == "rng-annotation" for binding in bindings) and not any(binding["familyId"] == "rng-fragment" for binding in bindings):
        return None
    locations = {row["id"]: block.get("locator", "") for row, _, block, _ in prepared}
    return bindings if (complete_enumerations(bindings) and complete_section_scopes(bindings) and complete_case_labels(bindings)
                        and bind_member_lists(bindings, locations, bind=True)) else None


def build(source_dir):
    groups, rows, classified = load_inventory()
    references = reference_contexts()
    manual = json.loads((ROOT / "docs/odf-semantic-reviews.json").read_text(encoding="utf-8"))
    manual_ids = {review["groupId"] for review in manual["reviews"]}
    policy = json.loads((ROOT / "docs/odf-semantic-body-ranges.json").read_text(encoding="utf-8"))
    validate_ranges(policy)
    source_locks = json.loads((ROOT / "docs/odf-obligation-inventory/sources.json").read_text(encoding="utf-8"))["sources"]
    spec = importlib.util.spec_from_file_location("inventory", ROOT / "eng/Build-OdfObligationInventory.py")
    inventory = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(inventory)
    contexts, bounds = {}, {}
    effective_spec = importlib.util.spec_from_file_location("effective", ROOT / "eng/Build-OdfEffectiveText.py")
    effective = importlib.util.module_from_spec(effective_spec)
    effective_spec.loader.exec_module(effective)
    projection = effective.check_contracts(ROOT)
    for region in policy["ranges"]:
        source = next(s for s in source_locks if s["version"] == region["version"] and s["part"] == region["part"])
        path = source_dir / source["file"]
        if digest(path.read_bytes()) != source["sha256"]:
            raise ValueError("Locked normative source changed")
        blocks, _, _ = inventory.odt_blocks(path)
        code_styles = region.get("codeStyleRoots", {})
        style_parents, style_cache = {}, {}
        if code_styles:
            with zipfile.ZipFile(path) as package:
                for entry in ("styles.xml", "content.xml"):
                    root = ET.fromstring(package.read(entry))
                    for node in root.iter(effective.STYLE + "style"):
                        if node.get(effective.STYLE + "family") == "paragraph":
                            snapshot = effective.style_snapshot(node)
                            style_parents[snapshot["name"]] = snapshot["parent"]
                            if snapshot["name"] in code_styles and effective.style_hash(snapshot) != code_styles[snapshot["name"]]["styleSha256"]:
                                raise ValueError("Reviewed normative grammar style changed")
            if set(code_styles) - style_parents.keys():
                raise ValueError("Normative grammar style is missing")

        def code_style(name, active=None):
            if name in style_cache:
                return style_cache[name]
            active = set() if active is None else active
            if name in active:
                raise ValueError("Cyclic grammar paragraph style")
            active.add(name)
            if name in code_styles:
                answer = name
            else:
                parent = style_parents.get(name)
                answer = code_style(parent, active) if parent else None
            active.remove(name)
            style_cache[name] = answer
            return answer

        for index, block in enumerate(blocks, 1):
            root_style = code_style(block.get("style", ""))
            if root_style:
                block["codeStyleRoot"] = root_style
                block["codeStyleProfile"] = code_styles[root_style]["profile"]
            contexts[f"ODF-{source['version']}/{source['part']}/B{index:05d}"] = block
        for marker in (region["first"], region["afterLast"]):
            if digest(contexts[marker["id"]]["text"].encode("utf-8")) != marker["textSha256"]:
                raise ValueError("Normative range boundary changed")
        for marker in region.get("grammarStyleBasis", []):
            if digest(contexts[marker["id"]]["text"].encode("utf-8")) != marker["textSha256"]:
                raise ValueError("Normative grammar designation changed")
        bounds[region["version"]] = tuple(int(region[key]["id"].rsplit("/B", 1)[1]) for key in ("first", "afterLast"))
    changed_blocks = {block["blockId"] for block in projection["blocks"]}
    for region in policy.get("informativeContexts", []):
        for marker in (region["first"], region["afterLast"]):
            if digest(contexts[marker["id"]]["text"].encode("utf-8")) != marker["textSha256"]:
                raise ValueError("Informative source context changed")
    changed_headings = {contexts[identifier]["text"] for identifier in changed_blocks
                        if identifier in contexts and contexts[identifier]["kind"] == "heading"}
    members = {}
    for identifier, row in rows.items():
        classification = classified[identifier]
        members.setdefault(classification["groupId"], []).append((row, classification))
    reviews, bindings = [], []
    for identifier, group in sorted(groups.items()):
        if identifier in manual_ids:
            continue
        if not eligible_group(group, [row["id"] for row, _ in members[identifier]], changed_blocks, changed_headings):
            continue
        compiled = compile_group(group, members[identifier], contexts, bounds, references)
        if compiled is None:
            continue
        bindings += compiled
        reviews.append({"groupId": identifier, "memberSha256": group["memberSha256"],
            "versions": group["versions"], "implementationAdjudication": "pending",
            "crossVersionEquivalenceClaim": False,
            "versionAssessment": "保留各版句型、技術參數、引用與來源 hash；同句型只共用語意分類規則，不宣告各版有效要求完全等價。"})
    return reviews, bindings


def render(reviews, bindings):
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fieldnames=["groupId", "unitId", "sourceUnitSha256", "familyId", "parameters"], lineterminator="\n")
    writer.writeheader()
    writer.writerows(sorted(bindings, key=lambda row: row["unitId"]))
    metadata = {"schemaVersion": 1, "fullConformanceClaim": False,
        "basis": "complete-parameterized-semantic-constructions-in-locked-body-ranges",
        "builderSha256": code_hash(), "familySha256": family_hash(),
        "sourceArchiveSha256": source_archives(),
        "sourceReferenceEvidenceSha256": digest((ROOT / "docs/odf-empty-reference-evidence.json").read_bytes().replace(b"\r\n", b"\n")),
        "bodyRangesSha256": digest((ROOT / "docs/odf-semantic-body-ranges.json").read_bytes().replace(b"\r\n", b"\n")),
        "unitCount": len(bindings), "bindingsSha256": digest(stream.getvalue().encode("utf-8")),
        "groups": reviews, "families": FAMILIES, "scopePolicy": SCOPE_POLICY,
        "effectiveTextEvidenceSha256": digest((ROOT / "docs/odf-effective-text/odf-1.1-spec.json").read_bytes().replace(b"\r\n", b"\n")),
        "scopeEvidenceSha256": {reference: digest((ROOT / reference).read_bytes().replace(b"\r\n", b"\n")) for reference in SCOPE_POLICY["evidence"]}}
    return {TARGET / "groups.json": json.dumps(metadata, ensure_ascii=False, indent=2) + "\n",
            TARGET / "bindings.csv": stream.getvalue()}


def validate_contracts(metadata, bindings, groups, rows, classified, manual_ids, policy, changed_blocks=None, changed_headings=None, references=None):
    validate_ranges(policy)
    for region in policy.get("informativeContexts", []):
        for marker in (region["first"], region["afterLast"]):
            if rows.get(marker["id"] + "/S001", {}).get("blockSha256") != marker["textSha256"]:
                raise ValueError("Informative boundary differs from locked inventory")
    if metadata.get("schemaVersion") != 1 or metadata.get("fullConformanceClaim") is not False:
        raise ValueError("Semantic batches must not claim full conformance")
    if metadata["builderSha256"] != code_hash() or metadata["familySha256"] != family_hash() or metadata["families"] != FAMILIES:
        raise ValueError("Semantic compiler or reviewed families changed")
    bounds = {}
    grammar_styles = {}
    for region in policy["ranges"]:
        for marker in (region["first"], region["afterLast"]):
            source = rows.get(marker["id"] + "/S001")
            if source is None or source["blockSha256"] != marker["textSha256"]:
                raise ValueError("Normative source boundary differs from locked inventory")
        bounds[region["version"]] = tuple(int(region[key]["id"].rsplit("/B", 1)[1]) for key in ("first", "afterLast"))
        grammar_styles[region["version"]] = region.get("codeStyleRoots", {})
        for marker in region.get("grammarStyleBasis", []):
            if rows.get(marker["id"] + "/S001", {}).get("blockSha256") != marker["textSha256"]:
                raise ValueError("Normative grammar source designation changed")
    reviews = {}
    expected = {}
    for identifier, classification in classified.items():
        expected.setdefault(classification["groupId"], set()).add(identifier)
    for review in metadata["groups"]:
        identifier = review["groupId"]
        if identifier in reviews or identifier not in groups or identifier in manual_ids:
            raise ValueError("Duplicate, unknown or manually reviewed batch group")
        group = groups[identifier]
        if not eligible_group(group, expected[identifier], changed_blocks or set(), changed_headings or set()):
            raise ValueError("Unreconciled editorial text or example context cannot be automatically classified")
        if review["memberSha256"] != group["memberSha256"] or review["versions"] != group["versions"]:
            raise ValueError("Semantic batch source membership or versions changed")
        if review["implementationAdjudication"] != "pending" or review["crossVersionEquivalenceClaim"] is not False or not review["versionAssessment"].strip():
            raise ValueError("Semantic batches cannot close implementation or assert version equivalence")
        reviews[identifier] = review
    seen, by_group, binding_groups = set(), {}, {}
    for binding in bindings:
        identifier, unit = binding["groupId"], binding["unitId"]
        if identifier not in reviews or unit not in rows or unit in seen or classified[unit]["groupId"] != identifier:
            raise ValueError("Duplicate, unknown or foreign semantic binding")
        row = rows[unit]
        if row["sentenceSha256"] != binding["sourceUnitSha256"]:
            raise ValueError("Semantic binding source sentence changed")
        if row["part"] != SOURCE_PARTS.get(row["version"]) or row["version"] not in bounds or "/B" not in unit:
            raise ValueError("Unsupported semantic source")
        first, last = bounds[row["version"]]
        if not first <= int(unit.split("/B")[1].split("/")[0]) < last:
            raise ValueError("Semantic binding outside reviewed body range")
        family = binding["familyId"]
        if family not in FAMILIES:
            raise ValueError("Unknown semantic family")
        if (family == "heading") != (classified[unit]["material"] == "heading"):
            raise ValueError("A paragraph cannot be waived as a heading")
        parameters = json.loads(binding["parameters"])
        fields = set(PATTERNS[family].groupindex) if family in PATTERNS else set()
        if family in ("rng-fragment", "rng-annotation"):
            root_style = parameters.get("sourceCodeStyle")
            definition = grammar_styles[row["version"]].get(root_style)
            if definition is None or parameters.get("sourceCodeProfile") != definition["profile"]:
                raise ValueError("Example code cannot be promoted to normative grammar")
        if family == "rng-fragment":
            fields = {"action", "nodes", "sourceCodeStyle", "sourceCodeProfile"}
            nodes = parameters.get("nodes", [])
            if parameters.get("action") not in ("open", "close", "complete") or not nodes:
                raise ValueError("Invalid RELAX NG fragment")
            for node in nodes:
                if set(node) != {"kind", "attributes", "textSha256"} or node["kind"] not in RNG_NODES or (
                        not isinstance(node["attributes"], dict)) or not re.fullmatch(r"[0-9a-f]{64}", node["textSha256"]):
                    raise ValueError("Unknown RELAX NG production or missing literal hash")
        if family == "rng-annotation":
            fields = {"commentSha256", "scopeSignals", "sourceCodeStyle", "sourceCodeProfile"}
            if not re.fullmatch(r"[0-9a-f]{64}", parameters.get("commentSha256", "")) or not set(parameters.get("scopeSignals", [])) <= {"shall", "must", "should", "may", "if", "unless"}:
                raise ValueError("Invalid RELAX NG annotation")
        if family in ("rng-fragment", "rng-annotation") and ("sourceFragmentUnits" in parameters or "sourceFragmentSha256" in parameters):
            fields.update(("sourceFragmentUnits", "sourceFragmentSha256"))
            fragment_units = parameters.get("sourceFragmentUnits", [])
            if len(fragment_units) < 2 or unit not in fragment_units or len(set(fragment_units)) != len(fragment_units) or any(
                    member not in expected[identifier] for member in fragment_units):
                raise ValueError("Invalid multi-unit grammar fragment ownership")
            fingerprint = digest("\n".join(member + ":" + rows[member]["sentenceSha256"] for member in fragment_units).encode("utf-8"))
            if parameters.get("sourceFragmentSha256") != fingerprint:
                raise ValueError("Multi-unit grammar fragment source changed")
        if family in PREDICATE_FAMILIES:
            fields.remove("predicate")
            fields.update(("predicateSha256", "referencedQNames", "referencedSections"))
            if not re.fullmatch(r"[0-9a-f]{64}", parameters.get("predicateSha256", "")):
                raise ValueError("Missing semantic predicate hash")
        if family in SIGNAL_FAMILIES:
            fields.add("scopeSignals")
            allowed = {"if", "unless", "shall", "must", "should", "may", "can", "will", "when", "only", "otherwise", "except", "provided", "depending", "not"}
            signals = parameters.get("scopeSignals", [])
            if (family == "compound-data-meaning" and not signals) or not set(signals) <= allowed or len(set(signals)) != len(signals):
                raise ValueError("Missing or invented compound semantic signals")
        if family == "enumeration-default":
            fields.add("valueUnitId")
        if family == "case-spelling-enumeration-description":
            fields.add("canonicalValue")
        if family in LIST_VALUES:
            fields.add("listContext")
            location = re.fullmatch(r"(?P<list>.+/list\[[0-9]+\])/list-item\[[0-9]+\]/p\[[0-9]+\]", row["locator"])
            if location is None or parameters.get("listContext") != location["list"]:
                raise ValueError("Enumeration item is not bound to its source list")
        if family == "implicit-enumeration-introduction":
            fields.add("subject")
            if parameters.get("subject") != parameters.get("attributeContext"):
                raise ValueError("Implicit enumeration subject changed")
        if family == "character-default" and (len(parameters.get("character", "")) != 1 or
                not re.fullmatch(r"[0-9A-F]{4,6}", parameters.get("codePoint", "")) or
                ord(parameters["character"]) != int(parameters["codePoint"], 16)):
            raise ValueError("Default character differs from its Unicode code point")
        if needs_attribute_context(family, parameters):
            fields.add("attributeContext")
            if family == "password-utf8" and parameters.get("attributeContext") not in PASSWORD_ATTRIBUTES:
                raise ValueError("Password encoding lacks a reviewed protection attribute context")
        if family in IMPLICIT_ELEMENT_FAMILIES:
            fields.add("elementContext")
        if family == "legacy-element-purpose" and parameters["displayName"].casefold() != groups[identifier]["context"]["headings"][-1].casefold():
            raise ValueError("Legacy element display name has a different source heading")
        if family in MEMBER_ITEMS:
            fields |= {"membershipKind", "membershipSubject", "membershipIntroUnitId", "membershipListContext"}
        reference_hash = reference_context_hash(parameters, unit, references or {})
        if reference_hash:
            fields.add("sourceReferenceBlockEvidenceSha256")
            if parameters.get("sourceReferenceBlockEvidenceSha256") != reference_hash:
                raise ValueError("Hidden reference field context changed")
        if set(parameters) != fields:
            raise ValueError("Incomplete or invented semantic parameters")
        seen.add(unit)
        by_group.setdefault(identifier, set()).add(unit)
        binding_groups.setdefault(identifier, []).append(binding)
    if metadata["unitCount"] != len(bindings):
        raise ValueError("Semantic unit count changed")
    if any(by_group.get(identifier, set()) != expected[identifier] for identifier in reviews):
        raise ValueError("A partially understood group cannot be marked classified")
    for group_bindings in binding_groups.values():
        if not bind_member_lists(group_bindings, {binding["unitId"]: rows[binding["unitId"]].get("locator", "") for binding in group_bindings}):
            raise ValueError("Incomplete or unbound source membership list")
        if not complete_case_labels(group_bindings):
            raise ValueError("Source label capitalization has no same-version literal declaration")
        if not complete_section_scopes(group_bindings):
            raise ValueError("Section properties have no same-version scope antecedent")
        group = groups[group_bindings[0]["groupId"]]
        parsed = [(binding["familyId"], json.loads(binding["parameters"])) for binding in group_bindings]
        context, element = attribute_context(group, parsed), element_context(group, parsed)
        for binding in group_bindings:
            parameters = json.loads(binding["parameters"])
            if "attributeContext" in parameters and (context is None or parameters["attributeContext"] != context):
                raise ValueError("Ambiguous semantic attribute context")
            if "elementContext" in parameters and (element is None or parameters["elementContext"] != element):
                raise ValueError("Ambiguous semantic element context")
            if binding["familyId"] == "region-paragraph-sequence" and element not in (
                    "style:region-left", "style:region-center", "style:region-right"):
                raise ValueError("Region content permission has no region element context")
        if not complete_enumerations(group_bindings):
            raise ValueError("Orphaned enumeration introduction or value branch")
        if any(binding["familyId"] == "rng-annotation" for binding in group_bindings) and not any(binding["familyId"] == "rng-fragment" for binding in group_bindings):
            raise ValueError("Orphaned schema annotation requires source context review")
        binding_lookup = {binding["unitId"]: binding for binding in group_bindings}
        for binding in group_bindings:
            parameters = json.loads(binding["parameters"])
            for member in parameters.get("sourceFragmentUnits", []):
                peer = binding_lookup[member]
                if peer["familyId"] != binding["familyId"] or peer["parameters"] != binding["parameters"]:
                    raise ValueError("Multi-unit fragment peers disagree")
    return set(reviews)


def check_contracts(root=ROOT):
    target = root / "docs/odf-semantic-batches"
    metadata = json.loads((target / "groups.json").read_text(encoding="utf-8"))
    references = reference_contexts(root)
    if (metadata.get("sourceArchiveSha256") != source_archives(root) or metadata.get("sourceReferenceEvidenceSha256") !=
            digest((root / "docs/odf-empty-reference-evidence.json").read_bytes().replace(b"\r\n", b"\n"))):
        raise ValueError("Semantic publication archives or reference targets changed")
    effective_spec = importlib.util.spec_from_file_location("effective", root / "eng/Build-OdfEffectiveText.py")
    effective = importlib.util.module_from_spec(effective_spec)
    effective_spec.loader.exec_module(effective)
    projection = effective.check_contracts(root)
    projection_hash = digest((root / "docs/odf-effective-text/odf-1.1-spec.json").read_bytes().replace(b"\r\n", b"\n"))
    if metadata["effectiveTextEvidenceSha256"] != projection_hash:
        raise ValueError("Effective source text projection changed")
    expected_evidence = {reference: digest((root / reference).read_bytes().replace(b"\r\n", b"\n")) for reference in SCOPE_POLICY["evidence"]}
    if metadata["scopePolicy"] != SCOPE_POLICY or metadata["scopeEvidenceSha256"] != expected_evidence:
        raise ValueError("SDK scope policy or evidence changed")
    csv_text = (target / "bindings.csv").read_text(encoding="utf-8")
    if digest(csv_text.encode("utf-8")) != metadata["bindingsSha256"]:
        raise ValueError("Semantic bindings changed without rebuilding metadata")
    policy_bytes = (root / "docs/odf-semantic-body-ranges.json").read_bytes().replace(b"\r\n", b"\n")
    if digest(policy_bytes) != metadata["bodyRangesSha256"]:
        raise ValueError("Semantic source ranges changed")
    groups, rows, classified = load_inventory(root)
    changed_blocks = {block["blockId"] for block in projection["blocks"]}
    changed_headings = {row["heading"] for identifier, row in rows.items()
                        if identifier.rsplit("/S", 1)[0] in changed_blocks and classified[identifier]["material"] == "heading"}
    manual_ids = {review["groupId"] for review in json.loads((root / "docs/odf-semantic-reviews.json").read_text(encoding="utf-8"))["reviews"]}
    return validate_contracts(metadata, list(csv.DictReader(io.StringIO(csv_text))), groups, rows, classified,
                              manual_ids, json.loads(policy_bytes), changed_blocks, changed_headings, references)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=ROOT / "artifacts/odf-conformance-sources")
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--check-contracts", action="store_true")
    options = parser.parse_args()
    if options.check_contracts:
        identifiers = check_contracts()
        print(json.dumps({"familyReviewedGroups": len(identifiers), "fullConformanceClaim": False}))
        return
    reviews, bindings = build(options.source_dir)
    for path, value in render(reviews, bindings).items():
        if options.verify:
            if not path.exists() or path.read_text(encoding="utf-8") != value:
                raise ValueError(f"Semantic batch differs: {path}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(value, encoding="utf-8", newline="")
    print(json.dumps({"familyReviewedGroups": len(reviews), "sourceUnits": len(bindings), "fullConformanceClaim": False}))


if __name__ == "__main__":
    main()
