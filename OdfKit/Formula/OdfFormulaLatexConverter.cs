using System;
using System.Collections.Generic;
using System.Text;
using CSharpMath.Atom;
using CSharpMath.Structures;

using OdfKit.Compliance;
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
    /// <param name="latex">LaTeX 公式字串</param>
    /// <returns>標準 MathML XML 字串</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="latex"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="ArgumentException">當 LaTeX 公式語法錯誤時擲出</exception>
    public static string Convert(string latex)
    {
        if (latex == null)
        {
            throw new ArgumentNullException(nameof(latex));
        }

        var (mathList, error) = LaTeXParser.MathListFromLaTeX(latex);
        if (error != null)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfFormulaLatexConverter_LatexParsingFailed", error), nameof(latex));
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

    /// <summary>
    /// 將一組 <see cref="OdfMathToken"/> token 反向轉換為 LaTeX 公式字串（best-effort，
    /// 因 LaTeX 與 MathML 並非一對一對應，部分語意可能無法完整保留）。
    /// </summary>
    /// <param name="tokens">要轉換的 token 清單。</param>
    /// <returns>LaTeX 公式字串。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="tokens"/> 為 <see langword="null"/> 時擲出。</exception>
    public static string ToLatex(IReadOnlyList<OdfMathToken> tokens)
    {
        if (tokens is null)
        {
            throw new ArgumentNullException(nameof(tokens));
        }

        var sb = new StringBuilder();
        foreach (OdfMathToken token in tokens)
        {
            AppendLatexToken(sb, token);
        }

        return sb.ToString();
    }

    private static void AppendLatexToken(StringBuilder sb, OdfMathToken token)
    {
        string? mathVariant = token.Attributes is not null && token.Attributes.TryGetValue("mathvariant", out string? variant) ? variant : null;
        string prefix = mathVariant switch
        {
            "bold" => "\\mathbf{",
            "italic" => "\\mathit{",
            "bold-italic" => "\\boldsymbol{",
            _ => string.Empty,
        };
        if (prefix.Length > 0)
        {
            sb.Append(prefix);
        }

        switch (token.Kind)
        {
            case OdfMathTokenKind.Identifier:
            case OdfMathTokenKind.Number:
            case OdfMathTokenKind.Operator:
                sb.Append(token.Text);
                break;
            case OdfMathTokenKind.Text:
                sb.Append("\\text{").Append(token.Text).Append('}');
                break;
            case OdfMathTokenKind.Superscript:
                AppendLatexGroup(sb, token.Base);
                sb.Append('^');
                AppendLatexGroup(sb, token.Script);
                break;
            case OdfMathTokenKind.Subscript:
                AppendLatexGroup(sb, token.Base);
                sb.Append('_');
                AppendLatexGroup(sb, token.Script);
                break;
            case OdfMathTokenKind.Fraction:
                sb.Append("\\frac");
                AppendLatexGroup(sb, token.Base);
                AppendLatexGroup(sb, token.Script);
                break;
            case OdfMathTokenKind.Radical:
                if (token.Script is null)
                {
                    sb.Append("\\sqrt");
                    AppendLatexGroup(sb, token.Base);
                }
                else
                {
                    sb.Append("\\sqrt[");
                    AppendLatexToken(sb, token.Script);
                    sb.Append(']');
                    AppendLatexGroup(sb, token.Base);
                }
                break;
            case OdfMathTokenKind.Row:
                AppendLatexChildren(sb, token.Children);
                break;
            case OdfMathTokenKind.Matrix:
                sb.Append("\\begin{matrix}");
                if (token.Children is not null)
                {
                    for (int i = 0; i < token.Children.Count; i++)
                    {
                        IReadOnlyList<OdfMathToken> cells = token.Children[i].Children ?? Array.Empty<OdfMathToken>();
                        for (int j = 0; j < cells.Count; j++)
                        {
                            if (j > 0)
                            {
                                sb.Append(" & ");
                            }

                            AppendLatexToken(sb, cells[j]);
                        }

                        if (i < token.Children.Count - 1)
                        {
                            sb.Append(" \\\\ ");
                        }
                    }
                }

                sb.Append("\\end{matrix}");
                break;
            case OdfMathTokenKind.Under:
                sb.Append("\\underset");
                AppendLatexGroup(sb, token.Script);
                AppendLatexGroup(sb, token.Base);
                break;
            case OdfMathTokenKind.Over:
                sb.Append("\\overset");
                AppendLatexGroup(sb, token.Script);
                AppendLatexGroup(sb, token.Base);
                break;
            case OdfMathTokenKind.UnderOver:
                if (token.Children is { Count: 3 })
                {
                    sb.Append("\\overset{");
                    AppendLatexToken(sb, token.Children[2]);
                    sb.Append("}{\\underset{");
                    AppendLatexToken(sb, token.Children[1]);
                    sb.Append("}{");
                    AppendLatexToken(sb, token.Children[0]);
                    sb.Append("}}");
                }

                break;
            case OdfMathTokenKind.Fenced:
                string[] delimiters = token.Text.Split('|');
                sb.Append("\\left").Append(delimiters.Length > 0 ? delimiters[0] : "(");
                AppendLatexToken(sb, token.Base!);
                sb.Append("\\right").Append(delimiters.Length > 1 ? delimiters[1] : ")");
                break;
            case OdfMathTokenKind.Style:
                sb.Append(token.Text == "true" ? "\\displaystyle{" : "\\textstyle{");
                AppendLatexToken(sb, token.Base!);
                sb.Append('}');
                break;
            case OdfMathTokenKind.Apply:
                AppendLatexApply(sb, token);
                break;
        }

        if (prefix.Length > 0)
        {
            sb.Append('}');
        }
    }

    private static readonly Dictionary<string, string> ContentMathMlInfixOperators = new(StringComparer.Ordinal)
    {
        ["plus"] = " + ",
        ["minus"] = " - ",
        ["times"] = " \\cdot ",
        ["divide"] = " \\div ",
        ["eq"] = " = ",
        ["neq"] = " \\neq ",
        ["lt"] = " < ",
        ["gt"] = " > ",
        ["leq"] = " \\leq ",
        ["geq"] = " \\geq ",
    };

    private static void AppendLatexApply(StringBuilder sb, OdfMathToken token)
    {
        IReadOnlyList<OdfMathToken> operands = token.Children ?? Array.Empty<OdfMathToken>();

        if (token.Text == "power" && operands.Count == 2)
        {
            AppendLatexGroup(sb, operands[0]);
            sb.Append('^');
            AppendLatexGroup(sb, operands[1]);
            return;
        }

        if (token.Text == "root" && operands.Count == 1)
        {
            sb.Append("\\sqrt");
            AppendLatexGroup(sb, operands[0]);
            return;
        }

        if (ContentMathMlInfixOperators.TryGetValue(token.Text, out string? infix) && operands.Count > 0)
        {
            for (int i = 0; i < operands.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(infix);
                }

                AppendLatexToken(sb, operands[i]);
            }

            return;
        }

        sb.Append("\\operatorname{").Append(token.Text).Append("}(");
        for (int i = 0; i < operands.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            AppendLatexToken(sb, operands[i]);
        }

        sb.Append(')');
    }

    private static void AppendLatexGroup(StringBuilder sb, OdfMathToken? token)
    {
        sb.Append('{');
        if (token is not null)
        {
            AppendLatexToken(sb, token);
        }

        sb.Append('}');
    }

    private static void AppendLatexChildren(StringBuilder sb, IReadOnlyList<OdfMathToken>? children)
    {
        if (children is null)
        {
            return;
        }

        foreach (OdfMathToken child in children)
        {
            AppendLatexToken(sb, child);
        }
    }
}
