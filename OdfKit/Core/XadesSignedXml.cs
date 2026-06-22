using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace OdfKit.Core;

/// <summary>
/// 自訂的 <see cref="SignedXml"/> 子類別，手動尋找符合參考 URI ID 的專案，以繞過 .NET Core 中 GetElementById 的結構描述解析限制。
/// </summary>
internal sealed class XadesSignedXml : SignedXml
{
    /// <summary>
    /// 使用指定的 XML 文件初始化 <see cref="XadesSignedXml"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">用於初始化 <see cref="XadesSignedXml"/> 的 XML 文件</param>
    public XadesSignedXml(XmlDocument document) : base(document)
    {
    }

    /// <summary>
    /// 使用指定的 XML 元素初始化 <see cref="XadesSignedXml"/> 類別的新執行個體。
    /// </summary>
    /// <param name="element">用於初始化 <see cref="XadesSignedXml"/> 的 XML 元素</param>
    public XadesSignedXml(XmlElement element) : base(element)
    {
    }

    /// <summary>
    /// 傳回指定 XML 文件中具有指定 ID 的 XML 元素。
    /// </summary>
    /// <param name="document">要搜尋的 XML 文件</param>
    /// <param name="idValue">要尋找的 ID 值</param>
    /// <returns>若找到具有指定 ID 的元素，則為該元素；否則為 <see langword="null"/></returns>
    public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
    {
        if (document == null)
            return null;

        // 1. 搜尋簽章的 ObjectList
        foreach (var obj in this.m_signature.ObjectList)
        {
            if (obj is DataObject dataObj && dataObj.Data != null)
            {
                foreach (XmlNode node in dataObj.Data)
                {
                    var found = FindElementById(node, idValue);
                    if (found != null)
                        return found;
                }
            }
        }

        // 2. 在簽章元素樹（this.GetXml()）內搜尋
        // 為避免簽署期間呼叫 GetXml() 時拋出「A Reference must contain a DigestValue」例外，
        // 暫時為尚未具備 DigestValue 的 Reference 指派假雜湊值
        var tempReferences = new List<(Reference Ref, byte[]? OrigValue)>();
        if (this.SignedInfo != null)
        {
            foreach (Reference reference in this.SignedInfo.References)
            {
                if (reference.DigestValue == null)
                {
                    tempReferences.Add((reference, null));
                    reference.DigestValue = Array.Empty<byte>();
                }
            }
        }

        try
        {
            var signatureElement = this.GetXml();
            var element = FindElementById(signatureElement, idValue);
            if (element != null)
                return element;
        }
        finally
        {
            foreach (var temp in tempReferences)
            {
                temp.Ref.DigestValue = temp.OrigValue;
            }
        }

        return null;
    }

    private static XmlElement? FindElementById(XmlNode? node, string idValue)
    {
        if (node == null)
            return null;
        if (node is XmlElement element)
        {
            if (element.GetAttribute("Id") == idValue || element.GetAttribute("id") == idValue)
                return element;
        }

        foreach (XmlNode child in node.ChildNodes)
        {
            var result = FindElementById(child, idValue);
            if (result != null)
                return result;
        }

        return null;
    }
}
