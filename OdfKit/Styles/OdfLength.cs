using System;
using System.Globalization;

namespace OdfKit.Styles
{
    public enum OdfUnit
    {
        Unspecified,
        Centimeters,
        Millimeters,
        Inches,
        Points,
        Picas,
        Pixels,
        Percentage,
        Em
    }

    public struct OdfLength : IEquatable<OdfLength>
    {
        public double Value { get; }
        public OdfUnit Unit { get; }

        public OdfLength(double value, OdfUnit unit)
        {
            Value = value;
            Unit = unit;
        }

        #region Factory Methods

        public static OdfLength FromCentimeters(double val) => new OdfLength(val, OdfUnit.Centimeters);
        public static OdfLength FromMillimeters(double val) => new OdfLength(val, OdfUnit.Millimeters);
        public static OdfLength FromInches(double val) => new OdfLength(val, OdfUnit.Inches);
        public static OdfLength FromPoints(double val) => new OdfLength(val, OdfUnit.Points);
        public static OdfLength FromPixels(double val) => new OdfLength(val, OdfUnit.Pixels);
        public static OdfLength FromPercentage(double val) => new OdfLength(val, OdfUnit.Percentage);
        public static OdfLength FromEm(double val) => new OdfLength(val, OdfUnit.Em);

        #endregion

        #region Conversion & Parsing

        public static OdfLength Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new OdfLength(0, OdfUnit.Unspecified);
            }

            text = text!.Trim();

            // Extract numeric part and suffix
            int suffixIndex = text.Length;
            while (suffixIndex > 0 && !char.IsDigit(text[suffixIndex - 1]) && text[suffixIndex - 1] != '.' && text[suffixIndex - 1] != '-')
            {
                suffixIndex--;
            }

            string numPart = text.Substring(0, suffixIndex).Trim();
            string unitPart = text.Substring(suffixIndex).Trim().ToLowerInvariant();

            if (!double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                throw new FormatException($"Invalid numeric format in length: '{text}'");
            }

            OdfUnit unit = unitPart switch
            {
                "cm" => OdfUnit.Centimeters,
                "mm" => OdfUnit.Millimeters,
                "in" => OdfUnit.Inches,
                "pt" => OdfUnit.Points,
                "pc" => OdfUnit.Picas,
                "px" => OdfUnit.Pixels,
                "%" => OdfUnit.Percentage,
                "em" => OdfUnit.Em,
                "" => OdfUnit.Unspecified,
                _ => throw new FormatException($"Unsupported unit '{unitPart}' in length: '{text}'")
            };

            return new OdfLength(val, unit);
        }

        public static bool TryParse(string? text, out OdfLength result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    result = new OdfLength(0, OdfUnit.Unspecified);
                    return false;
                }
                result = Parse(text);
                return true;
            }
            catch
            {
                result = new OdfLength(0, OdfUnit.Unspecified);
                return false;
            }
        }

        public double ToCentimeters() => ConvertTo(OdfUnit.Centimeters);
        public double ToPoints() => ConvertTo(OdfUnit.Points);
        public double ToInches() => ConvertTo(OdfUnit.Inches);
        public double ToMillimeters() => ConvertTo(OdfUnit.Millimeters);

        public double ConvertTo(OdfUnit targetUnit)
        {
            if (Unit == targetUnit) return Value;
            if (Unit == OdfUnit.Unspecified || targetUnit == OdfUnit.Unspecified)
            {
                return Value; // Treat as 1:1 if unspecified
            }

            if (Unit == OdfUnit.Percentage || Unit == OdfUnit.Em || targetUnit == OdfUnit.Percentage || targetUnit == OdfUnit.Em)
            {
                throw new InvalidOperationException($"Cannot convert relative unit '{Unit}' to absolute unit '{targetUnit}' directly.");
            }

            // Convert current unit to points (72 points = 1 inch)
            double points = Unit switch
            {
                OdfUnit.Points => Value,
                OdfUnit.Centimeters => Value * 28.3464567,
                OdfUnit.Millimeters => Value * 2.83464567,
                OdfUnit.Inches => Value * 72.0,
                OdfUnit.Picas => Value * 12.0,
                OdfUnit.Pixels => Value * (72.0 / 96.0), // Assuming standard 96 DPI
                _ => throw new NotSupportedException($"Conversion from unit {Unit} is not supported.")
            };

            // Convert points to target unit
            return targetUnit switch
            {
                OdfUnit.Points => points,
                OdfUnit.Centimeters => points / 28.3464567,
                OdfUnit.Millimeters => points / 2.83464567,
                OdfUnit.Inches => points / 72.0,
                OdfUnit.Picas => points / 12.0,
                OdfUnit.Pixels => points / (72.0 / 96.0),
                _ => throw new NotSupportedException($"Conversion to unit {targetUnit} is not supported.")
            };
        }

        /// <summary>
        /// 如果是 Unspecified 則套用 context 預設的單位進行轉譯。
        /// </summary>
        public OdfLength FallbackTo(OdfUnit defaultUnit)
        {
            if (Unit == OdfUnit.Unspecified)
            {
                return new OdfLength(Value, defaultUnit);
            }
            return this;
        }

        public override string ToString()
        {
            string unitStr = Unit switch
            {
                OdfUnit.Centimeters => "cm",
                OdfUnit.Millimeters => "mm",
                OdfUnit.Inches => "in",
                OdfUnit.Points => "pt",
                OdfUnit.Picas => "pc",
                OdfUnit.Pixels => "px",
                OdfUnit.Percentage => "%",
                OdfUnit.Em => "em",
                _ => ""
            };
            return Value.ToString(CultureInfo.InvariantCulture) + unitStr;
        }

        #endregion

        #region Implicit/Explicit Operators

        public static implicit operator OdfLength(string text) => Parse(text);
        public static implicit operator string(OdfLength len) => len.ToString();
        public static explicit operator double(OdfLength len) => len.Value;

        #endregion

        #region Equality

        public bool Equals(OdfLength other)
        {
            if (Unit == other.Unit)
            {
                return Math.Abs(Value - other.Value) < 1e-9;
            }
            try
            {
                double converted = other.ConvertTo(Unit);
                return Math.Abs(Value - converted) < 1e-9;
            }
            catch
            {
                return false;
            }
        }

        public override bool Equals(object? obj) => obj is OdfLength other && Equals(other);

        public override int GetHashCode()
        {
            // Normalize to points for consistent hashing if possible
            try
            {
                if (Unit != OdfUnit.Percentage && Unit != OdfUnit.Em && Unit != OdfUnit.Unspecified)
                {
                    double roundedPoints = Math.Round(ToPoints(), 4);
                    return roundedPoints.GetHashCode() ^ OdfUnit.Points.GetHashCode();
                }
            }
            catch { }
            return Math.Round(Value, 4).GetHashCode() ^ Unit.GetHashCode();
        }

        public static bool operator ==(OdfLength left, OdfLength right) => left.Equals(right);
        public static bool operator !=(OdfLength left, OdfLength right) => !left.Equals(right);

        #endregion
    }
}
