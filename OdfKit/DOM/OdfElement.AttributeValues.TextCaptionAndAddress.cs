using System;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;
/// <summary>
/// Provides the OdfElement API.
/// 提供 OdfElement API。
/// </summary>

public partial class OdfElement
{
    #region Attribute Values - Text Caption & Address
    /// <summary>
    /// Short overload of GetTextCaptionSequenceFormatAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextCaptionSequenceFormatAttributeValue 多載。
    /// </summary>
    public OdfTextCaptionSequenceFormat? GetTextCaptionSequenceFormatAttributeValue(string localName, string namespaceUri) => GetTextCaptionSequenceFormatAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text caption sequence format attribute value.
    /// 取得具有 schema awareness 的文字標號序列格式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字標號序列格式；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextCaptionSequenceFormat? GetTextCaptionSequenceFormatAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextCaptionSequenceFormat>(value, OdfElementSchemaRegistry.TryParseTextCaptionSequenceFormat);
    }
    /// <summary>
    /// Short overload of SetTextCaptionSequenceFormatAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextCaptionSequenceFormatAttributeValue 多載。
    /// </summary>
    public void SetTextCaptionSequenceFormatAttributeValue(string localName, string namespaceUri, OdfTextCaptionSequenceFormat value) => SetTextCaptionSequenceFormatAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextCaptionSequenceFormatAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextCaptionSequenceFormatAttributeValue 多載。
    /// </summary>
    public void SetTextCaptionSequenceFormatAttributeValue(string localName, string namespaceUri, OdfTextCaptionSequenceFormat value, string? prefix) => SetTextCaptionSequenceFormatAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text caption sequence format attribute value.
    /// 設定具有 schema awareness 的文字標號序列格式屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字標號序列格式</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextCaptionSequenceFormatAttributeValue(string localName, string namespaceUri, OdfTextCaptionSequenceFormat value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextCaptionSequenceFormat(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextNumberPositionAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextNumberPositionAttributeValue 多載。
    /// </summary>
    public OdfTextNumberPosition? GetTextNumberPositionAttributeValue(string localName, string namespaceUri) => GetTextNumberPositionAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text number position attribute value.
    /// 取得具有 schema awareness 的文字編號位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字編號位置；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextNumberPosition? GetTextNumberPositionAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextNumberPosition>(value, OdfElementSchemaRegistry.TryParseTextNumberPosition);
    }
    /// <summary>
    /// Short overload of SetTextNumberPositionAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextNumberPositionAttributeValue 多載。
    /// </summary>
    public void SetTextNumberPositionAttributeValue(string localName, string namespaceUri, OdfTextNumberPosition value) => SetTextNumberPositionAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextNumberPositionAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextNumberPositionAttributeValue 多載。
    /// </summary>
    public void SetTextNumberPositionAttributeValue(string localName, string namespaceUri, OdfTextNumberPosition value, string? prefix) => SetTextNumberPositionAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text number position attribute value.
    /// 設定具有 schema awareness 的文字編號位置屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字編號位置</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextNumberPositionAttributeValue(string localName, string namespaceUri, OdfTextNumberPosition value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextNumberPosition(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextPlaceholderTypeAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextPlaceholderTypeAttributeValue 多載。
    /// </summary>
    public OdfTextPlaceholderType? GetTextPlaceholderTypeAttributeValue(string localName, string namespaceUri) => GetTextPlaceholderTypeAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text placeholder type attribute value.
    /// 取得具有 schema awareness 的文字預留位置類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字預留位置類型；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextPlaceholderType? GetTextPlaceholderTypeAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextPlaceholderType>(value, OdfElementSchemaRegistry.TryParseTextPlaceholderType);
    }
    /// <summary>
    /// Short overload of SetTextPlaceholderTypeAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextPlaceholderTypeAttributeValue 多載。
    /// </summary>
    public void SetTextPlaceholderTypeAttributeValue(string localName, string namespaceUri, OdfTextPlaceholderType value) => SetTextPlaceholderTypeAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextPlaceholderTypeAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextPlaceholderTypeAttributeValue 多載。
    /// </summary>
    public void SetTextPlaceholderTypeAttributeValue(string localName, string namespaceUri, OdfTextPlaceholderType value, string? prefix) => SetTextPlaceholderTypeAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text placeholder type attribute value.
    /// 設定具有 schema awareness 的文字預留位置類型屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字預留位置類型</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextPlaceholderTypeAttributeValue(string localName, string namespaceUri, OdfTextPlaceholderType value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextPlaceholderType(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextAnimationAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextAnimationAttributeValue 多載。
    /// </summary>
    public OdfTextAnimation? GetTextAnimationAttributeValue(string localName, string namespaceUri) => GetTextAnimationAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text animation attribute value.
    /// 取得具有 schema awareness 的文字動畫屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字動畫；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextAnimation? GetTextAnimationAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextAnimation>(value, OdfElementSchemaRegistry.TryParseTextAnimation);
    }
    /// <summary>
    /// Short overload of SetTextAnimationAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextAnimationAttributeValue 多載。
    /// </summary>
    public void SetTextAnimationAttributeValue(string localName, string namespaceUri, OdfTextAnimation value) => SetTextAnimationAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextAnimationAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextAnimationAttributeValue 多載。
    /// </summary>
    public void SetTextAnimationAttributeValue(string localName, string namespaceUri, OdfTextAnimation value, string? prefix) => SetTextAnimationAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text animation attribute value.
    /// 設定具有 schema awareness 的文字動畫屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字動畫</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextAnimationAttributeValue(string localName, string namespaceUri, OdfTextAnimation value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextAnimation(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextAnimationDirectionAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextAnimationDirectionAttributeValue 多載。
    /// </summary>
    public OdfTextAnimationDirection? GetTextAnimationDirectionAttributeValue(string localName, string namespaceUri) => GetTextAnimationDirectionAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text animation direction attribute value.
    /// 取得具有 schema awareness 的文字動畫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字動畫方向；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextAnimationDirection? GetTextAnimationDirectionAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextAnimationDirection>(value, OdfElementSchemaRegistry.TryParseTextAnimationDirection);
    }
    /// <summary>
    /// Short overload of SetTextAnimationDirectionAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextAnimationDirectionAttributeValue 多載。
    /// </summary>
    public void SetTextAnimationDirectionAttributeValue(string localName, string namespaceUri, OdfTextAnimationDirection value) => SetTextAnimationDirectionAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextAnimationDirectionAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextAnimationDirectionAttributeValue 多載。
    /// </summary>
    public void SetTextAnimationDirectionAttributeValue(string localName, string namespaceUri, OdfTextAnimationDirection value, string? prefix) => SetTextAnimationDirectionAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text animation direction attribute value.
    /// 設定具有 schema awareness 的文字動畫方向屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字動畫方向</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextAnimationDirectionAttributeValue(string localName, string namespaceUri, OdfTextAnimationDirection value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextAnimationDirection(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetTextKindAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetTextKindAttributeValue 多載。
    /// </summary>
    public OdfTextKind? GetTextKindAttributeValue(string localName, string namespaceUri) => GetTextKindAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets text kind attribute value.
    /// 取得具有 schema awareness 的文字索引專案種類屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的文字索引專案種類；若屬性不存在或不是已知 token 則為 <see langword="null"/></returns>
    public OdfTextKind? GetTextKindAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementEnumAttributeAccess.GetNullable<OdfTextKind>(value, OdfElementSchemaRegistry.TryParseTextKind);
    }
    /// <summary>
    /// Short overload of SetTextKindAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetTextKindAttributeValue 多載。
    /// </summary>
    public void SetTextKindAttributeValue(string localName, string namespaceUri, OdfTextKind value) => SetTextKindAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetTextKindAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetTextKindAttributeValue 多載。
    /// </summary>
    public void SetTextKindAttributeValue(string localName, string namespaceUri, OdfTextKind value, string? prefix) => SetTextKindAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets text kind attribute value.
    /// 設定具有 schema awareness 的文字索引專案種類屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的文字索引專案種類</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetTextKindAttributeValue(string localName, string namespaceUri, OdfTextKind value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, OdfElementSchemaRegistry.FormatTextKind(value), prefix, version);
    }

    /// <summary>
    /// Short overload of GetPercentAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetPercentAttributeValue 多載。
    /// </summary>
    public OdfPercent? GetPercentAttributeValue(string localName, string namespaceUri) => GetPercentAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets percent attribute value.
    /// 取得具有 schema awareness 的 0 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的百分比；若屬性不存在或超出 0 到 100 範圍則為 <see langword="null"/></returns>
    public OdfPercent? GetPercentAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetPercent(value);
    }

    /// <summary>
    /// Short overload of GetSignedPercentAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetSignedPercentAttributeValue 多載。
    /// </summary>
    public OdfPercent? GetSignedPercentAttributeValue(string localName, string namespaceUri) => GetSignedPercentAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets signed percent attribute value.
    /// 取得具有 schema awareness 的 -100 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的百分比；若屬性不存在或超出 -100 到 100 範圍則為 <see langword="null"/></returns>
    public OdfPercent? GetSignedPercentAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetSignedPercent(value);
    }
    /// <summary>
    /// Short overload of SetPercentAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetPercentAttributeValue 多載。
    /// </summary>
    public void SetPercentAttributeValue(string localName, string namespaceUri, OdfPercent value) => SetPercentAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetPercentAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetPercentAttributeValue 多載。
    /// </summary>
    public void SetPercentAttributeValue(string localName, string namespaceUri, OdfPercent value, string? prefix) => SetPercentAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets percent attribute value.
    /// 設定具有 schema awareness 的 0 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的百分比</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    /// <exception cref="ArgumentOutOfRangeException">當百分比值為負數時擲回</exception>
    public void SetPercentAttributeValue(string localName, string namespaceUri, OdfPercent value, string? prefix, OdfVersion version)
    {
        if (value.Percent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value.Percent, OdfLocalizer.GetMessage("Err_OdfElement_PercentageValuesCannotNegative"));
        }

        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of SetSignedPercentAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetSignedPercentAttributeValue 多載。
    /// </summary>
    public void SetSignedPercentAttributeValue(string localName, string namespaceUri, OdfPercent value) => SetSignedPercentAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetSignedPercentAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetSignedPercentAttributeValue 多載。
    /// </summary>
    public void SetSignedPercentAttributeValue(string localName, string namespaceUri, OdfPercent value, string? prefix) => SetSignedPercentAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);


    /// <summary>
    /// Sets signed percent attribute value.
    /// 設定具有 schema awareness 的 -100 到 100 百分比屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的百分比</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetSignedPercentAttributeValue(string localName, string namespaceUri, OdfPercent value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetCellAddressAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetCellAddressAttributeValue 多載。
    /// </summary>
    public OdfCellAddressReference? GetCellAddressAttributeValue(string localName, string namespaceUri) => GetCellAddressAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets cell address attribute value.
    /// 取得具有 schema awareness 的儲存格位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的儲存格位址；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfCellAddressReference? GetCellAddressAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfCellAddressReference>(value, OdfCellAddressReference.TryParse);
    }
    /// <summary>
    /// Short overload of SetCellAddressAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetCellAddressAttributeValue 多載。
    /// </summary>
    public void SetCellAddressAttributeValue(string localName, string namespaceUri, OdfCellAddressReference value) => SetCellAddressAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetCellAddressAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetCellAddressAttributeValue 多載。
    /// </summary>
    public void SetCellAddressAttributeValue(string localName, string namespaceUri, OdfCellAddressReference value, string? prefix) => SetCellAddressAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets cell address attribute value.
    /// 設定具有 schema awareness 的儲存格位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的儲存格位址</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetCellAddressAttributeValue(string localName, string namespaceUri, OdfCellAddressReference value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetCellRangeAddressAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetCellRangeAddressAttributeValue 多載。
    /// </summary>
    public OdfCellRangeAddress? GetCellRangeAddressAttributeValue(string localName, string namespaceUri) => GetCellRangeAddressAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets cell range address attribute value.
    /// 取得具有 schema awareness 的儲存格範圍位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的儲存格範圍位址；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfCellRangeAddress? GetCellRangeAddressAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfCellRangeAddress>(value, OdfCellRangeAddress.TryParse);
    }
    /// <summary>
    /// Short overload of SetCellRangeAddressAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetCellRangeAddressAttributeValue 多載。
    /// </summary>
    public void SetCellRangeAddressAttributeValue(string localName, string namespaceUri, OdfCellRangeAddress value) => SetCellRangeAddressAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetCellRangeAddressAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetCellRangeAddressAttributeValue 多載。
    /// </summary>
    public void SetCellRangeAddressAttributeValue(string localName, string namespaceUri, OdfCellRangeAddress value, string? prefix) => SetCellRangeAddressAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets cell range address attribute value.
    /// 設定具有 schema awareness 的儲存格範圍位址屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的儲存格範圍位址</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetCellRangeAddressAttributeValue(string localName, string namespaceUri, OdfCellRangeAddress value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }

    /// <summary>
    /// Short overload of GetCellRangeAddressListAttributeValue that accepts localName and namespaceUri; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName 與 namespaceUri；其餘可選參數使用預設值並轉呼叫最長 GetCellRangeAddressListAttributeValue 多載。
    /// </summary>
    public OdfCellRangeAddressList? GetCellRangeAddressListAttributeValue(string localName, string namespaceUri) => GetCellRangeAddressListAttributeValue(localName, namespaceUri, OdfVersion.Odf14);


    /// <summary>
    /// Gets cell range address list attribute value.
    /// 取得具有 schema awareness 的儲存格範圍位址清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="version">ODF 版本內容</param>
    /// <returns>解析後的儲存格範圍位址清單；若屬性不存在或格式無效則為 <see langword="null"/></returns>
    public OdfCellRangeAddressList? GetCellRangeAddressListAttributeValue(string localName, string namespaceUri, OdfVersion version)
    {
        string? value = GetAttributeValue(localName, namespaceUri, version);
        return OdfElementDomainAttributeAccess.GetNullable<OdfCellRangeAddressList>(value, OdfCellRangeAddressList.TryParse);
    }
    /// <summary>
    /// Short overload of SetCellRangeAddressListAttributeValue that accepts localName, namespaceUri, and value; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri 與 value；其餘可選參數使用預設值並轉呼叫最長 SetCellRangeAddressListAttributeValue 多載。
    /// </summary>
    public void SetCellRangeAddressListAttributeValue(string localName, string namespaceUri, OdfCellRangeAddressList value) => SetCellRangeAddressListAttributeValue(localName, namespaceUri, value, null, OdfVersion.Odf14);

    /// <summary>
    /// Short overload of SetCellRangeAddressListAttributeValue that accepts localName, namespaceUri, value, and prefix; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 localName、namespaceUri、value 與 prefix；其餘可選參數使用預設值並轉呼叫最長 SetCellRangeAddressListAttributeValue 多載。
    /// </summary>
    public void SetCellRangeAddressListAttributeValue(string localName, string namespaceUri, OdfCellRangeAddressList value, string? prefix) => SetCellRangeAddressListAttributeValue(localName, namespaceUri, value, prefix, OdfVersion.Odf14);



    /// <summary>
    /// Sets cell range address list attribute value.
    /// 設定具有 schema awareness 的儲存格範圍位址清單屬性。
    /// </summary>
    /// <param name="localName">屬性局部名稱</param>
    /// <param name="namespaceUri">屬性命名空間 URI</param>
    /// <param name="value">要寫入的儲存格範圍位址清單</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <param name="version">ODF 版本內容</param>
    public void SetCellRangeAddressListAttributeValue(string localName, string namespaceUri, OdfCellRangeAddressList value, string? prefix, OdfVersion version)
    {
        SetAttributeValue(localName, namespaceUri, value.Value, prefix, version);
    }



    #endregion
}
