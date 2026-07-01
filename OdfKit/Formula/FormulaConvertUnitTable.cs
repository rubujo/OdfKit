using System;
using System.Collections.Generic;

namespace OdfKit.Formula;

/// <summary>
/// Unit conversion tables backing the OpenFormula <c>CONVERT</c> function (ODF 1.4 §6.16.18, Table 24-26).
/// 支援 OpenFormula <c>CONVERT</c> 函式（ODF 1.4 §6.16.18，Table 24-26）的單位換算表。
/// </summary>
internal static class FormulaConvertUnitTable
{
    private const double Foot = 0.3048;
    private const double Inch = 0.0254;
    private const double Yard = 0.9144;
    private const double Mile = 1609.344;
    private const double NauticalMile = 1852;
    private const double LbmGram = 453.59237;
    private const double StandardGravity = 9.80665;
    private const double LightYear = 299792458.0 * 3600 * 24 * 365.25;
    private const double UsGallon = 3.785411784e-3;
    private const double UsFluidOunce = 2.95735295625e-5;

    /// <summary>
    /// Records a unit's conversion group and its linear factor to the group's SI base unit.
    /// 記錄單位所屬換算群組與其相對於群組 SI 基準單位的線性係數。
    /// </summary>
    private readonly record struct UnitInfo(string Group, double ToBase, bool AllowsDecimalPrefix, bool AllowsBinaryPrefix);

    private static readonly Dictionary<string, UnitInfo> Units = BuildUnits();

    private static readonly Dictionary<string, double> DecimalPrefixes = new(StringComparer.Ordinal)
    {
        ["Y"] = 1e24,
        ["Z"] = 1e21,
        ["E"] = 1e18,
        ["P"] = 1e15,
        ["T"] = 1e12,
        ["G"] = 1e9,
        ["M"] = 1e6,
        ["k"] = 1e3,
        ["h"] = 1e2,
        ["da"] = 1e1,
        ["e"] = 1e1,
        ["d"] = 1e-1,
        ["c"] = 1e-2,
        ["m"] = 1e-3,
        ["u"] = 1e-6,
        ["n"] = 1e-9,
        ["p"] = 1e-12,
        ["f"] = 1e-15,
        ["a"] = 1e-18,
        ["z"] = 1e-21,
        ["y"] = 1e-24,
    };

    private static readonly Dictionary<string, double> BinaryPrefixes = new(StringComparer.Ordinal)
    {
        ["Yi"] = Math.Pow(2, 80),
        ["Zi"] = Math.Pow(2, 70),
        ["Ei"] = Math.Pow(2, 60),
        ["Pi"] = Math.Pow(2, 50),
        ["Ti"] = Math.Pow(2, 40),
        ["Gi"] = Math.Pow(2, 30),
        ["Mi"] = Math.Pow(2, 20),
        ["Ki"] = Math.Pow(2, 10),
    };

    private static Dictionary<string, UnitInfo> BuildUnits()
    {
        var units = new Dictionary<string, UnitInfo>(StringComparer.Ordinal);

        void Add(string group, double toBase, bool decimalPrefix, params string[] names)
        {
            foreach (string name in names)
                units[name] = new UnitInfo(group, toBase, decimalPrefix, false);
        }

        void AddBinary(string group, double toBase, params string[] names)
        {
            foreach (string name in names)
                units[name] = new UnitInfo(group, toBase, false, true);
        }

        // Area (base m^2)
        Add("Area", 4046.8564224, false, "uk_acre");
        Add("Area", 4046.0 + 13525426.0 / 15499969.0, false, "us_acre");
        Add("Area", 1e-20, true, "ang2", "ang^2");
        Add("Area", 100, true, "ar");
        Add("Area", Foot * Foot, false, "ft2", "ft^2");
        Add("Area", 10000, false, "ha");
        Add("Area", Inch * Inch, false, "in2", "in^2");
        Add("Area", LightYear * LightYear, true, "ly2", "ly^2");
        Add("Area", 1, true, "m2", "m^2");
        Add("Area", 2500, false, "Morgen");
        Add("Area", Mile * Mile, false, "mi2", "mi^2");
        Add("Area", NauticalMile * NauticalMile, false, "Nmi2", "Nmi^2");
        Add("Area", Math.Pow(Inch / 72, 2), false, "Pica2", "Pica^2", "picapt2", "picapt^2");
        Add("Area", Math.Pow(Inch / 6, 2), false, "pica2", "pica^2");
        Add("Area", Yard * Yard, false, "yd2", "yd^2");

        // Distance (base m)
        Add("Distance", 1e-10, true, "ang");
        Add("Distance", 45 * Inch, false, "ell");
        Add("Distance", Foot, false, "ft");
        Add("Distance", Inch, false, "in");
        Add("Distance", LightYear, true, "ly");
        Add("Distance", 1, true, "m");
        Add("Distance", Mile, false, "mi");
        Add("Distance", NauticalMile, false, "Nmi");
        Add("Distance", 149597870691.0 / Math.Tan((1.0 / 3600.0) * Math.PI / 180.0), true, "parsec", "pc");
        Add("Distance", Inch / 72, false, "Pica", "picapt");
        Add("Distance", Inch / 6, false, "pica");
        Add("Distance", 6336000.0 / 3937.0, false, "survey_mi");
        Add("Distance", Yard, false, "yd");

        // Energy (base J)
        Add("Energy", 1055.05585262, false, "BTU", "btu");
        Add("Energy", 4.184, true, "c");
        Add("Energy", 4.1868, true, "cal");
        Add("Energy", 1e-7, true, "e");
        Add("Energy", 1.602176634e-19, true, "eV", "ev");
        Add("Energy", Foot * LbmGram / 1000 * StandardGravity, false, "flb");
        Add("Energy", 550 * Foot * LbmGram / 1000 * StandardGravity * 3600, false, "HPh", "hh");
        Add("Energy", 1, true, "J");
        Add("Energy", 3600, true, "Wh", "wh");

        // Force (base N)
        Add("Force", 1e-5, true, "dyn", "dy");
        Add("Force", 1, true, "N");
        Add("Force", LbmGram / 1000 * StandardGravity, false, "lbf");
        Add("Force", 9.80665e-3, true, "pond");

        // Information (base bit)
        AddBinary("Information", 1, "bit");
        AddBinary("Information", 8, "byte");

        // Magnetic flux density (base T)
        Add("MagneticFluxDensity", 1e-4, true, "ga");
        Add("MagneticFluxDensity", 1, true, "T");

        // Mass (base g)
        Add("Mass", 1, true, "g");
        Add("Mass", LbmGram / 7000, false, "grain");
        Add("Mass", 100 * LbmGram, false, "cwt", "shweight");
        Add("Mass", 112 * LbmGram, false, "uk_cwt", "lcwt", "hweight");
        Add("Mass", LbmGram, false, "lbm");
        Add("Mass", 14 * LbmGram, false, "stone");
        Add("Mass", 2000 * LbmGram, false, "ton");
        Add("Mass", LbmGram / 16, false, "ozm");
        Add("Mass", 32.174 * LbmGram, false, "sg");
        Add("Mass", 1.66053906660e-24, true, "u");
        Add("Mass", 2240 * LbmGram, false, "uk_ton", "LTON", "brton");

        // Power (base W)
        Add("Power", 550 * Foot * LbmGram / 1000 * StandardGravity, false, "HP", "h");
        Add("Power", 735.49875, false, "PS");
        Add("Power", 1, true, "W", "w");

        // Pressure (base Pa). "at" is deprecated and historically ambiguous between standard and
        // technical atmosphere; this maps it to the technical atmosphere ("SI_at") value.
        // "at" 已淘汰且歷史上在標準大氣壓與技術大氣壓間定義不一，此處對應至技術大氣壓（"SI_at"）數值。
        Add("Pressure", 9.8066510e4, true, "at");
        Add("Pressure", 1.0132510e5, true, "atm");
        Add("Pressure", 133.322387415, true, "mmHg");
        Add("Pressure", 1, true, "Pa");
        Add("Pressure", (LbmGram / 1000 * StandardGravity) / (Inch * Inch), false, "psi");
        Add("Pressure", 9.8066510e4, true, "SI_at");
        Add("Pressure", 101325.0 / 760.0, false, "Torr");

        // Speed (base m/s)
        Add("Speed", 6080 * Foot / 3600, false, "admkn");
        Add("Speed", NauticalMile / 3600, false, "kn");
        Add("Speed", 1.0 / 3600, true, "m/h", "m/hr");
        Add("Speed", 1, true, "m/s", "m/sec");
        Add("Speed", Mile / 3600, false, "mph");

        // Time (base s)
        Add("Time", 86400, false, "day", "d");
        Add("Time", 3600, false, "hr");
        Add("Time", 60, false, "mn", "min");
        Add("Time", 1, true, "sec", "s");
        Add("Time", 365.25 * 86400, false, "yr");

        // Volume (base m^3)
        Add("Volume", 1e-30, true, "ang3", "ang^3");
        Add("Volume", 42 * UsGallon, false, "barrel");
        Add("Volume", 0.03523907016688, false, "bushel");
        Add("Volume", 8 * UsFluidOunce, false, "cup");
        Add("Volume", Foot * Foot * Foot, false, "ft3", "ft^3");
        Add("Volume", UsGallon, false, "gal");
        Add("Volume", 100 * Foot * Foot * Foot, false, "GRT", "regton");
        Add("Volume", Inch * Inch * Inch, false, "in3", "in^3");
        Add("Volume", 1e-3, true, "l", "L", "lt");
        Add("Volume", LightYear * LightYear * LightYear, false, "ly3", "ly^3");
        Add("Volume", 1, true, "m3", "m^3");
        Add("Volume", Mile * Mile * Mile, false, "mi3", "mi^3");
        Add("Volume", 40 * Foot * Foot * Foot, false, "MTON");
        Add("Volume", NauticalMile * NauticalMile * NauticalMile, false, "Nmi3", "Nmi^3");
        Add("Volume", UsFluidOunce, false, "oz");
        Add("Volume", Math.Pow(Inch / 72, 3), false, "Pica3", "Pica^3", "picapt3", "picapt^3");
        Add("Volume", Math.Pow(Inch / 6, 3), false, "pica3", "pica^3");
        Add("Volume", UsGallon / 8, false, "pt", "us_pt");
        Add("Volume", 0.946352946e-3, false, "qt");
        Add("Volume", 0.5 * UsFluidOunce, false, "tbs");
        Add("Volume", UsFluidOunce / 6, false, "tsp");
        Add("Volume", 5e-6, false, "tspm");
        Add("Volume", 4.54609e-3, false, "uk_gal");
        Add("Volume", 4.54609e-3 / 8, false, "uk_pt");
        Add("Volume", 4.54609e-3 / 4, false, "uk_qt");
        Add("Volume", Yard * Yard * Yard, false, "yd3", "yd^3");

        return units;
    }

    private const string TemperatureGroup = "Temperature";

    private static readonly HashSet<string> TemperatureUnits = new(StringComparer.Ordinal)
    {
        "C", "cel", "F", "fah", "K", "kel", "Rank", "Reau",
    };

    /// <summary>
    /// Attempts to convert a number between two CONVERT unit symbols, applying decimal or binary prefixes where permitted.
    /// 嘗試在兩個 CONVERT 單位符號之間換算數值，並依規則套用十進位或二進位字首。
    /// </summary>
    /// <param name="value">The number to convert. / 要換算的數值。</param>
    /// <param name="fromUnit">The source unit symbol. / 來源單位符號。</param>
    /// <param name="toUnit">The target unit symbol. / 目標單位符號。</param>
    /// <param name="result">The converted value when successful. / 換算成功時的結果值。</param>
    /// <returns><see langword="true"/> if both units are recognized and belong to the same group. / 若兩個單位皆可辨識且屬於同一群組則為 <see langword="true"/>。</returns>
    internal static bool TryConvert(double value, string fromUnit, string toUnit, out double result)
    {
        result = 0;

        if (TemperatureUnits.Contains(fromUnit) && TemperatureUnits.Contains(toUnit))
        {
            if (!TryToCelsius(fromUnit, value, out double celsius))
                return false;
            return TryFromCelsius(toUnit, celsius, out result);
        }

        if (!TryResolveUnit(fromUnit, out double fromFactor, out string fromGroup))
            return false;
        if (!TryResolveUnit(toUnit, out double toFactor, out string toGroup))
            return false;
        if (!string.Equals(fromGroup, toGroup, StringComparison.Ordinal))
            return false;

        result = value * fromFactor / toFactor;
        return true;
    }

    private static bool TryResolveUnit(string unit, out double factor, out string group)
    {
        factor = 0;
        group = "";

        if (Units.TryGetValue(unit, out UnitInfo exact))
        {
            factor = exact.ToBase;
            group = exact.Group;
            return true;
        }

        foreach ((string prefix, double prefixValue) in DecimalPrefixes)
        {
            if (unit.Length > prefix.Length && unit.StartsWith(prefix, StringComparison.Ordinal))
            {
                string baseName = unit.Substring(prefix.Length);
                if (Units.TryGetValue(baseName, out UnitInfo info) && info.AllowsDecimalPrefix)
                {
                    factor = info.ToBase * prefixValue;
                    group = info.Group;
                    return true;
                }
            }
        }

        foreach ((string prefix, double prefixValue) in BinaryPrefixes)
        {
            if (unit.Length > prefix.Length && unit.StartsWith(prefix, StringComparison.Ordinal))
            {
                string baseName = unit.Substring(prefix.Length);
                if (Units.TryGetValue(baseName, out UnitInfo info) && info.AllowsBinaryPrefix)
                {
                    factor = info.ToBase * prefixValue;
                    group = info.Group;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryToCelsius(string unit, double value, out double celsius)
    {
        switch (unit)
        {
            case "C":
            case "cel":
                celsius = value;
                return true;
            case "F":
            case "fah":
                celsius = (value - 32) * 5 / 9;
                return true;
            case "K":
            case "kel":
                celsius = value - 273.15;
                return true;
            case "Rank":
                celsius = (value - 491.67) * 5 / 9;
                return true;
            case "Reau":
                celsius = value * 5 / 4;
                return true;
            default:
                celsius = 0;
                return false;
        }
    }

    private static bool TryFromCelsius(string unit, double celsius, out double value)
    {
        switch (unit)
        {
            case "C":
            case "cel":
                value = celsius;
                return true;
            case "F":
            case "fah":
                value = celsius * 9 / 5 + 32;
                return true;
            case "K":
            case "kel":
                value = celsius + 273.15;
                return true;
            case "Rank":
                value = celsius * 9 / 5 + 491.67;
                return true;
            case "Reau":
                value = celsius * 4 / 5;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}
