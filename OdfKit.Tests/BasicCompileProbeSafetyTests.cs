using System.Reflection;
using OdfKit.Extensions.Scripting;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：LibreOffice Basic 診斷會在最低巨集安全等級下呼叫模組入口，
/// 因此產生的探測模組不得讓來源的陳述式在入口被執行，含模組層級程式碼時也不得啟動探測。
/// 這些方法為內部實作，透過反射驗證。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class BasicCompileProbeSafetyTests
{
    private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;

    private static (string Module, string EntryPoint) Prepare(string source)
    {
        MethodInfo method = typeof(OdfExternalScriptCompiler).GetMethod("PrepareBasicCompileModule", Flags)!;
        object?[] args = [source, null];
        string module = (string)method.Invoke(null, args)!;
        return (module, (string)args[1]!);
    }

    private static bool HasModuleLevelCode(string source)
    {
        MethodInfo method = typeof(OdfExternalScriptCompiler).GetMethod("HasModuleLevelExecutableCode", Flags)!;
        return (bool)method.Invoke(null, [source])!;
    }

    [Theory]
    [InlineData("Sub Main\nShell(\"calc\")\nEnd Sub")]
    [InlineData("Sub Main\nEnd If\nShell(\"calc\")\nIf True Then\nEnd Sub")]
    [InlineData("Function Run\nEnd If\nShell(\"calc\")\nIf True Then\nEnd Function")]
    public void EntryPoint_ReturnsImmediately_AndCannotBeEscapedByCraftedSource(string source)
    {
        (string module, string entry) = Prepare(source);
        string[] lines = module.Replace("\r\n", "\n").Split('\n');

        int declaration = System.Array.FindIndex(
            lines,
            line => line.TrimStart().StartsWith("Sub " + entry, System.StringComparison.OrdinalIgnoreCase) ||
                    line.TrimStart().StartsWith("Function " + entry, System.StringComparison.OrdinalIgnoreCase));
        Assert.True(declaration >= 0, "找不到入口宣告。");

        string kind = lines[declaration].TrimStart().StartsWith("Sub", System.StringComparison.OrdinalIgnoreCase) ? "Sub" : "Function";
        Assert.Equal("Exit " + kind, lines[declaration + 1].Trim());
        Assert.DoesNotContain("If False Then", module);
    }

    [Theory]
    [InlineData("Shell(\"calc\")\nSub Main\nEnd Sub")]
    [InlineData("x = Shell(\"calc\")")]
    [InlineData("Dim a = Shell(\"calc\")")]
    [InlineData("Dim a : a = Shell(\"calc\")")]
    [InlineData("Dim a _\n= Shell(\"calc\")")]
    [InlineData("Const C = Shell(\"calc\")")]
    [InlineData("Sub Main\nEnd Sub\nShell(\"calc\")")]
    [InlineData("Sub Main")]
    [InlineData("Dim a(Shell(\"calc\")) As Integer")]
    [InlineData("Private a(Shell(\"calc\")) As Integer")]
    [InlineData("Public a(1 To Shell(\"calc\")) As Integer")]
    [InlineData("Dim a(MaxN) As Integer")]
    [InlineData("Dim a(10")]
    public void ModuleLevelExecutableCode_IsDetected(string source)
    {
        Assert.True(HasModuleLevelCode(source));
    }

    [Theory]
    [InlineData("")]
    [InlineData("REM comment\n' another\nOption Explicit")]
    [InlineData("Option Explicit\nDim counter As Integer\nPrivate name As String\nConst Limit = 10")]
    [InlineData("Declare Function GetTick Lib \"kernel32\" () As Long")]
    [InlineData("Sub Main\nShell(\"calc\")\nEnd Sub")]
    [InlineData("Public Function Add(a As Integer, b As Integer) As Integer\nAdd = a + b\nEnd Function")]
    [InlineData("Private Sub Helper\nEnd Sub\n\nSub Main\nHelper\nEnd Sub")]
    [InlineData("Dim a(1 To 10) As Integer")]
    [InlineData("Dim grid(10, 5) As Double")]
    [InlineData("Public buffer(0 To 255) As Byte")]
    [InlineData("Dim names(3) As String")]
    public void DeclarationsCommentsAndProcedureBodies_AreNotModuleLevelCode(string source)
    {
        Assert.False(HasModuleLevelCode(source));
    }
}
