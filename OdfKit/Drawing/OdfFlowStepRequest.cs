using OdfKit.Presentation;

namespace OdfKit.Drawing;

/// <summary>
/// Describes a flow-diagram step for high-level drawing builders.
/// 描述高階繪圖 builder 使用的流程圖步驟。
/// </summary>
/// <param name="Id">The shape identifier. / 圖形識別碼。</param>
/// <param name="Text">The displayed step text. / 顯示的步驟文字。</param>
/// <param name="ShapeType">The step shape type. / 步驟圖形類型。</param>
public sealed record OdfFlowStepRequest(
    string Id,
    string Text,
    OdfShapeType ShapeType = OdfShapeType.Rectangle);
