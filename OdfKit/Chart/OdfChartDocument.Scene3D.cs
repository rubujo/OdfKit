using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Chart;

public partial class OdfChartDocument
{
    /// <summary>
    /// 取得 3D 圖表繪圖區中所有光源（<c>dr3d:light</c>）的摘要清單。
    /// </summary>
    /// <returns>光源摘要清單，依文件中出現順序排列。</returns>
    public IReadOnlyList<OdfChartLightInfo> GetLights()
    {
        List<OdfChartLightInfo> lights = [];
        foreach (OdfNode child in FindOrCreatePlotArea().Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "light" &&
                child.NamespaceUri == OdfNamespaces.Dr3d)
            {
                string direction = child.GetAttribute("direction", OdfNamespaces.Dr3d) ?? string.Empty;
                lights.Add(new OdfChartLightInfo(
                    direction,
                    child.GetAttribute("diffuse-color", OdfNamespaces.Dr3d),
                    ParseBoolean(child.GetAttribute("enabled", OdfNamespaces.Dr3d)),
                    ParseBoolean(child.GetAttribute("specular", OdfNamespaces.Dr3d))));
            }
        }

        return lights;
    }

    /// <summary>
    /// 新增一個 3D 圖表光源（<c>dr3d:light</c>）。
    /// </summary>
    /// <param name="direction">光源方向向量，格式為 <c>(x y z)</c>。</param>
    /// <param name="diffuseColor">選用的漫射色。</param>
    /// <param name="enabled">選用的啟用狀態。</param>
    /// <param name="specular">選用的反射光啟用狀態。</param>
    /// <exception cref="ArgumentException">當 <paramref name="direction"/> 為空白時擲出。</exception>
    public void AddLight(string direction, string? diffuseColor = null, bool? enabled = null, bool? specular = null)
    {
        if (string.IsNullOrWhiteSpace(direction))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfChartDocument_LightCannotBeEmpty"), nameof(direction));
        }

        OdfNode light = OdfNodeFactory.CreateElement("light", OdfNamespaces.Dr3d, "dr3d");
        light.SetAttribute("direction", OdfNamespaces.Dr3d, direction, "dr3d");
        if (!string.IsNullOrWhiteSpace(diffuseColor))
        {
            light.SetAttribute("diffuse-color", OdfNamespaces.Dr3d, diffuseColor!, "dr3d");
        }

        if (enabled is not null)
        {
            light.SetAttribute("enabled", OdfNamespaces.Dr3d, enabled.Value ? "true" : "false", "dr3d");
        }

        if (specular is not null)
        {
            light.SetAttribute("specular", OdfNamespaces.Dr3d, specular.Value ? "true" : "false", "dr3d");
        }

        FindOrCreatePlotArea().AppendChild(light);
    }

    /// <summary>
    /// 移除繪圖區中所有 3D 圖表光源。
    /// </summary>
    public void ClearLights()
    {
        OdfNode plotArea = FindOrCreatePlotArea();
        foreach (OdfNode child in new List<OdfNode>(plotArea.Children))
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "light" &&
                child.NamespaceUri == OdfNamespaces.Dr3d)
            {
                plotArea.RemoveChild(child);
            }
        }
    }

    private static bool? ParseBoolean(string? value) =>
        value switch
        {
            "true" => true,
            "false" => false,
            _ => null,
        };
}
