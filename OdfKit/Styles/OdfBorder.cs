using System;
using System.Drawing;
using OdfKit.Styles;

namespace OdfKit.Styles
{
    public readonly struct OdfBorder : IEquatable<OdfBorder>
    {
        public enum BorderStyle { None, Solid, Double, Dotted, Dashed }
        
        public BorderStyle Style { get; }
        public OdfLength Width { get; }
        public Color Color { get; }

        public OdfBorder(BorderStyle style, OdfLength width, Color color)
        {
            Style = style;
            Width = width;
            Color = color;
        }

        public static OdfBorder None => new OdfBorder(BorderStyle.None, new OdfLength(0, OdfUnit.Unspecified), Color.Empty);

        public static OdfBorder Parse(string borderString)
        {
            if (string.IsNullOrWhiteSpace(borderString) || borderString == "none")
                return None;

            string[] parts = borderString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            OdfLength width = new OdfLength(0, OdfUnit.Unspecified);
            BorderStyle style = BorderStyle.Solid;
            Color color = Color.Black;

            foreach (var part in parts)
            {
                if (part.StartsWith("#"))
                {
                    try
                    {
                        if (part.Length == 7)
                        {
                            int r = Convert.ToInt32(part.Substring(1, 2), 16);
                            int g = Convert.ToInt32(part.Substring(3, 2), 16);
                            int b = Convert.ToInt32(part.Substring(5, 2), 16);
                            color = Color.FromArgb(r, g, b);
                        }
                    }
                    catch
                    {
                        color = Color.Black;
                    }
                }
                else if (Enum.TryParse<BorderStyle>(part, true, out var parsedStyle))
                {
                    style = parsedStyle;
                }
                else if (OdfLength.TryParse(part, out var parsedLength))
                {
                    width = parsedLength;
                }
            }
            return new OdfBorder(style, width, color);
        }

        public override string ToString()
        {
            if (Style == BorderStyle.None) return "none";
            string hexColor = $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
            return $"{Width.ToString()} {Style.ToString().ToLowerInvariant()} {hexColor}";
        }

        public bool Equals(OdfBorder other) => Style == other.Style && Width.Equals(other.Width) && Color.ToArgb() == other.Color.ToArgb();
        public override bool Equals(object? obj) => obj is OdfBorder other && Equals(other);
        
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + Style.GetHashCode();
            hash = hash * 23 + Width.GetHashCode();
            hash = hash * 23 + Color.ToArgb().GetHashCode();
            return hash;
        }

        public static bool operator ==(OdfBorder left, OdfBorder right) => left.Equals(right);
        public static bool operator !=(OdfBorder left, OdfBorder right) => !left.Equals(right);
    }
}
