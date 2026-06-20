using System;
using System.Text;
using CSharpMath.Atom;
using CSharpMath.Structures;

namespace OdfKit.Formula;

/// <summary>
/// 提供將 LaTeX 公式字串轉譯為 MathML XML 結構的轉換器。
/// </summary>
public static class OdfFormulaLatexConverter
{
    private const string MathMlNamespace = "http://www.w3.org/1998/Math/MathML";

    /// <summary>
    /// 將 LaTeX 公式字串轉換為標準 MathML XML 字串。
    /// </summary>
    /// <param name="latex">LaTeX 公式字串。</param>
    /// <returns>標準 MathML XML 字串。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="latex"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">當 LaTeX 公式語法錯誤時擲出。</exception>
    public static string Convert(string latex)
    {
        if (latex == null)
        {
            throw new ArgumentNullException(nameof(latex));
        }

        var (mathList, error) = LaTeXParser.MathListFromLaTeX(latex);
        if (error != null)
        {
            throw new ArgumentException($"LaTeX 解析失敗：{error}", nameof(latex));
        }

        var sb = new StringBuilder();
        sb.Append($"<math xmlns=\"{MathMlNamespace}\">");

        if (mathList != null && mathList.Count > 0)
        {
            sb.Append("<mrow>");
            AppendMathList(sb, mathList);
            sb.Append("</mrow>");
        }

        sb.Append("</math>");
        return sb.ToString();
    }

    private static void AppendMathList(StringBuilder sb, MathList? mathList)
    {
        if (mathList == null)
            return;
        foreach (var atom in mathList)
        {
            AppendAtom(sb, atom);
        }
    }

    private static void AppendAtom(StringBuilder sb, MathAtom atom)
    {
        if (atom == null)
            return;

        bool hasSub = atom.Subscript != null && atom.Subscript.Count > 0;
        bool hasSup = atom.Superscript != null && atom.Superscript.Count > 0;

        if (hasSub || hasSup)
        {
            bool isLargeOp = atom is CSharpMath.Atom.Atoms.LargeOperator;
            bool useLimits = false;
            if (isLargeOp)
            {
                var largeOp = (CSharpMath.Atom.Atoms.LargeOperator)atom;
                bool isIntegral = atom.Nucleus == "∫";
                useLimits = !isIntegral && !largeOp.ForceNoLimits;
            }

            if (useLimits)
            {
                if (hasSub && hasSup)
                {
                    sb.Append("<munderover>");
                    AppendBaseAtom(sb, atom);
                    sb.Append("<mrow>");
                    AppendMathList(sb, atom.Subscript);
                    sb.Append("</mrow>");
                    sb.Append("<mrow>");
                    AppendMathList(sb, atom.Superscript);
                    sb.Append("</mrow>");
                    sb.Append("</munderover>");
                }
                else if (hasSub)
                {
                    sb.Append("<munder>");
                    AppendBaseAtom(sb, atom);
                    sb.Append("<mrow>");
                    AppendMathList(sb, atom.Subscript);
                    sb.Append("</mrow>");
                    sb.Append("</munder>");
                }
                else
                {
                    sb.Append("<mover>");
                    AppendBaseAtom(sb, atom);
                    sb.Append("<mrow>");
                    AppendMathList(sb, atom.Superscript);
                    sb.Append("</mrow>");
                    sb.Append("</mover>");
                }
            }
            else
            {
                if (hasSub && hasSup)
                {
                    sb.Append("<msubsup>");
                    AppendBaseAtom(sb, atom);
                    sb.Append("<mrow>");
                    AppendMathList(sb, atom.Subscript);
                    sb.Append("</mrow>");
                    sb.Append("<mrow>");
                    AppendMathList(sb, atom.Superscript);
                    sb.Append("</mrow>");
                    sb.Append("</msubsup>");
                }
                else if (hasSub)
                {
                    sb.Append("<msub>");
                    AppendBaseAtom(sb, atom);
                    sb.Append("<mrow>");
                    AppendMathList(sb, atom.Subscript);
                    sb.Append("</mrow>");
                    sb.Append("</msub>");
                }
                else
                {
                    sb.Append("<msup>");
                    AppendBaseAtom(sb, atom);
                    sb.Append("<mrow>");
                    AppendMathList(sb, atom.Superscript);
                    sb.Append("</mrow>");
                    sb.Append("</msup>");
                }
            }
        }
        else
        {
            AppendBaseAtom(sb, atom);
        }
    }

    private static void AppendBaseAtom(StringBuilder sb, MathAtom atom)
    {
        if (atom == null)
            return;

        var type = atom.GetType();

        if (atom is CSharpMath.Atom.Atoms.Fraction frac)
        {
            sb.Append("<mfrac>");
            sb.Append("<mrow>");
            AppendMathList(sb, frac.Numerator);
            sb.Append("</mrow>");
            sb.Append("<mrow>");
            AppendMathList(sb, frac.Denominator);
            sb.Append("</mrow>");
            sb.Append("</mfrac>");
        }
        else if (atom is CSharpMath.Atom.Atoms.Radical rad)
        {
            if (rad.Degree == null || rad.Degree.Count == 0)
            {
                sb.Append("<msqrt>");
                AppendMathList(sb, rad.Radicand);
                sb.Append("</msqrt>");
            }
            else
            {
                sb.Append("<mroot>");
                sb.Append("<mrow>");
                AppendMathList(sb, rad.Radicand);
                sb.Append("</mrow>");
                sb.Append("<mrow>");
                AppendMathList(sb, rad.Degree);
                sb.Append("</mrow>");
                sb.Append("</mroot>");
            }
        }
        else if (atom is CSharpMath.Atom.Atoms.Table table)
        {
            sb.Append("<mtable>");
            if (table.Cells != null)
            {
                foreach (var row in table.Cells)
                {
                    sb.Append("<mtr>");
                    foreach (var cell in row)
                    {
                        sb.Append("<mtd>");
                        sb.Append("<mrow>");
                        AppendMathList(sb, cell);
                        sb.Append("</mrow>");
                        sb.Append("</mtd>");
                    }
                    sb.Append("</mtr>");
                }
            }
            sb.Append("</mtable>");
        }
        else if (atom is CSharpMath.Atom.Atoms.Inner inner)
        {
            sb.Append("<mrow>");

            if (!string.IsNullOrEmpty(inner.LeftBoundary.Nucleus) && inner.LeftBoundary.Nucleus != ".")
            {
                sb.Append("<mo>");
                sb.Append(EscapeXml(inner.LeftBoundary.Nucleus));
                sb.Append("</mo>");
            }

            if (inner.InnerList != null)
            {
                AppendMathList(sb, inner.InnerList);
            }

            if (!string.IsNullOrEmpty(inner.RightBoundary.Nucleus) && inner.RightBoundary.Nucleus != ".")
            {
                sb.Append("<mo>");
                sb.Append(EscapeXml(inner.RightBoundary.Nucleus));
                sb.Append("</mo>");
            }

            sb.Append("</mrow>");
        }
        else if (type.Name == "Space")
        {
            sb.Append("<mspace width=\"0.167em\"/>");
        }
        else
        {
            string? nucleus = atom.Nucleus;
            if (nucleus == null || nucleus.Length == 0)
            {
                return;
            }

            string tag = "mi";

            if (atom is CSharpMath.Atom.Atoms.BinaryOperator ||
                atom is CSharpMath.Atom.Atoms.Relation ||
                atom is CSharpMath.Atom.Atoms.LargeOperator ||
                type.Name.Contains("Operator") ||
                type.Name.Contains("Relation") ||
                type.Name.Contains("Punctuation") ||
                nucleus == "=" || nucleus == "+" || nucleus == "-" || nucleus == "*" || nucleus == "/" || nucleus == "," || nucleus == "." || nucleus == ";" || nucleus == ":")
            {
                tag = "mo";
            }
            else if (IsNumber(nucleus))
            {
                tag = "mn";
            }

            sb.Append($"<{tag}>");
            sb.Append(EscapeXml(nucleus));
            sb.Append($"</{tag}>");
        }
    }

    private static bool IsNumber(string? s)
    {
        if (s == null || s.Length == 0)
            return false;
        foreach (char c in s)
        {
            if (!char.IsDigit(c) && c != '.')
                return false;
        }
        return true;
    }

    private static string EscapeXml(string? value)
    {
        if (value == null || value.Length == 0)
            return string.Empty;
        return System.Security.SecurityElement.Escape(value);
    }
}
