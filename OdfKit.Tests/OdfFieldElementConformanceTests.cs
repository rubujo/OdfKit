using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests text field and related native elements against their declared attributes and content models.
/// 依宣告的屬性與內容模型測試文字欄位及相關原生元素。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfFieldElementConformanceTests
{
    // 欄位：元素;版本;包裝;內容;必要屬性;可選屬性。屬性格式 名稱=有效值[!無效值]。
    private static readonly string[] Cases =
    [
        "number:currency-symbol;1.2;currency;text;;number:country=tok,number:language=tok,number:rfc-language-tag=en,number:script=tok", // ODFG-4b2d94fceb8addd96590
        "number:currency-symbol;1.3;currency;text;;number:country=tok,number:language=tok,number:rfc-language-tag=en,number:script=tok", // ODFG-4b2d94fceb8addd96590
        "number:currency-symbol;1.4;currency;text;;number:country=tok,number:language=tok,number:rfc-language-tag=en,number:script=tok", // ODFG-4b2d94fceb8addd96590
        "number:embedded-text;1.2;embedded;text;number:position=1!not-a-value;", // ODFG-5ceb4326c29a4834085b
        "number:embedded-text;1.3;embedded;text;number:position=1!not-a-value;", // ODFG-5ceb4326c29a4834085b
        "number:embedded-text;1.4;embedded;text;number:position=1!not-a-value;", // ODFG-5ceb4326c29a4834085b
        "number:fill-character;1.3;numberstyle;text;;", // ODFG-abdf2132bb3942475345
        "number:fill-character;1.4;numberstyle;text;;", // ODFG-abdf2132bb3942475345
        "presentation:date-time-decl;1.2;presentation;text;presentation:name=text,presentation:source=fixed!__invalid__;style:data-style-name=n1", // ODFG-e4a33344101744d606e4
        "presentation:date-time-decl;1.3;presentation;text;presentation:name=text,presentation:source=fixed!__invalid__;style:data-style-name=n1", // ODFG-e4a33344101744d606e4
        "presentation:date-time-decl;1.4;presentation;text;presentation:name=text,presentation:source=fixed!__invalid__;style:data-style-name=n1", // ODFG-e4a33344101744d606e4
        "text:bibliography-mark;1.2;p;text;text:bibliography-type=article!__invalid__;", // ODFG-8da76246b91da84ed8b9
        "text:bibliography-mark;1.3;p;text;text:bibliography-type=article!__invalid__;", // ODFG-8da76246b91da84ed8b9
        "text:bibliography-mark;1.4;p;text;text:bibliography-type=article!__invalid__;", // ODFG-8da76246b91da84ed8b9
        "text:bookmark-ref;1.2;p;text;;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-6c156a888fde9bbaa290
        "text:bookmark-ref;1.3;p;text;;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-6c156a888fde9bbaa290
        "text:bookmark-ref;1.4;p;text;;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-6c156a888fde9bbaa290
        "text:chapter;1.2;p;text;text:display=name!__invalid__,text:outline-level=1!not-a-value;", // ODFG-272c56e640e53d6784ee
        "text:chapter;1.3;p;text;text:display=name!__invalid__,text:outline-level=1!not-a-value;", // ODFG-272c56e640e53d6784ee
        "text:chapter;1.4;p;text;text:display=name!__invalid__,text:outline-level=1!not-a-value;", // ODFG-272c56e640e53d6784ee
        "text:character-count;1.2;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-138bb1fc57b2d0ed8ceb
        "text:character-count;1.3;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-138bb1fc57b2d0ed8ceb
        "text:character-count;1.4;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-138bb1fc57b2d0ed8ceb
        "text:conditional-text;1.2;p;text;text:condition=text,text:string-value-if-false=text,text:string-value-if-true=text;text:current-value=true!__invalid__", // ODFG-2e719adcee4a025d69e9
        "text:conditional-text;1.3;p;text;text:condition=text,text:string-value-if-false=text,text:string-value-if-true=text;text:current-value=true!__invalid__", // ODFG-2e719adcee4a025d69e9
        "text:conditional-text;1.4;p;text;text:condition=text,text:string-value-if-false=text,text:string-value-if-true=text;text:current-value=true!__invalid__", // ODFG-2e719adcee4a025d69e9
        "text:creation-date;1.2;p;text;;style:data-style-name=n1,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-3598109f0b725941665b
        "text:creation-date;1.3;p;text;;style:data-style-name=n1,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-3598109f0b725941665b
        "text:creation-date;1.4;p;text;;style:data-style-name=n1,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-3598109f0b725941665b
        "text:creation-time;1.2;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-value=03:04:05!not-a-value", // ODFG-e7bc342a5df09cf1c9f1
        "text:creation-time;1.3;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-value=03:04:05!not-a-value", // ODFG-e7bc342a5df09cf1c9f1
        "text:creation-time;1.4;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-value=03:04:05!not-a-value", // ODFG-e7bc342a5df09cf1c9f1
        "text:creator;1.2;p;text;;text:fixed=true!__invalid__", // ODFG-d7e4401c9849a6a7638f
        "text:creator;1.3;p;text;;text:fixed=true!__invalid__", // ODFG-d7e4401c9849a6a7638f
        "text:creator;1.4;p;text;;text:fixed=true!__invalid__", // ODFG-d7e4401c9849a6a7638f
        "text:database-display;1.2;p;model;text:column-name=text,text:table-name=text;style:data-style-name=n1,text:database-name=text,text:table-type=table!__invalid__", // ODFG-b607d4ae30e71ba4135b
        "text:database-display;1.3;p;model;text:column-name=text,text:table-name=text;style:data-style-name=n1,text:database-name=text,text:table-type=table!__invalid__", // ODFG-b607d4ae30e71ba4135b
        "text:database-display;1.4;p;model;text:column-name=text,text:table-name=text;style:data-style-name=n1,text:database-name=text,text:table-type=table!__invalid__", // ODFG-b607d4ae30e71ba4135b
        "text:database-name;1.2;p;model;text:table-name=text;text:database-name=text,text:table-type=table!__invalid__", // ODFG-2334fd4d9ced128f7f8f
        "text:database-name;1.3;p;model;text:table-name=text;text:database-name=text,text:table-type=table!__invalid__", // ODFG-2334fd4d9ced128f7f8f
        "text:database-name;1.4;p;model;text:table-name=text;text:database-name=text,text:table-type=table!__invalid__", // ODFG-2334fd4d9ced128f7f8f
        "text:database-row-number;1.2;p;model;text:table-name=text;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a,text:database-name=text,text:table-type=table!__invalid__,text:value=1!not-a-value", // ODFG-6ff8cd4593e22bca682c
        "text:database-row-number;1.3;p;model;text:table-name=text;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a,text:database-name=text,text:table-type=table!__invalid__,text:value=1!not-a-value", // ODFG-6ff8cd4593e22bca682c
        "text:database-row-number;1.4;p;model;text:table-name=text;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a,text:database-name=text,text:table-type=table!__invalid__,text:value=1!not-a-value", // ODFG-6ff8cd4593e22bca682c
        "text:date;1.2;p;text;;style:data-style-name=n1,text:date-adjust=PT1H!not-a-value,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-4e791058c1612dd460ea
        "text:date;1.3;p;text;;style:data-style-name=n1,text:date-adjust=PT1H!not-a-value,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-4e791058c1612dd460ea
        "text:date;1.4;p;text;;style:data-style-name=n1,text:date-adjust=PT1H!not-a-value,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-4e791058c1612dd460ea
        "text:dde-connection;1.2;p;text;text:connection-name=text;", // ODFG-37cb5fe2daf4599d4f75
        "text:dde-connection;1.3;p;text;text:connection-name=text;", // ODFG-37cb5fe2daf4599d4f75
        "text:dde-connection;1.4;p;text;text:connection-name=text;", // ODFG-37cb5fe2daf4599d4f75
        "text:description;1.2;p;text;;text:fixed=true!__invalid__", // ODFG-e6f9fbf6e6c60ca9defa
        "text:description;1.3;p;text;;text:fixed=true!__invalid__", // ODFG-e6f9fbf6e6c60ca9defa
        "text:description;1.4;p;text;;text:fixed=true!__invalid__", // ODFG-e6f9fbf6e6c60ca9defa
        "text:drop-down;1.3;p;model;text:name=text;", // ODFG-1eb0105f48edf6e9a766
        "text:drop-down;1.4;p;model;text:name=text;", // ODFG-1eb0105f48edf6e9a766
        "text:editing-cycles;1.2;p;text;;text:fixed=true!__invalid__", // ODFG-43da89ba8adf039970fc
        "text:editing-cycles;1.3;p;text;;text:fixed=true!__invalid__", // ODFG-43da89ba8adf039970fc
        "text:editing-cycles;1.4;p;text;;text:fixed=true!__invalid__", // ODFG-43da89ba8adf039970fc
        "text:editing-duration;1.2;p;text;;style:data-style-name=n1,text:duration=PT1H!not-a-value,text:fixed=true!__invalid__", // ODFG-361417cc890a13865db8
        "text:editing-duration;1.3;p;text;;style:data-style-name=n1,text:duration=PT1H!not-a-value,text:fixed=true!__invalid__", // ODFG-361417cc890a13865db8
        "text:editing-duration;1.4;p;text;;style:data-style-name=n1,text:duration=PT1H!not-a-value,text:fixed=true!__invalid__", // ODFG-361417cc890a13865db8
        "text:execute-macro;1.2;p;model;;text:name=text", // ODFG-77c19ca1d7d4af1e4c66
        "text:execute-macro;1.3;p;model;;text:name=text", // ODFG-77c19ca1d7d4af1e4c66
        "text:execute-macro;1.4;p;model;;text:name=text", // ODFG-77c19ca1d7d4af1e4c66
        "text:expression;1.2;p;text;;style:data-style-name=n1,text:display=value!__invalid__,text:formula=text", // ODFG-6ea3cf11bd2ca8e7e0b9
        "text:expression;1.3;p;text;;style:data-style-name=n1,text:display=value!__invalid__,text:formula=text", // ODFG-6ea3cf11bd2ca8e7e0b9
        "text:expression;1.4;p;text;;style:data-style-name=n1,text:display=value!__invalid__,text:formula=text", // ODFG-6ea3cf11bd2ca8e7e0b9
        "text:file-name;1.2;p;text;;text:display=full!__invalid__,text:fixed=true!__invalid__", // ODFG-1b18162846534ce68701
        "text:file-name;1.3;p;text;;text:display=full!__invalid__,text:fixed=true!__invalid__", // ODFG-1b18162846534ce68701
        "text:file-name;1.4;p;text;;text:display=full!__invalid__,text:fixed=true!__invalid__", // ODFG-1b18162846534ce68701
        "text:hidden-paragraph;1.2;p;text;text:condition=text;text:is-hidden=true!__invalid__", // ODFG-a8a68784813b8b127a87
        "text:hidden-paragraph;1.3;p;text;text:condition=text;text:is-hidden=true!__invalid__", // ODFG-a8a68784813b8b127a87
        "text:hidden-paragraph;1.4;p;text;text:condition=text;text:is-hidden=true!__invalid__", // ODFG-a8a68784813b8b127a87
        "text:hidden-text;1.2;p;text;text:condition=text,text:string-value=text;text:is-hidden=true!__invalid__", // ODFG-d2c3e6c89a11fe46a556
        "text:hidden-text;1.3;p;text;text:condition=text,text:string-value=text;text:is-hidden=true!__invalid__", // ODFG-d2c3e6c89a11fe46a556
        "text:hidden-text;1.4;p;text;text:condition=text,text:string-value=text;text:is-hidden=true!__invalid__", // ODFG-d2c3e6c89a11fe46a556
        "text:image-count;1.2;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-4adf5c9cba9367882521
        "text:image-count;1.3;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-4adf5c9cba9367882521
        "text:image-count;1.4;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-4adf5c9cba9367882521
        "text:index-entry-span;1.2;tocentry;text;;text:style-name=n1", // ODFG-f953dfe3a4a54727cdfa
        "text:index-entry-span;1.3;tocentry;text;;text:style-name=n1", // ODFG-f953dfe3a4a54727cdfa
        "text:index-entry-span;1.4;tocentry;text;;text:style-name=n1", // ODFG-f953dfe3a4a54727cdfa
        "text:index-title-template;1.2;toc;text;;text:style-name=n1", // ODFG-338467dd09a0545e388f
        "text:index-title-template;1.3;toc;text;;text:style-name=n1", // ODFG-338467dd09a0545e388f
        "text:index-title-template;1.4;toc;text;;text:style-name=n1", // ODFG-338467dd09a0545e388f
        "text:initial-creator;1.2;p;text;;text:fixed=true!__invalid__", // ODFG-d20631dfcf5b2eb4d1b6
        "text:initial-creator;1.3;p;text;;text:fixed=true!__invalid__", // ODFG-d20631dfcf5b2eb4d1b6
        "text:initial-creator;1.4;p;text;;text:fixed=true!__invalid__", // ODFG-d20631dfcf5b2eb4d1b6
        "text:keywords;1.2;p;text;;text:fixed=true!__invalid__", // ODFG-e6b92f6760c44277ca72
        "text:keywords;1.3;p;text;;text:fixed=true!__invalid__", // ODFG-e6b92f6760c44277ca72
        "text:keywords;1.4;p;text;;text:fixed=true!__invalid__", // ODFG-e6b92f6760c44277ca72
        "text:linenumbering-separator;1.2;linenumbering;text;;text:increment=1!not-a-value", // ODFG-25f776ed24d2cb65e1c1
        "text:linenumbering-separator;1.3;linenumbering;text;;text:increment=1!not-a-value", // ODFG-25f776ed24d2cb65e1c1
        "text:linenumbering-separator;1.4;linenumbering;text;;text:increment=1!not-a-value", // ODFG-25f776ed24d2cb65e1c1
        "text:measure;1.2;measure;text;text:kind=value!__invalid__;", // ODFG-7950a6d6e732cec8daef
        "text:measure;1.3;measure;text;text:kind=value!__invalid__;", // ODFG-7950a6d6e732cec8daef
        "text:measure;1.4;measure;text;text:kind=value!__invalid__;", // ODFG-7950a6d6e732cec8daef
        "text:modification-date;1.2;p;text;;style:data-style-name=n1,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-d29fc1c80de99a05c3af
        "text:modification-date;1.3;p;text;;style:data-style-name=n1,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-d29fc1c80de99a05c3af
        "text:modification-date;1.4;p;text;;style:data-style-name=n1,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-d29fc1c80de99a05c3af
        "text:modification-time;1.2;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-value=03:04:05!not-a-value", // ODFG-579086629d31df01aca4
        "text:modification-time;1.3;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-value=03:04:05!not-a-value", // ODFG-579086629d31df01aca4
        "text:modification-time;1.4;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-value=03:04:05!not-a-value", // ODFG-579086629d31df01aca4
        "text:note-citation;1.2;note;text;;text:label=text", // ODFG-6fc0b923fa9031b618e4
        "text:note-citation;1.3;note;text;;text:label=text", // ODFG-6fc0b923fa9031b618e4
        "text:note-citation;1.4;note;text;;text:label=text", // ODFG-6fc0b923fa9031b618e4
        "text:note-continuation-notice-backward;1.2;notes;text;;", // ODFG-4976e49d0178743b8773
        "text:note-continuation-notice-backward;1.3;notes;text;;", // ODFG-4976e49d0178743b8773
        "text:note-continuation-notice-backward;1.4;notes;text;;", // ODFG-4976e49d0178743b8773
        "text:note-continuation-notice-forward;1.2;notes;text;;", // ODFG-13be9c6e391d6db38768
        "text:note-continuation-notice-forward;1.3;notes;text;;", // ODFG-13be9c6e391d6db38768
        "text:note-continuation-notice-forward;1.4;notes;text;;", // ODFG-13be9c6e391d6db38768
        "text:note-ref;1.2;p;text;text:note-class=footnote!__invalid__;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-2898c7d813c35c74347b
        "text:note-ref;1.3;p;text;text:note-class=footnote!__invalid__;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-2898c7d813c35c74347b
        "text:note-ref;1.4;p;text;text:note-class=footnote!__invalid__;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-2898c7d813c35c74347b
        "text:object-count;1.2;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-cd37729188a8a2f108f4
        "text:object-count;1.3;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-cd37729188a8a2f108f4
        "text:object-count;1.4;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-cd37729188a8a2f108f4
        "text:page-continuation;1.2;p;text;text:select-page=previous!__invalid__;text:string-value=text", // ODFG-1eb3b93228a21c9fa967
        "text:page-continuation;1.3;p;text;text:select-page=previous!__invalid__;text:string-value=text", // ODFG-1eb3b93228a21c9fa967
        "text:page-continuation;1.4;p;text;text:select-page=previous!__invalid__;text:string-value=text", // ODFG-1eb3b93228a21c9fa967
        "text:page-count;1.2;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-400fb37553f6b2631737
        "text:page-count;1.3;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-400fb37553f6b2631737
        "text:page-count;1.4;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-400fb37553f6b2631737
        "text:page-number;1.2;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a,text:fixed=true!__invalid__,text:page-adjust=1!not-a-value,text:select-page=previous!__invalid__", // ODFG-f603ef9965b344736cc1
        "text:page-number;1.3;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a,text:fixed=true!__invalid__,text:page-adjust=1!not-a-value,text:select-page=previous!__invalid__", // ODFG-f603ef9965b344736cc1
        "text:page-number;1.4;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a,text:fixed=true!__invalid__,text:page-adjust=1!not-a-value,text:select-page=previous!__invalid__", // ODFG-f603ef9965b344736cc1
        "text:page-variable-get;1.2;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-02e5cb5cf15238aca475
        "text:page-variable-get;1.3;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-02e5cb5cf15238aca475
        "text:page-variable-get;1.4;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-02e5cb5cf15238aca475
        "text:page-variable-set;1.2;p;text;;text:active=true!__invalid__,text:page-adjust=1!not-a-value", // ODFG-3bb86233f0eb2b3cd62e
        "text:page-variable-set;1.3;p;text;;text:active=true!__invalid__,text:page-adjust=1!not-a-value", // ODFG-3bb86233f0eb2b3cd62e
        "text:page-variable-set;1.4;p;text;;text:active=true!__invalid__,text:page-adjust=1!not-a-value", // ODFG-3bb86233f0eb2b3cd62e
        "text:paragraph-count;1.2;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-779cf9d9ef3c8edcc262
        "text:paragraph-count;1.3;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-779cf9d9ef3c8edcc262
        "text:paragraph-count;1.4;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-779cf9d9ef3c8edcc262
        "text:placeholder;1.2;p;text;text:placeholder-type=text!__invalid__;text:description=text", // ODFG-e7874065c66983f9f803
        "text:placeholder;1.3;p;text;text:placeholder-type=text!__invalid__;text:description=text", // ODFG-e7874065c66983f9f803
        "text:placeholder;1.4;p;text;text:placeholder-type=text!__invalid__;text:description=text", // ODFG-e7874065c66983f9f803
        "text:print-date;1.2;p;text;;style:data-style-name=n1,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-677dbbb8d1eb523c7056
        "text:print-date;1.3;p;text;;style:data-style-name=n1,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-677dbbb8d1eb523c7056
        "text:print-date;1.4;p;text;;style:data-style-name=n1,text:date-value=2024-01-02!not-a-value,text:fixed=true!__invalid__", // ODFG-677dbbb8d1eb523c7056
        "text:print-time;1.2;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-value=03:04:05!not-a-value", // ODFG-397062085057038b9ef7
        "text:print-time;1.3;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-value=03:04:05!not-a-value", // ODFG-397062085057038b9ef7
        "text:print-time;1.4;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-value=03:04:05!not-a-value", // ODFG-397062085057038b9ef7
        "text:printed-by;1.2;p;text;;text:fixed=true!__invalid__", // ODFG-9af0c3b76a37b79dd7cc
        "text:printed-by;1.3;p;text;;text:fixed=true!__invalid__", // ODFG-9af0c3b76a37b79dd7cc
        "text:printed-by;1.4;p;text;;text:fixed=true!__invalid__", // ODFG-9af0c3b76a37b79dd7cc
        "text:reference-ref;1.2;p;text;;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-749a0a6115ea33ecfe3a
        "text:reference-ref;1.3;p;text;;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-749a0a6115ea33ecfe3a
        "text:reference-ref;1.4;p;text;;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-749a0a6115ea33ecfe3a
        "text:ruby-text;1.2;ruby;text;;text:style-name=n1", // ODFG-41596eda144f06e5efee
        "text:ruby-text;1.3;ruby;text;;text:style-name=n1", // ODFG-41596eda144f06e5efee
        "text:ruby-text;1.4;ruby;text;;text:style-name=n1", // ODFG-41596eda144f06e5efee
        "text:script;1.2;p;text;;script:language=text,xlink:href=http://example.com/+xlink:type=simple+empty:content=1,xlink:type=simple!__invalid__+xlink:href=http://example.com/+empty:content=1", // ODFG-42349b88b77f18360178
        "text:script;1.3;p;text;;script:language=text,xlink:href=http://example.com/+xlink:type=simple+empty:content=1,xlink:type=simple!__invalid__+xlink:href=http://example.com/+empty:content=1", // ODFG-42349b88b77f18360178
        "text:script;1.4;p;text;;script:language=text,xlink:href=http://example.com/+xlink:type=simple+empty:content=1,xlink:type=simple!__invalid__+xlink:href=http://example.com/+empty:content=1", // ODFG-42349b88b77f18360178
        "text:sequence;1.2;p;text;text:name=text;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a,text:formula=text,text:ref-name=text", // ODFG-95d7be41cb497da92194
        "text:sequence;1.3;p;text;text:name=text;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a,text:formula=text,text:ref-name=text", // ODFG-95d7be41cb497da92194
        "text:sequence;1.4;p;text;text:name=text;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a,text:formula=text,text:ref-name=text", // ODFG-95d7be41cb497da92194
        "text:sequence-ref;1.2;p;text;;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-045dbaa3c4e066f8cc93
        "text:sequence-ref;1.3;p;text;;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-045dbaa3c4e066f8cc93
        "text:sequence-ref;1.4;p;text;;text:ref-name=text,text:reference-format=page!__invalid__", // ODFG-045dbaa3c4e066f8cc93
        "text:sheet-name;1.2;p;text;;", // ODFG-8c802d201c5fa75aef72
        "text:sheet-name;1.3;p;text;;", // ODFG-8c802d201c5fa75aef72
        "text:sheet-name;1.4;p;text;;", // ODFG-8c802d201c5fa75aef72
        "text:subject;1.2;p;text;;text:fixed=true!__invalid__", // ODFG-dbc8ea0c2ecfae2a9e3b
        "text:subject;1.3;p;text;;text:fixed=true!__invalid__", // ODFG-dbc8ea0c2ecfae2a9e3b
        "text:subject;1.4;p;text;;text:fixed=true!__invalid__", // ODFG-dbc8ea0c2ecfae2a9e3b
        "text:table-count;1.2;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-7b8e3835029119436679
        "text:table-count;1.3;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-7b8e3835029119436679
        "text:table-count;1.4;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-7b8e3835029119436679
        "text:table-formula;1.2;p;text;;style:data-style-name=n1,text:display=value!__invalid__,text:formula=text", // ODFG-47bb280604b51b5165fe
        "text:table-formula;1.3;p;text;;style:data-style-name=n1,text:display=value!__invalid__,text:formula=text", // ODFG-47bb280604b51b5165fe
        "text:table-formula;1.4;p;text;;style:data-style-name=n1,text:display=value!__invalid__,text:formula=text", // ODFG-47bb280604b51b5165fe
        "text:template-name;1.2;p;text;;text:display=full!__invalid__", // ODFG-2fb355be2db1d8c1311d
        "text:template-name;1.3;p;text;;text:display=full!__invalid__", // ODFG-2fb355be2db1d8c1311d
        "text:template-name;1.4;p;text;;text:display=full!__invalid__", // ODFG-2fb355be2db1d8c1311d
        "text:text-input;1.2;p;text;;text:description=text", // ODFG-e5c528cba52a9f8368a1
        "text:text-input;1.3;p;text;;text:description=text", // ODFG-e5c528cba52a9f8368a1
        "text:text-input;1.4;p;text;;text:description=text", // ODFG-e5c528cba52a9f8368a1
        "text:time;1.2;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-adjust=PT1H!not-a-value,text:time-value=03:04:05!not-a-value", // ODFG-bc73df14f4afb5d9b992
        "text:time;1.3;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-adjust=PT1H!not-a-value,text:time-value=03:04:05!not-a-value", // ODFG-bc73df14f4afb5d9b992
        "text:time;1.4;p;text;;style:data-style-name=n1,text:fixed=true!__invalid__,text:time-adjust=PT1H!not-a-value,text:time-value=03:04:05!not-a-value", // ODFG-bc73df14f4afb5d9b992
        "text:title;1.2;p;text;;text:fixed=true!__invalid__", // ODFG-9568ce9d955f1c4dfa11
        "text:title;1.3;p;text;;text:fixed=true!__invalid__", // ODFG-9568ce9d955f1c4dfa11
        "text:title;1.4;p;text;;text:fixed=true!__invalid__", // ODFG-9568ce9d955f1c4dfa11
        "text:user-defined;1.2;p;text;text:name=text;style:data-style-name=n1,text:fixed=true!__invalid__", // ODFG-375348523a163332d018
        "text:user-defined;1.3;p;text;text:name=text;style:data-style-name=n1,text:fixed=true!__invalid__", // ODFG-375348523a163332d018
        "text:user-defined;1.4;p;text;text:name=text;style:data-style-name=n1,text:fixed=true!__invalid__", // ODFG-375348523a163332d018
        "text:user-field-get;1.2;p;text;text:name=text;style:data-style-name=n1,text:display=value!__invalid__", // ODFG-abfdea5abd5cd3064073
        "text:user-field-get;1.3;p;text;text:name=text;style:data-style-name=n1,text:display=value!__invalid__", // ODFG-abfdea5abd5cd3064073
        "text:user-field-get;1.4;p;text;text:name=text;style:data-style-name=n1,text:display=value!__invalid__", // ODFG-abfdea5abd5cd3064073
        "text:user-field-input;1.2;p;text;text:name=text;style:data-style-name=n1,text:description=text", // ODFG-8a22a80f5293a92aca73
        "text:user-field-input;1.3;p;text;text:name=text;style:data-style-name=n1,text:description=text", // ODFG-8a22a80f5293a92aca73
        "text:user-field-input;1.4;p;text;text:name=text;style:data-style-name=n1,text:description=text", // ODFG-8a22a80f5293a92aca73
        "text:variable-get;1.2;p;text;text:name=text;style:data-style-name=n1,text:display=value!__invalid__", // ODFG-8da52e8be838fddaf89f
        "text:variable-get;1.3;p;text;text:name=text;style:data-style-name=n1,text:display=value!__invalid__", // ODFG-8da52e8be838fddaf89f
        "text:variable-get;1.4;p;text;text:name=text;style:data-style-name=n1,text:display=value!__invalid__", // ODFG-8da52e8be838fddaf89f
        "text:variable-input;1.2;p;text;office:value-type=float!__invalid__,text:name=text;style:data-style-name=n1,text:description=text,text:display=value!__invalid__", // ODFG-2d80c282e4aee1e6a6e6
        "text:variable-input;1.3;p;text;office:value-type=float!__invalid__,text:name=text;style:data-style-name=n1,text:description=text,text:display=value!__invalid__", // ODFG-2d80c282e4aee1e6a6e6
        "text:variable-input;1.4;p;text;office:value-type=float!__invalid__,text:name=text;style:data-style-name=n1,text:description=text,text:display=value!__invalid__", // ODFG-2d80c282e4aee1e6a6e6
        "text:variable-set;1.2;p;text;text:name=text,office:value-type=float,office:value=1;style:data-style-name=n1,text:display=value!__invalid__,text:formula=text", // ODFG-9ee5e8d448211877824e
        "text:variable-set;1.3;p;text;text:name=text,office:value-type=float,office:value=1;style:data-style-name=n1,text:display=value!__invalid__,text:formula=text", // ODFG-9ee5e8d448211877824e
        "text:variable-set;1.4;p;text;text:name=text,office:value-type=float,office:value=1;style:data-style-name=n1,text:display=value!__invalid__,text:formula=text", // ODFG-9ee5e8d448211877824e
        "text:word-count;1.2;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-0eb3424f36ec05e4701f
        "text:word-count;1.3;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-0eb3424f36ec05e4701f
        "text:word-count;1.4;p;text;;style:num-format=a,style:num-letter-sync=true!__invalid__+style:num-format=a", // ODFG-0eb3424f36ec05e4701f
    ];

    private static readonly List<string> Failures = [];

    private static readonly string[] Values = ["", " ", " \t\n ", "文字😀<&>"];
    private static readonly string[] Parents = ["p", "h", "span", "a", "ruby-base", "meta", "meta-field"];

    /// <summary>
    /// A minimal instance with only the required attributes is valid and keeps its character data.
    /// 只有必要屬性的最小實例有效，且保留字元資料。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void MinimalInstancesAreValidAndKeepCharacterData(OdfVersion version)
    {
        Failures.Clear();
        int exercised = 0;
        foreach (FieldCase item in For(version))
        {
            AssertValid(version, Document(version, item, Build(item)), item.Name);
            if (item.Content == "text")
                foreach (string value in Values)
                    AssertRoundTrip(version, item, value);
            exercised++;
        }
        Assert.True(exercised > 0);
        Assert.True(Failures.Count == 0, string.Join("\n", Failures.Select(failure => failure.Length > 240 ? failure[..240] : failure).Distinct()));
    }

    /// <summary>
    /// Every declared optional attribute is accepted with a valid value and rejected with an invalid one.
    /// 每個宣告的可選屬性接受有效值，並拒絕無效值。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void OptionalAttributesFollowTheirDatatypes(OdfVersion version)
    {
        Failures.Clear();
        foreach (FieldCase item in For(version))
            foreach (Attribute attribute in item.Optional.Concat(item.Required))
            {
                XElement valid = Build(item);
                valid.SetAttributeValue(attribute.XName, attribute.Value);
                Apply(valid, attribute);
                AssertValid(version, Document(version, item, valid), item.Name + " " + attribute.Name);
                if (attribute.Invalid is null)
                    continue;
                XElement bad = Build(item);
                Apply(bad, attribute with { Value = attribute.Invalid });
                AssertSchemaInvalid(version, Document(version, item, bad), item.Name + " " + attribute.Name);
            }
        Assert.True(Failures.Count == 0, string.Join("\n", Failures.Select(failure => failure.Length > 240 ? failure[..240] : failure).Distinct()));
    }

    /// <summary>
    /// Elements reject missing required attributes, unknown native attributes, child elements and wrong placement.
    /// 元素拒絕缺少必要屬性、未知原生屬性、子元素及錯誤位置。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void InvalidNativeStructureIsRejected(OdfVersion version)
    {
        Failures.Clear();
        XNamespace text = OdfNamespaces.Text;
        foreach (FieldCase item in For(version))
        {
            foreach (Attribute required in item.Required)
            {
                XElement element = Build(item);
                element.SetAttributeValue(required.XName, null);
                AssertSchemaInvalid(version, Document(version, item, element), item.Name + " missing " + required.Name);
            }
            XElement bogus = Build(item);
            bogus.SetAttributeValue(text + "bogus", "x");
            AssertSchemaInvalid(version, Document(version, item, bogus), item.Name + " bogus");
            if (item.Content != "model")
            {
                XElement child = Build(item);
                child.Add(new XElement(text + "span", "child"));
                AssertSchemaInvalid(version, Document(version, item, child), item.Name + " child");
            }
            AssertSchemaInvalid(version, Document(version, item with { Wrapper = "misplaced" }, Build(item)), item.Name + " placement");
            XElement lookAlike = new(XName.Get(item.Local, OdfNamespaces.Office));
            foreach (Attribute required in item.Required)
                lookAlike.SetAttributeValue(required.XName, required.Value);
            AssertSchemaInvalid(version, Document(version, item, lookAlike), item.Name + " look-alike");
        }
        Assert.True(Failures.Count == 0, string.Join("\n", Failures.Select(failure => failure.Length > 240 ? failure[..240] : failure).Distinct()));
    }

    /// <summary>
    /// Paragraph-level elements are usable in every documented text parent.
    /// 段落層級元素可用於每個文件列出的文字父元素。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void ParagraphElementsAreUsableInEveryTextParent(OdfVersion version)
    {
        Failures.Clear();
        foreach (FieldCase item in For(version).Where(item => item.Wrapper == "p"))
            foreach (string parent in Parents)
                AssertValid(version, Document(version, item, Build(item), parent), item.Name + " in " + parent);
        Assert.True(Failures.Count == 0, string.Join("\n", Failures.Select(failure => failure.Length > 240 ? failure[..240] : failure).Distinct()));
    }

    /// <summary>
    /// Elements with structured content accept their declared children and reject undeclared ones.
    /// 具結構化內容的元素接受宣告的子元素，並拒絕未宣告的子元素。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void StructuredContentFollowsItsDefinition(OdfVersion version)
    {
        Failures.Clear();
        XNamespace text = OdfNamespaces.Text;
        XNamespace form = OdfNamespaces.Form;
        XNamespace xlink = OdfNamespaces.XLink;
        XNamespace script = OdfNamespaces.Script;
        XNamespace office = OdfNamespaces.Office;
        int exercised = 0;
        foreach (FieldCase item in For(version).Where(item => item.Content == "model"))
        {
            List<(XElement Element, bool Valid, string Label)> variants = [];
            XElement stray = Build(item);
            stray.Add(new XElement(text + "span", "child"));
            variants.Add((stray, false, "stray child"));
            if (item.Local == "drop-down")
            {
                XElement ok = Build(item);
                ok.Add(new XElement(text + "label", new XAttribute(text + "value", "a"), new XAttribute(text + "current-selected", "true")), "Value");
                variants.Add((ok, true, "label"));
                XElement bad = Build(item);
                bad.Add(new XElement(text + "label", new XAttribute(text + "current-selected", "maybe")));
                variants.Add((bad, false, "invalid selected flag"));
                XElement unknown = Build(item);
                unknown.Add(new XElement(text + "label", new XAttribute(text + "bogus", "x")));
                variants.Add((unknown, false, "unknown label attribute"));
            }
            else if (item.Local == "execute-macro")
            {
                XElement ok = Build(item);
                ok.Add(new XElement(office + "event-listeners", new XElement(script + "event-listener", new XAttribute(script + "event-name", "dom:click"), new XAttribute(script + "language", "ooo:script"), new XAttribute(script + "macro-name", "Macro"))));
                variants.Add((ok, true, "event listener"));
                XElement missing = Build(item);
                missing.Add(new XElement(office + "event-listeners", new XElement(script + "event-listener", new XAttribute(script + "language", "ooo:script"), new XAttribute(script + "macro-name", "Macro"))));
                variants.Add((missing, false, "listener without event"));
            }
            else
            {
                XElement ok = Build(item);
                ok.Add(new XElement(form + "connection-resource", new XAttribute(xlink + "href", "http://example.com/")));
                variants.Add((ok, true, "connection resource"));
                XElement both = Build(item);
                both.SetAttributeValue(text + "database-name", "db");
                both.Add(new XElement(form + "connection-resource", new XAttribute(xlink + "href", "http://example.com/")));
                variants.Add((both, false, "database name and resource"));
            }
            foreach ((XElement element, bool valid, string label) in variants)
            {
                if (valid)
                    AssertValid(version, Document(version, item, element), item.Name + " " + label);
                else
                    AssertSchemaInvalid(version, Document(version, item, element), item.Name + " " + label);
            }
            exercised++;
        }
        Assert.True(exercised > 0);
        Assert.True(Failures.Count == 0, string.Join("\n", Failures.Select(failure => failure.Length > 240 ? failure[..240] : failure).Distinct()));
    }

    /// <summary>
    /// Loading and saving keeps every declared attribute value and the character data of each element.
    /// 載入並儲存後，每個宣告的屬性值與元素字元資料都保持不變。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void SavingPreservesDeclaredAttributesAndContent(OdfVersion version)
    {
        Failures.Clear();
        foreach (FieldCase item in For(version))
            foreach (Attribute attribute in item.Optional.Concat(item.Required).Prepend(new Attribute(string.Empty, string.Empty, null, [])))
            {
                XElement element = Build(item);
                if (attribute.Name.Length > 0)
                    Apply(element, attribute);
                if (item.Content == "text" && element.Value.Length == 0)
                    element.Value = "Value";
                XElement document = Document(version, item, element);
                string extension = item.Wrapper == "presentation" ? "fodp" : "fodt";
                using MemoryStream flat = new(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
                using OdfDocument loaded = OdfDocument.Load(flat, "field." + extension);
                using MemoryStream saved = new();
                loaded.SaveToStream(saved);
                using OdfPackage package = OdfPackage.Open(new MemoryStream(saved.ToArray()));
                XName name = XName.Get(item.Local, item.Namespace);
                XElement? found = null;
                foreach (string entry in new[] { "content.xml", "styles.xml" })
                    if (found is null && package.HasEntry(entry))
                        found = XElement.Parse(Encoding.UTF8.GetString(package.ReadEntry(entry)), LoadOptions.PreserveWhitespace).Descendants(name).FirstOrDefault();
                string context = item.Name + " " + attribute.Name;
                if (found is null)
                {
                    Failures.Add(context + " was lost");
                    continue;
                }
                string Describe(XElement node) => string.Join(",", node.Attributes().Where(a => !a.IsNamespaceDeclaration).Select(a => a.Name.NamespaceName + a.Name.LocalName + "=" + a.Value).OrderBy(x => x, StringComparer.Ordinal));
                if (Describe(found) != Describe(element) || found.Value != element.Value)
                    Failures.Add(context + " changed: " + Describe(found) + " / " + Describe(element));
            }
        Assert.True(Failures.Count == 0, string.Join("\n", Failures.Select(failure => failure.Length > 240 ? failure[..240] : failure).Distinct()));
    }

    private static IEnumerable<FieldCase> For(OdfVersion version)
    {
        string label = OdfVersionInfo.ToVersionString(version);
        return Cases.Select(FieldCase.Parse).Where(item => item.Version == label);
    }

    private static void Apply(XElement element, Attribute attribute)
    {
        element.SetAttributeValue(attribute.XName, attribute.Value);
        foreach (Attribute companion in attribute.Companions)
        {
            if (companion.Name == "empty:content")
                element.RemoveNodes();
            else
                element.SetAttributeValue(companion.XName, companion.Value);
        }
    }

    private static XElement Build(FieldCase item)
    {
        XElement element = new(XName.Get(item.Local, item.Namespace));
        foreach (Attribute required in item.Required)
            element.SetAttributeValue(required.XName, required.Value);
        if (item.Content == "text")
            element.Add("Value");
        return element;
    }

    private static XElement Document(OdfVersion version, FieldCase item, XElement element, string parent = "p")
    {
        XNamespace office = OdfNamespaces.Office;
        XNamespace text = OdfNamespaces.Text;
        XNamespace number = OdfNamespaces.Number;
        XNamespace style = OdfNamespaces.Style;
        string kind = item.Wrapper == "presentation" ? "presentation" : "text";
        XElement body;
        XElement? styles = null;
        switch (item.Wrapper)
        {
            case "currency":
                styles = new XElement(office + "styles", new XElement(number + "currency-style", new XAttribute(style + "name", "N1"), element));
                body = new XElement(office + "text");
                break;
            case "embedded":
                styles = new XElement(office + "styles", new XElement(number + "number-style", new XAttribute(style + "name", "N1"), new XElement(number + "number", element)));
                body = new XElement(office + "text");
                break;
            case "numberstyle":
                styles = new XElement(office + "styles", new XElement(number + "number-style", new XAttribute(style + "name", "N1"), element, new XElement(number + "number")));
                body = new XElement(office + "text");
                break;
            case "linenumbering":
                styles = new XElement(office + "styles", new XElement(text + "linenumbering-configuration", element));
                body = new XElement(office + "text");
                break;
            case "notes":
                styles = new XElement(office + "styles", new XElement(text + "notes-configuration", new XAttribute(text + "note-class", "footnote"), element));
                body = new XElement(office + "text");
                break;
            case "presentation":
                body = new XElement(office + "presentation", element);
                break;
            case "toc":
                body = new XElement(office + "text", new XElement(text + "table-of-content", new XAttribute(text + "name", "T1"),
                    new XElement(text + "table-of-content-source", element), new XElement(text + "index-body")));
                break;
            case "tocentry":
                body = new XElement(office + "text", new XElement(text + "table-of-content", new XAttribute(text + "name", "T1"),
                    new XElement(text + "table-of-content-source", new XElement(text + "table-of-content-entry-template", new XAttribute(text + "outline-level", "1"), new XAttribute(text + "style-name", "S1"), element)),
                    new XElement(text + "index-body")));
                break;
            case "ruby":
                body = new XElement(office + "text", new XElement(text + "p", new XElement(text + "ruby", new XElement(text + "ruby-base", "b"), element)));
                break;
            case "note":
                body = new XElement(office + "text", new XElement(text + "p", new XElement(text + "note", new XAttribute(text + "note-class", "footnote"), element, new XElement(text + "note-body"))));
                break;
            case "measure":
                body = new XElement(office + "text", new XElement(text + "p", new XElement(XName.Get("measure", OdfNamespaces.Draw),
                    new XAttribute(XName.Get("x1", OdfNamespaces.Svg), "0cm"), new XAttribute(XName.Get("y1", OdfNamespaces.Svg), "0cm"),
                    new XAttribute(XName.Get("x2", OdfNamespaces.Svg), "1cm"), new XAttribute(XName.Get("y2", OdfNamespaces.Svg), "1cm"),
                    new XAttribute(text + "anchor-type", "paragraph"), new XElement(text + "p", element))));
                break;
            case "misplaced":
                body = new XElement(office + "text", element);
                break;
            default:
                body = new XElement(office + "text", Wrap(parent, element));
                break;
        }
        XElement document = new(office + "document",
            new XAttribute(XNamespace.Xmlns + "o", office),
            new XAttribute(XNamespace.Xmlns + "t", OdfNamespaces.Text),
            new XAttribute(XNamespace.Xmlns + "x", OdfNamespaces.XLink),
            new XAttribute(XNamespace.Xmlns + "n", OdfNamespaces.Number),
            new XAttribute(XNamespace.Xmlns + "s", OdfNamespaces.Style),
            new XAttribute(XNamespace.Xmlns + "pr", OdfNamespaces.Presentation),
            new XAttribute(office + "version", OdfVersionInfo.ToVersionString(version)),
            new XAttribute(office + "mimetype", "application/vnd.oasis.opendocument." + kind));
        if (styles is not null)
            document.Add(styles);
        document.Add(new XElement(office + "body", body));
        return document;
    }

    private static XElement Wrap(string parent, XElement field)
    {
        XNamespace text = OdfNamespaces.Text;
        return parent switch
        {
            "p" => new XElement(text + "p", field),
            "h" => new XElement(text + "h", new XAttribute(text + "outline-level", "1"), field),
            "span" => new XElement(text + "p", new XElement(text + "span", field)),
            "a" => new XElement(text + "p", new XElement(text + "a", new XAttribute(XName.Get("type", OdfNamespaces.XLink), "simple"), new XAttribute(XName.Get("href", OdfNamespaces.XLink), "http://example.com/"), field)),
            "meta" => new XElement(text + "p", new XElement(text + "meta", field)),
            "meta-field" => new XElement(text + "p", new XElement(text + "meta-field", new XAttribute(XNamespace.Xml + "id", "meta-field-1"), field)),
            _ => new XElement(text + "p", new XElement(text + "ruby", new XElement(text + "ruby-base", field), new XElement(text + "ruby-text", "r")))
        };
    }

    private static void AssertRoundTrip(OdfVersion version, FieldCase item, string value)
    {
        XElement element = Build(item);
        element.Value = value;
        XElement document = Document(version, item, element);
        AssertValid(version, document, item.Name);
        byte[] bytes = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
        foreach (bool streamReader in new[] { false, true })
        {
            using MemoryStream input = new(bytes);
            OdfNode dom = streamReader ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(bytes);
            Assert.Equal(value, dom.Descendants().Single(node => node.NamespaceUri == item.Namespace && node.LocalName == item.Local).TextContent);
            using MemoryStream output = new();
            OdfXmlWriter.Write(dom, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
            Assert.Equal(value, saved.Descendants(XName.Get(item.Local, item.Namespace)).Single().Value);
            AssertValid(version, saved, item.Name);
        }
    }

    private static OdfValidationReport Validate(XElement document, OdfComplianceProfile profile)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
        string path = (string?)document.Attribute(XName.Get("mimetype", OdfNamespaces.Office)) == "application/vnd.oasis.opendocument.presentation" ? "field.fodp" : "field.fodt";
        return OdfValidator.Validate(input, path, profile);
    }

    private static void AssertValid(OdfVersion version, XElement document, string context)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
        {
            OdfValidationReport report = Validate(document, profile);
            if (!report.IsValid)
                Failures.Add(context + " " + string.Join(";", report.Issues.Select(issue => issue.RuleId + ":" + issue.Message)));
        }
    }

    private static void AssertSchemaInvalid(OdfVersion version, XElement document, string context)
    {
        foreach (OdfComplianceProfile profile in Profiles(version))
            if (!Validate(document, profile).Issues.Any(issue => issue.RuleId == "ODF3101"))
                Failures.Add(context + " was not rejected");
    }

    private static OdfComplianceProfile[] Profiles(OdfVersion version) => version switch
    {
        OdfVersion.Odf12 => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        OdfVersion.Odf13 => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };

    private sealed record Attribute(string Name, string Value, string? Invalid, Attribute[] Companions)
    {
        public XName XName
        {
            get
            {
                int colon = Name.IndexOf(':');
                string ns = Name[..colon] switch
                {
                    "text" => OdfNamespaces.Text,
                    "office" => OdfNamespaces.Office,
                    "style" => OdfNamespaces.Style,
                    "number" => OdfNamespaces.Number,
                    "presentation" => OdfNamespaces.Presentation,
                    "meta" => OdfNamespaces.Meta,
                    "table" => OdfNamespaces.Table,
                    "fo" => OdfNamespaces.Fo,
                    "xlink" => OdfNamespaces.XLink,
                    "svg" => OdfNamespaces.Svg,
                    "draw" => OdfNamespaces.Draw,
                    "script" => OdfNamespaces.Script,
                    "empty" => string.Empty,
                    _ => throw new InvalidOperationException(Name)
                };
                return XName.Get(Name[(colon + 1)..], ns);
            }
        }

        public static Attribute Parse(string text)
        {
            string[] pieces = text.Split('+');
            int equals = pieces[0].IndexOf('=');
            string value = pieces[0][(equals + 1)..];
            int bang = value.IndexOf('!');
            return new Attribute(pieces[0][..equals], bang < 0 ? value : value[..bang], bang < 0 ? null : value[(bang + 1)..], pieces.Skip(1).Select(Parse).ToArray());
        }
    }

    private sealed record FieldCase(string Name, string Version, string Wrapper, string Content, Attribute[] Required, Attribute[] Optional)
    {
        public string Local => Name[(Name.IndexOf(':') + 1)..];

        public string Namespace => Name[..Name.IndexOf(':')] switch
        {
            "text" => OdfNamespaces.Text,
            "number" => OdfNamespaces.Number,
            "presentation" => OdfNamespaces.Presentation,
            _ => throw new InvalidOperationException(Name)
        };

        public static FieldCase Parse(string row)
        {
            string[] parts = row.Split(';');
            static Attribute[] Attributes(string text) => text.Length == 0 ? [] : text.Split(',').Select(Attribute.Parse).ToArray();
            return new FieldCase(parts[0], parts[1], parts[2], parts[3], Attributes(parts[4]), Attributes(parts[5]));
        }
    }
}
