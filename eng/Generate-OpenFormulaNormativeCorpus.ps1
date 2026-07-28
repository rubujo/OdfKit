#Requires -Version 7.0
<#
.SYNOPSIS
    以 OASIS OpenFormula 規範與 LibreOffice 產生 Safe Large 獨立 oracle corpus。
.DESCRIPTION
    從 ODF 1.4 Part 4 HTML 擷取每個 Large Group 函式的條文與簽章，建立一個可重現的
    正常語意案例，再交由指定版本的 LibreOffice headless 計算。產出的預期值會提交至
    儲存庫，讓一般 CI 不需要安裝 LibreOffice 或連線網路。

    LibreOffice 是獨立差異 oracle，不取代 OASIS 規範；每筆案例仍保留規範條文定位。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SpecPath,

    [Parameter(Mandatory)]
    [string]$SofficePath,

    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot 'docs/openformula-conformance-manifest.json'
$outputPath = Join-Path $repoRoot 'docs/openformula-normative-corpus.json'
$specUri = 'https://docs.oasis-open.org/office/OpenDocument/v1.4/os/part4-formula/OpenDocument-v1.4-os-part4-formula.html'

if (-not (Test-Path -LiteralPath $SpecPath -PathType Leaf)) {
    throw "找不到 OASIS OpenFormula HTML：$SpecPath"
}

if (-not (Test-Path -LiteralPath $SofficePath -PathType Leaf)) {
    throw "找不到 LibreOffice soffice：$SofficePath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$specHtml = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $SpecPath))
$specSha256 = (Get-FileHash -LiteralPath $SpecPath -Algorithm SHA256).Hash.ToLowerInvariant()

function Get-PlainText {
    param([Parameter(Mandatory)][string]$Html)

    $withoutTags = [regex]::Replace($Html, '<[^>]+>', ' ')
    return ([System.Net.WebUtility]::HtmlDecode($withoutTags) -replace '\s+', ' ').Trim()
}

$functionSpecificationCache = $null

function Get-FunctionSpecification {
    param([Parameter(Mandatory)][string]$FunctionName)

    if ($null -eq $script:functionSpecificationCache) {
        $script:functionSpecificationCache = @{}
        foreach ($match in [regex]::Matches(
            $specHtml,
            '(?is)<h3[^>]*>(.*?)</h3>(.*?)(?=<h[23][^>]*>)')) {
            $heading = Get-PlainText -Html $match.Groups[1].Value
            $headingMatch = [regex]::Match(
                $heading,
                '^(\d+\.\d+\.\d+)\s+(.+)$')
            if (-not $headingMatch.Success) {
                continue
            }

            $body = Get-PlainText -Html $match.Groups[2].Value
            $syntaxMatch = [regex]::Match(
                $body,
                'Syntax:\s*(.*?)(?=\s+(?:Returns|Semantics):)')
            if ($syntaxMatch.Success) {
                $functionKey = $headingMatch.Groups[2].Value -replace '\s+', ''
                $script:functionSpecificationCache[$functionKey] = [ordered]@{
                    section = $headingMatch.Groups[1].Value
                    syntax = $syntaxMatch.Groups[1].Value.Trim()
                }
            }
        }
    }

    if (-not $script:functionSpecificationCache.ContainsKey($FunctionName)) {
        throw "無法在 OASIS OpenFormula HTML 找到 $FunctionName 的 Syntax。"
    }

    return $script:functionSpecificationCache[$FunctionName]
}

function Get-RequiredParameterTypes {
    param(
        [Parameter(Mandatory)][string]$FunctionName,
        [Parameter(Mandatory)][string]$Syntax
    )

    $firstSignature = ($Syntax -split '\s+or\s+[A-Z0-9.]+\s*\(', 2)[0]
    $parameterMatch = [regex]::Match($firstSignature, '^[A-Z0-9.]+\s*\((.*)\)')
    if (-not $parameterMatch.Success) {
        throw "無法解析 $FunctionName 的 Syntax：$Syntax"
    }

    $required = $parameterMatch.Groups[1].Value
    while ($required -match '\[[^\[\]]*\]') {
        $required = [regex]::Replace($required, '\[[^\[\]]*\]', '')
    }

    $typePattern = '(?<![A-Za-z])(NumberSequenceList|ComplexSequence|LogicalSequence|NumberSequence|DateSequence|ReferenceList|TextOrNumber|DateParam|TimeParam|Database|Criteria|Criterion|Reference|Logical|Complex|Integer|Number|Scalar|Field|Basis|Array|Any|Text)(?![A-Za-z])'
    return @(
        [regex]::Matches($required, $typePattern) |
            ForEach-Object { $_.Groups[1].Value }
    )
}

function Get-DefaultArgument {
    param([Parameter(Mandatory)][string]$Type)

    switch ($Type) {
        'Number' { return '2' }
        'Integer' { return '2' }
        'Logical' { return 'TRUE()' }
        'Text' { return '"x"' }
        'TextOrNumber' { return '2' }
        'Scalar' { return '2' }
        'Any' { return '2' }
        'Complex' { return '"1+i"' }
        'Reference' { return '[Data.A2]' }
        'ReferenceList' { return '[Data.A2]' }
        'NumberSequence' { return '{1;2;3}' }
        'NumberSequenceList' { return '{1;2;3}' }
        'DateSequence' { return '{43831;43832;43833}' }
        'LogicalSequence' { return '{TRUE();FALSE();TRUE()}' }
        'ComplexSequence' { return '{"1+i";"2-i"}' }
        'DateParam' { return 'DATE(2020;1;15)' }
        'TimeParam' { return 'TIME(12;30;0)' }
        'Basis' { return '0' }
        'Criterion' { return '">1"' }
        'Database' { return '[Data.A1:.B4]' }
        'Field' { return '2' }
        'Criteria' { return '[Data.D1:.D2]' }
        'Array' { return '{1;2|3;4}' }
        default { throw "未知的 OpenFormula 參數型別：$Type" }
    }
}

$formulaOverrides = @{
    'ACCRINT' = 'ACCRINT(DATE(2020;1;1);DATE(2020;3;1);DATE(2020;7;1);0.05;1000;2;0)'
    'ACCRINTM' = 'ACCRINTM(DATE(2020;1;1);DATE(2020;7;1);0.05;1000;0)'
    'ACOS' = 'ACOS(0.5)'
    'AMORLINC' = 'AMORLINC(1000;DATE(2020;1;1);DATE(2020;12;31);100;0;0.1;0)'
    'ASIN' = 'ASIN(0.5)'
    'ATANH' = 'ATANH(0.5)'
    'ACOSH' = 'ACOSH(2)'
    'ACOTH' = 'ACOTH(2)'
    'AVEDEV' = 'AVEDEV(1;2;3)'
    'AVERAGEIF' = 'AVERAGEIF([Data.B2:.B4];">1";[Data.B2:.B4])'
    'AVERAGEIFS' = 'AVERAGEIFS([Data.B2:.B4];[Data.B2:.B4];">1")'
    'BASE' = 'BASE(255;16;4)'
    'BESSELI' = 'BESSELI(0;0)'
    'BESSELJ' = 'BESSELJ(0;0)'
    'BESSELK' = 'BESSELK(1;0)'
    'BESSELY' = 'BESSELY(1;0)'
    'BETADIST' = 'BETADIST(0.5;2;3)'
    'BETAINV' = 'BETAINV(0.5;2;3)'
    'BINOM.DIST.RANGE' = 'BINOM.DIST.RANGE(10;0.5;3;5)'
    'BINOMDIST' = 'BINOMDIST(3;10;0.5;FALSE())'
    'BIN2DEC' = 'BIN2DEC("10")'
    'BIN2HEX' = 'BIN2HEX("10")'
    'BIN2OCT' = 'BIN2OCT("10")'
    'CHAR' = 'CHAR(65)'
    'CHISQDIST' = 'CHISQDIST(2;3;TRUE())'
    'CHISQINV' = 'CHISQINV(0.5;3)'
    'CHOOSE' = 'CHOOSE(2;"a";"b";"c")'
    'CLEAN' = 'CLEAN("a"&CHAR(9)&"b")'
    'CODE' = 'CODE("A")'
    'COMPLEX' = 'COMPLEX(1;2)'
    'CONFIDENCE' = 'CONFIDENCE(0.05;2;100)'
    'CONVERT' = 'CONVERT(1;"m";"cm")'
    'CORREL' = 'CORREL({1;2;3};{2;4;6})'
    'COUPDAYBS' = 'COUPDAYBS(DATE(2020;1;15);DATE(2022;1;15);2;0)'
    'COUPDAYS' = 'COUPDAYS(DATE(2020;1;15);DATE(2022;1;15);2;0)'
    'COUPDAYSNC' = 'COUPDAYSNC(DATE(2020;1;15);DATE(2022;1;15);2;0)'
    'COUPNCD' = 'COUPNCD(DATE(2020;1;15);DATE(2022;1;15);2;0)'
    'COUPNUM' = 'COUPNUM(DATE(2020;1;15);DATE(2022;1;15);2;0)'
    'COUPPCD' = 'COUPPCD(DATE(2020;1;15);DATE(2022;1;15);2;0)'
    'COUNTBLANK' = 'COUNTBLANK([Data.C1:.C3])'
    'COLUMN' = 'COLUMN([Data.B2])'
    'COLUMNS' = 'COLUMNS([Data.A2:.B4])'
    'COUNTIF' = 'COUNTIF([Data.B2:.B4];">1")'
    'COUNTIFS' = 'COUNTIFS([Data.B2:.B4];">1";[Data.B2:.B4];"<3")'
    'CRITBINOM' = 'CRITBINOM(10;0.5;0.7)'
    'CUMIPMT' = 'CUMIPMT(0.01;12;1000;1;3;0)'
    'CUMPRINC' = 'CUMPRINC(0.01;12;1000;1;3;0)'
    'DATE' = 'DATE(2020;1;15)'
    'DATEDIF' = 'DATEDIF(DATE(2020;1;1);DATE(2021;2;3);"d")'
    'DATEVALUE' = 'DATEVALUE("2020-01-15")'
    'DCOUNT' = 'DCOUNT([Data.A1:.B4];2;[Data.D1:.D2])'
    'DCOUNTA' = 'DCOUNTA([Data.A1:.B4];2;[Data.D1:.D2])'
    'DGET' = 'DGET([Data.A1:.B4];2;[Data.E1:.E2])'
    'DAYS' = 'DAYS(DATE(2020;1;15);DATE(2020;1;1))'
    'DAYS360' = 'DAYS360(DATE(2020;1;1);DATE(2020;7;1);FALSE())'
    'DB' = 'DB(1000;100;5;1;12)'
    'DDB' = 'DDB(1000;100;5;1;2)'
    'DECIMAL' = 'DECIMAL("FF";16)'
    'DISC' = 'DISC(DATE(2020;1;1);DATE(2020;7;1);97;100;0)'
    'DOLLARDE' = 'DOLLARDE(1.02;16)'
    'DOLLARFR' = 'DOLLARFR(1.125;16)'
    'DURATION' = 'DURATION(DATE(2020;1;1);DATE(2025;1;1);0.05;0.04;2;0)'
    'EDATE' = 'EDATE(DATE(2020;1;31);1)'
    'EFFECT' = 'EFFECT(0.1;12)'
    'EOMONTH' = 'EOMONTH(DATE(2020;1;15);1)'
    'ERF' = 'ERF(1)'
    'ERFC' = 'ERFC(1)'
    'ERROR.TYPE' = 'ERROR.TYPE(#REF!)'
    'EUROCONVERT' = 'EUROCONVERT(1;"EUR";"DEM";TRUE();3)'
    'EXPONDIST' = 'EXPONDIST(1;2;TRUE())'
    'FDIST' = 'FDIST(1;5;10)'
    'FINDB' = 'FINDB("b";"abc";1)'
    'FINV' = 'FINV(0.5;5;10)'
    'FISHER' = 'FISHER(0.5)'
    'FISHERINV' = 'FISHERINV(0.5)'
    'FIXED' = 'FIXED(1234.5;1;TRUE())'
    'FORECAST' = 'FORECAST(4;{2;4;6};{1;2;3})'
    'FREQUENCY' = 'FREQUENCY({1;2;3;4};{2;3})'
    'FTEST' = 'FTEST({1;2;3};{2;3;4})'
    'GAMMADIST' = 'GAMMADIST(2;3;2;TRUE())'
    'GAMMAINV' = 'GAMMAINV(0.5;3;2)'
    'GEOMEAN' = 'GEOMEAN(1;2;4)'
    'GROWTH' = 'GROWTH({2;4;8};{1;2;3};{4})'
    'HARMEAN' = 'HARMEAN(1;2;4)'
    'HLOOKUP' = 'HLOOKUP(2;[Data.A6:.C7];2;FALSE())'
    'HYPGEOMDIST' = 'HYPGEOMDIST(1;4;3;10)'
    'IF' = 'IF(TRUE();1;2)'
    'IFERROR' = 'IFERROR(1/0;42)'
    'IFNA' = 'IFNA(#N/A;42)'
    'IMARGUMENT' = 'IMARGUMENT("1+i")'
    'IMDIV' = 'IMDIV("2+2i";"1+i")'
    'IMPOWER' = 'IMPOWER("1+i";2)'
    'IMPRODUCT' = 'IMPRODUCT("1+i";"1-i")'
    'IMSUB' = 'IMSUB("2+2i";"1+i")'
    'IMSUM' = 'IMSUM("1+i";"1-i")'
    'INDEX' = 'INDEX([Data.A2:.B4];2;2)'
    'INFO' = 'INFO("system")'
    'INDIRECT' = 'INDIRECT("Data.A2")'
    'INTERCEPT' = 'INTERCEPT({2;4;6};{1;2;3})'
    'INTRATE' = 'INTRATE(DATE(2020;1;1);DATE(2020;7;1);97;100;0)'
    'IPMT' = 'IPMT(0.01;1;12;1000;0;0)'
    'IRR' = 'IRR({-100;60;60};0.1)'
    'KURT' = 'KURT(1;2;3;4;5)'
    'LARGE' = 'LARGE({1;2;3};2)'
    'LEFT' = 'LEFT("abc";2)'
    'LEFTB' = 'LEFTB("abc";2)'
    'LEGACY.CHIDIST' = 'LEGACY.CHIDIST(2;3)'
    'LEGACY.CHIINV' = 'LEGACY.CHIINV(0.5;3)'
    'LEGACY.CHITEST' = 'LEGACY.CHITEST({10;20|30;40};{12;18|28;42})'
    'LEGACY.FDIST' = 'LEGACY.FDIST(1;5;10)'
    'LEGACY.FINV' = 'LEGACY.FINV(0.5;5;10)'
    'LEGACY.NORMSDIST' = 'LEGACY.NORMSDIST(0)'
    'LEGACY.NORMSINV' = 'LEGACY.NORMSINV(0.5)'
    'LEGACY.TDIST' = 'LEGACY.TDIST(1;10;2)'
    'LINEST' = 'LINEST({2;4;6};{1;2;3};TRUE();FALSE())'
    'LOGEST' = 'LOGEST({2;4;8};{1;2;3};TRUE();FALSE())'
    'LOGINV' = 'LOGINV(0.5;0;1)'
    'LOGNORMDIST' = 'LOGNORMDIST(1;0;1)'
    'LOOKUP' = 'LOOKUP(2;{1;2;3};{10;20;30})'
    'MATCH' = 'MATCH(2;[Data.B2:.B4];0)'
    'MDETERM' = 'MDETERM({1;2|3;4})'
    'MDURATION' = 'MDURATION(DATE(2020;1;1);DATE(2025;1;1);0.05;0.04;2;0)'
    'MEDIAN' = 'MEDIAN(1;2;3)'
    'MID' = 'MID("abcd";2;2)'
    'MIDB' = 'MIDB("abcd";2;2)'
    'MINVERSE' = 'MINVERSE({1;2|3;4})'
    'MIRR' = 'MIRR({-100;60;60};0.1;0.12)'
    'MMULT' = 'MMULT({1;2|3;4};{5;6|7;8})'
    'MODE' = 'MODE(1;2;2;3)'
    'MULTINOMIAL' = 'MULTINOMIAL(2;3)'
    'NEGBINOMDIST' = 'NEGBINOMDIST(2;3;0.5)'
    'NETWORKDAYS' = 'NETWORKDAYS(DATE(2020;1;1);DATE(2020;1;10))'
    'NOMINAL' = 'NOMINAL(0.1;12)'
    'NORMDIST' = 'NORMDIST(0;0;1;TRUE())'
    'NORMINV' = 'NORMINV(0.5;0;1)'
    'NPER' = 'NPER(0.01;-100;1000;0;0)'
    'NPV' = 'NPV(0.1;100;100;100)'
    'NUMBERVALUE' = 'NUMBERVALUE("1,234.5";".";",")'
    'OFFSET' = 'OFFSET([Data.A2];2;1)'
    'ODDFPRICE' = 'ODDFPRICE(DATE(2008;11;11);DATE(2021;3;1);DATE(2008;10;15);DATE(2009;3;1);0.0785;0.0625;100;2;1)'
    'ODDFYIELD' = 'ODDFYIELD(DATE(2008;11;11);DATE(2021;3;1);DATE(2008;10;15);DATE(2009;3;1);0.0785;113.597717474078;100;2;1)'
    'ODDLPRICE' = 'ODDLPRICE(DATE(2021;10;1);DATE(2022;1;1);DATE(2021;7;1);0.05;0.04;100;2;0)'
    'ODDLYIELD' = 'ODDLYIELD(DATE(2021;10;1);DATE(2022;1;1);DATE(2021;7;1);0.05;95;100;2;0)'
    'PERCENTILE' = 'PERCENTILE({1;2;3;4};0.5)'
    'PERCENTRANK' = 'PERCENTRANK({1;2;3;4};2.5;3)'
    'PERMUT' = 'PERMUT(5;2)'
    'POISSON' = 'POISSON(2;3;TRUE())'
    'POWER' = 'POWER(2;3)'
    'PRICE' = 'PRICE(DATE(2020;1;1);DATE(2025;1;1);0.05;0.04;100;2;0)'
    'PRICEDISC' = 'PRICEDISC(DATE(2020;1;1);DATE(2020;7;1);0.04;100;0)'
    'PRICEMAT' = 'PRICEMAT(DATE(2020;1;1);DATE(2021;1;1);DATE(2019;7;1);0.05;0.04;0)'
    'PROB' = 'PROB({1;2;3};{0.2;0.3;0.5};2;3)'
    'QUARTILE' = 'QUARTILE({1;2;3;4};2)'
    'RANDBETWEEN' = 'RANDBETWEEN(1;1)'
    'RANK' = 'RANK(2;{1;2;3};0)'
    'RATE' = 'RATE(12;-100;1000;0;0;0.01)'
    'RECEIVED' = 'RECEIVED(DATE(2020;1;1);DATE(2020;7;1);97;0.04;0)'
    'REPLACE' = 'REPLACE("abcd";2;2;"XY")'
    'REPLACEB' = 'REPLACEB("abcd";2;2;"XY")'
    'RIGHT' = 'RIGHT("abc";2)'
    'RIGHTB' = 'RIGHTB("abc";2)'
    'ROMAN' = 'ROMAN(14;0)'
    'ROW' = 'ROW([Data.B2])'
    'ROWS' = 'ROWS([Data.A2:.B4])'
    'SEARCH' = 'SEARCH("b";"abc";1)'
    'SEARCHB' = 'SEARCHB("b";"abc";1)'
    'SERIESSUM' = 'SERIESSUM(2;1;1;{1;2;3})'
    'SHEET' = 'SHEET([Data.A1])'
    'SHEETS' = 'SHEETS([Data.A1])'
    'SKEW' = 'SKEW(1;2;3;4;5)'
    'SKEWP' = 'SKEWP(1;2;3;4;5)'
    'SLOPE' = 'SLOPE({2;4;6};{1;2;3})'
    'SMALL' = 'SMALL({1;2;3};2)'
    'STANDARDIZE' = 'STANDARDIZE(2;1;0.5)'
    'STDEVA' = 'STDEVA(1;2;3)'
    'STEYX' = 'STEYX({2;4;5};{1;2;3})'
    'SUBSTITUTE' = 'SUBSTITUTE("abab";"a";"x";2)'
    'SUBTOTAL' = 'SUBTOTAL(9;{1;2;3})'
    'SUMIF' = 'SUMIF([Data.B2:.B4];">1";[Data.B2:.B4])'
    'SUMIFS' = 'SUMIFS([Data.B2:.B4];[Data.B2:.B4];">1")'
    'SUMPRODUCT' = 'SUMPRODUCT({1;2;3};{4;5;6})'
    'SUMX2MY2' = 'SUMX2MY2({1;2};{3;4})'
    'SUMX2PY2' = 'SUMX2PY2({1;2};{3;4})'
    'SUMXMY2' = 'SUMXMY2({1;2};{3;4})'
    'TBILLEQ' = 'TBILLEQ(DATE(2008;3;31);DATE(2008;6;1);0.0914)'
    'TBILLPRICE' = 'TBILLPRICE(DATE(2008;3;31);DATE(2008;6;1);0.09)'
    'TBILLYIELD' = 'TBILLYIELD(DATE(2008;3;31);DATE(2008;6;1);98.45)'
    'TEXT' = 'TEXT(1234.5;"0.0")'
    'TIME' = 'TIME(12;30;0)'
    'TIMEVALUE' = 'TIMEVALUE("12:30:00")'
    'TINV' = 'TINV(0.5;10)'
    'TREND' = 'TREND({2;4;6};{1;2;3};{4};TRUE())'
    'TRIMMEAN' = 'TRIMMEAN({1;2;3;100};0.5)'
    'TTEST' = 'TTEST({1;2;3};{2;3;4};2;2)'
    'UNICHAR' = 'UNICHAR(65)'
    'UNICODE' = 'UNICODE("A")'
    'VALUE' = 'VALUE("42.5")'
    'VDB' = 'VDB(1000;100;5;0;1)'
    'VARA' = 'VARA(1;2;3)'
    'VLOOKUP' = 'VLOOKUP("Two";[Data.A2:.B4];2;FALSE())'
    'WEEKDAY' = 'WEEKDAY(DATE(2020;1;1);2)'
    'WEEKNUM' = 'WEEKNUM(DATE(2020;1;1);2)'
    'WEIBULL' = 'WEIBULL(1;2;3;TRUE())'
    'WORKDAY' = 'WORKDAY(DATE(2020;1;1);5)'
    'XIRR' = 'XIRR({-100;60;60};{43831;44013;44197};0.1)'
    'XNPV' = 'XNPV(0.1;{-100;60;60};{43831;44013;44197})'
    'YEARFRAC' = 'YEARFRAC(DATE(2020;1;1);DATE(2021;1;1);0)'
    'YIELD' = 'YIELD(DATE(2020;1;1);DATE(2025;1;1);0.05;95;100;2;0)'
    'YIELDDISC' = 'YIELDDISC(DATE(2020;1;1);DATE(2020;7;1);97;100;0)'
    'YIELDMAT' = 'YIELDMAT(DATE(2020;1;1);DATE(2021;1;1);DATE(2019;7;1);0.05;95;0)'
    'ZTEST' = 'ZTEST({1;2;3};2;1)'
}

$expectedOverrides = @{
    'GETPIVOTDATA' = [ordered]@{
        kind = 'predicate'
        value = 'workbook-pivot-contract'
        oracle = 'oasis-host-contract'
    }
    'INFO' = [ordered]@{
        kind = 'predicate'
        value = 'non-empty-host-text'
        oracle = 'oasis-host-contract'
    }
    'MULTIPLE.OPERATIONS' = [ordered]@{
        kind = 'predicate'
        value = 'workbook-data-table-contract'
        oracle = 'oasis-host-contract'
    }
    'NOW' = [ordered]@{
        kind = 'predicate'
        value = 'current-date-time'
        oracle = 'oasis-runtime-property'
    }
    'ODDFPRICE' = [ordered]@{
        kind = 'float'
        value = '113.597717474078'
        oracle = 'published-reference-example'
    }
    'ODDFYIELD' = [ordered]@{
        kind = 'float'
        value = '0.0625'
        oracle = 'published-reference-example'
    }
    'RAND' = [ordered]@{
        kind = 'predicate'
        value = 'number-zero-inclusive-one-exclusive'
        oracle = 'oasis-runtime-property'
    }
    'TODAY' = [ordered]@{
        kind = 'predicate'
        value = 'current-date'
        oracle = 'oasis-runtime-property'
    }
}

$cases = foreach ($function in $manifest.functions) {
    $specification = Get-FunctionSpecification -FunctionName $function.name
    if ($formulaOverrides.ContainsKey($function.name)) {
        $expression = $formulaOverrides[$function.name]
    }
    else {
        $types = Get-RequiredParameterTypes -FunctionName $function.name -Syntax $specification.syntax
        $arguments = @($types | ForEach-Object { Get-DefaultArgument -Type $_ })
        $expression = "$($function.name)($($arguments -join ';'))"
    }

    [ordered]@{
        function = $function.name
        section = $specification.section
        syntax = $specification.syntax
        formula = "of:=$expression"
        expected = $null
    }
}

$workRoot = Join-Path $repoRoot '.tmp/openformula-normative-oracle'
New-Item -ItemType Directory -Force -Path $workRoot | Out-Null
$fodsPath = Join-Path $workRoot 'openformula-normative-oracle.fods'
$odsPath = Join-Path $workRoot 'openformula-normative-oracle.ods'
$profilePath = (Join-Path $workRoot 'libreoffice-profile').Replace('\', '/')

$rows = foreach ($case in $cases) {
    $name = [System.Security.SecurityElement]::Escape($case.function)
    $formula = [System.Security.SecurityElement]::Escape($case.formula)
    "<table:table-row><table:table-cell office:value-type=`"string`"><text:p>$name</text:p></table:table-cell><table:table-cell table:formula=`"$formula`" office:value-type=`"float`" office:value=`"0`"><text:p>0</text:p></table:table-cell></table:table-row>"
}

$fods = @"
<?xml version="1.0" encoding="UTF-8"?>
<office:document xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:of="urn:oasis:names:tc:opendocument:xmlns:of:1.2" office:version="1.4" office:mimetype="application/vnd.oasis.opendocument.spreadsheet">
  <office:body>
    <office:spreadsheet>
      <table:table table:name="Data">
        <table:table-row><table:table-cell office:value-type="string"><text:p>Label</text:p></table:table-cell><table:table-cell office:value-type="string"><text:p>Value</text:p></table:table-cell><table:table-cell/><table:table-cell office:value-type="string"><text:p>Value</text:p></table:table-cell><table:table-cell office:value-type="string"><text:p>Value</text:p></table:table-cell></table:table-row>
        <table:table-row><table:table-cell office:value-type="string"><text:p>One</text:p></table:table-cell><table:table-cell office:value-type="float" office:value="1"><text:p>1</text:p></table:table-cell><table:table-cell/><table:table-cell office:value-type="string"><text:p>&gt;1</text:p></table:table-cell><table:table-cell office:value-type="string"><text:p>=2</text:p></table:table-cell></table:table-row>
        <table:table-row><table:table-cell office:value-type="string"><text:p>Two</text:p></table:table-cell><table:table-cell office:value-type="float" office:value="2"><text:p>2</text:p></table:table-cell><table:table-cell/></table:table-row>
        <table:table-row><table:table-cell office:value-type="string"><text:p>Three</text:p></table:table-cell><table:table-cell office:value-type="float" office:value="3"><text:p>3</text:p></table:table-cell><table:table-cell/></table:table-row>
        <table:table-row><table:table-cell table:number-columns-repeated="4"/></table:table-row>
        <table:table-row><table:table-cell office:value-type="float" office:value="1"><text:p>1</text:p></table:table-cell><table:table-cell office:value-type="float" office:value="2"><text:p>2</text:p></table:table-cell><table:table-cell office:value-type="float" office:value="3"><text:p>3</text:p></table:table-cell></table:table-row>
        <table:table-row><table:table-cell office:value-type="float" office:value="10"><text:p>10</text:p></table:table-cell><table:table-cell office:value-type="float" office:value="20"><text:p>20</text:p></table:table-cell><table:table-cell office:value-type="float" office:value="30"><text:p>30</text:p></table:table-cell></table:table-row>
      </table:table>
      <table:table table:name="Oracle">
        $($rows -join "`n        ")
      </table:table>
    </office:spreadsheet>
  </office:body>
</office:document>
"@

[System.IO.File]::WriteAllText($fodsPath, $fods, [System.Text.UTF8Encoding]::new($false))
if (Test-Path -LiteralPath $odsPath) {
    Remove-Item -LiteralPath $odsPath
}

$arguments = @(
    "-env:UserInstallation=file:///$profilePath",
    '--headless',
    '--convert-to', 'ods',
    '--outdir', $workRoot,
    $fodsPath
)
$process = Start-Process -FilePath $SofficePath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $odsPath -PathType Leaf)) {
    throw "LibreOffice oracle 計算失敗，exit code：$($process.ExitCode)"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($odsPath)
try {
    $entry = $archive.GetEntry('content.xml')
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try {
        [xml]$content = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
}
finally {
    $archive.Dispose()
}

$namespaceManager = [System.Xml.XmlNamespaceManager]::new($content.NameTable)
$namespaceManager.AddNamespace('table', 'urn:oasis:names:tc:opendocument:xmlns:table:1.0')
$namespaceManager.AddNamespace('office', 'urn:oasis:names:tc:opendocument:xmlns:office:1.0')
$oracleTable = $content.SelectSingleNode('//table:table[@table:name="Oracle"]', $namespaceManager)
$resultCells = @($oracleTable.SelectNodes('table:table-row/table:table-cell[2]', $namespaceManager))
if ($resultCells.Count -ne $cases.Count) {
    throw "LibreOffice oracle 筆數不符：預期 $($cases.Count)，實際 $($resultCells.Count)。"
}

for ($index = 0; $index -lt $cases.Count; $index++) {
    $cell = $resultCells[$index]
    $kind = $cell.GetAttribute('value-type', 'urn:oasis:names:tc:opendocument:xmlns:office:1.0')
    $value = switch ($kind) {
        'float' { $cell.GetAttribute('value', 'urn:oasis:names:tc:opendocument:xmlns:office:1.0') }
        'currency' { $cell.GetAttribute('value', 'urn:oasis:names:tc:opendocument:xmlns:office:1.0') }
        'percentage' { $cell.GetAttribute('value', 'urn:oasis:names:tc:opendocument:xmlns:office:1.0') }
        'boolean' { $cell.GetAttribute('boolean-value', 'urn:oasis:names:tc:opendocument:xmlns:office:1.0') }
        'date' { $cell.GetAttribute('date-value', 'urn:oasis:names:tc:opendocument:xmlns:office:1.0') }
        'time' { $cell.GetAttribute('time-value', 'urn:oasis:names:tc:opendocument:xmlns:office:1.0') }
        'string' {
            $stringValue = $cell.GetAttribute(
                'string-value',
                'urn:oasis:names:tc:opendocument:xmlns:office:1.0')
            if ($stringValue.Length -gt 0) {
                $stringValue
            }
            else {
                $cell.InnerText
            }
        }
        '' { '' }
        default { '' }
    }

    $cases[$index].expected = [ordered]@{
        kind = $kind
        value = $value
        oracle = 'libreoffice-calc'
    }
}

foreach ($case in $cases) {
    if ($expectedOverrides.ContainsKey($case.function)) {
        $case.expected = $expectedOverrides[$case.function]
    }
}

$errorCases = @($cases | Where-Object { $_.expected.kind -eq 'string' -and $_.expected.value -like '#*' })
$allowedErrorFunctions = @('DDE', 'FORMULA', 'GETPIVOTDATA', 'MULTIPLE.OPERATIONS', 'NA')
$unexpectedErrors = @($errorCases | Where-Object { $_.function -notin $allowedErrorFunctions })
if ($unexpectedErrors.Count -gt 0) {
    $summary = $unexpectedErrors | ForEach-Object { "$($_.function)=$($_.expected.value)" }
    throw "正常語意案例出現未預期錯誤：$($summary -join ', ')"
}

$version = (& $SofficePath --version | Select-Object -First 1).Trim()
$corpus = [ordered]@{
    schemaVersion = 1
    profile = 'OdfKit Safe Large'
    normativeSource = [ordered]@{
        title = 'OpenDocument v1.4 Part 4 OpenFormula'
        uri = $specUri
        sha256 = $specSha256
    }
    independentOracle = [ordered]@{
        product = 'LibreOffice Calc'
        version = $version
    }
    requiredFunctionCount = $cases.Count
    cases = @($cases)
}

$json = $corpus | ConvertTo-Json -Depth 8
$json = $json -replace "`r?`n", "`r`n"
$json += "`r`n"

if ($VerifyOnly) {
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        throw "缺少 OpenFormula normative corpus：$outputPath"
    }

    $current = [System.IO.File]::ReadAllText($outputPath)
    if ($current -ne $json) {
        throw 'OpenFormula normative corpus 已漂移，請重新執行產生器。'
    }

    Write-Host "PASS：OpenFormula normative corpus 與 OASIS 規範及 LibreOffice oracle 一致。"
    exit 0
}

[System.IO.File]::WriteAllText(
    $outputPath,
    $json,
    [System.Text.UTF8Encoding]::new($false))
Write-Host "WROTE：$outputPath（$($cases.Count) normative cases）"
