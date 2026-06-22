using System;
using System.Globalization;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 XML Schema <c>time</c> 值，包含日內時間與選用時區 offset。
/// </summary>
public readonly struct OdfTime : IEquatable<OdfTime>
{
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
    private static readonly TimeSpan MaximumTimezoneOffset = TimeSpan.FromHours(14);

    /// <summary>
    /// 使用沒有時區 offset 的日內時間建立 <see cref="OdfTime"/>。
    /// </summary>
    /// <param name="timeOfDay">日內時間，範圍必須大於等於 00:00:00 且小於 24:00:00。</param>
    public OdfTime(TimeSpan timeOfDay)
        : this(timeOfDay, null)
    {
    }

    /// <summary>
    /// 使用日內時間與選用時區 offset 建立 <see cref="OdfTime"/>。
    /// </summary>
    /// <param name="timeOfDay">日內時間，範圍必須大於等於 00:00:00 且小於 24:00:00。</param>
    /// <param name="offset">選用時區 offset，範圍必須在 -14:00 至 +14:00 之間。</param>
    /// <exception cref="ArgumentOutOfRangeException">當日內時間或時區 offset 超出 XML Schema <c>time</c> 可接受範圍時擲回。</exception>
    public OdfTime(TimeSpan timeOfDay, TimeSpan? offset)
    {
        if (timeOfDay < TimeSpan.Zero || timeOfDay >= OneDay)
        {
            throw new ArgumentOutOfRangeException(nameof(timeOfDay), timeOfDay, OdfLocalizer.GetMessage("Err_OdfTime_TimeDayGreaterEqual"));
        }

        if (offset is { } value && (value < -MaximumTimezoneOffset || value > MaximumTimezoneOffset))
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, OdfLocalizer.GetMessage("Err_OdfTime_TimeZoneOffsetBetween"));
        }

        TimeOfDay = timeOfDay;
        Offset = offset;
    }

    /// <summary>
    /// 取得日內時間。
    /// </summary>
    public TimeSpan TimeOfDay { get; }

    /// <summary>
    /// 取得選用時區 offset；沒有時區資訊時為 <see langword="null"/>。
    /// </summary>
    public TimeSpan? Offset { get; }

    /// <summary>
    /// 嘗試解析 XML Schema <c>time</c> 字串。
    /// </summary>
    /// <param name="value">要解析的字串。</param>
    /// <param name="time">成功時傳回解析後的時間值。</param>
    /// <returns>若字串符合 XML Schema <c>time</c> 的常用 lexical form 則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfTime time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ReadOnlySpan<char> text = value.AsSpan().Trim();
        TimeSpan? offset = null;
        if (text.Length > 0 && text[text.Length - 1] == 'Z')
        {
            offset = TimeSpan.Zero;
            text = text.Slice(0, text.Length - 1);
        }
        else if (TryReadOffset(text, out TimeSpan parsedOffset, out int offsetStart))
        {
            offset = parsedOffset;
            text = text.Slice(0, offsetStart);
        }

        if (!TryParseTimeOfDay(text, out TimeSpan timeOfDay))
        {
            return false;
        }

        try
        {
            time = new OdfTime(timeOfDay, offset);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>
    /// 傳回 XML Schema <c>time</c> 字串。
    /// </summary>
    /// <returns>可寫入 ODF 屬性的時間字串。</returns>
    public override string ToString()
    {
        string formatted = FormatTimeOfDay(TimeOfDay);
        if (Offset is null)
        {
            return formatted;
        }

        if (Offset.Value == TimeSpan.Zero)
        {
            return formatted + "Z";
        }

        TimeSpan offset = Offset.Value;
        char sign = offset < TimeSpan.Zero ? '-' : '+';
        TimeSpan absolute = offset.Duration();
        return string.Concat(
            formatted,
            sign.ToString(),
            absolute.Hours.ToString("00", CultureInfo.InvariantCulture),
            ":",
            absolute.Minutes.ToString("00", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 判斷目前值是否等於另一個 ODF 時間。
    /// </summary>
    /// <param name="other">要比較的 ODF 時間。</param>
    /// <returns>若日內時間與時區 offset 都相等則為 <see langword="true"/>。</returns>
    public bool Equals(OdfTime other) => TimeOfDay == other.TimeOfDay && Offset == other.Offset;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfTime other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(TimeOfDay, Offset);

    /// <summary>
    /// 判斷兩個 ODF 時間是否相等。
    /// </summary>
    /// <param name="left">左側 ODF 時間。</param>
    /// <param name="right">右側 ODF 時間。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfTime left, OdfTime right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個 ODF 時間是否不相等。
    /// </summary>
    /// <param name="left">左側 ODF 時間。</param>
    /// <param name="right">右側 ODF 時間。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfTime left, OdfTime right) => !left.Equals(right);

    private static bool TryReadOffset(ReadOnlySpan<char> text, out TimeSpan offset, out int offsetStart)
    {
        offset = default;
        offsetStart = -1;
        if (text.Length < 6)
        {
            return false;
        }

        offsetStart = text.Length - 6;
        char sign = text[offsetStart];
        if ((sign != '+' && sign != '-') || text[offsetStart + 3] != ':')
        {
            return false;
        }

        if (!TryParseTwoDigits(text.Slice(offsetStart + 1, 2), out int hours) ||
            !TryParseTwoDigits(text.Slice(offsetStart + 4, 2), out int minutes) ||
            hours > 14 ||
            minutes > 59 ||
            (hours == 14 && minutes != 0))
        {
            return false;
        }

        offset = new TimeSpan(hours, minutes, 0);
        if (sign == '-')
        {
            offset = -offset;
        }

        return true;
    }

    private static bool TryParseTimeOfDay(ReadOnlySpan<char> text, out TimeSpan timeOfDay)
    {
        timeOfDay = default;
        if (text.Length < 8 || text[2] != ':' || text[5] != ':')
        {
            return false;
        }

        if (!TryParseTwoDigits(text.Slice(0, 2), out int hours) ||
            !TryParseTwoDigits(text.Slice(3, 2), out int minutes) ||
            !TryParseTwoDigits(text.Slice(6, 2), out int seconds) ||
            hours > 23 ||
            minutes > 59 ||
            seconds > 59)
        {
            return false;
        }

        long ticks = new TimeSpan(hours, minutes, seconds).Ticks;
        if (text.Length > 8)
        {
            if (text[8] != '.' || text.Length == 9)
            {
                return false;
            }

            ReadOnlySpan<char> fraction = text.Slice(9);
            if (fraction.Length > 7)
            {
                return false;
            }

            int fractionalTicks = 0;
            for (int i = 0; i < fraction.Length; i++)
            {
                char ch = fraction[i];
                if (ch < '0' || ch > '9')
                {
                    return false;
                }

                fractionalTicks = (fractionalTicks * 10) + (ch - '0');
            }

            for (int i = fraction.Length; i < 7; i++)
            {
                fractionalTicks *= 10;
            }

            ticks += fractionalTicks;
        }

        timeOfDay = new TimeSpan(ticks);
        return true;
    }

    private static bool TryParseTwoDigits(ReadOnlySpan<char> text, out int value)
    {
        value = 0;
        if (text.Length != 2)
        {
            return false;
        }

        char first = text[0];
        char second = text[1];
        if (first < '0' || first > '9' || second < '0' || second > '9')
        {
            return false;
        }

        value = ((first - '0') * 10) + (second - '0');
        return true;
    }

    private static string FormatTimeOfDay(TimeSpan timeOfDay)
    {
        string baseText = string.Concat(
            timeOfDay.Hours.ToString("00", CultureInfo.InvariantCulture),
            ":",
            timeOfDay.Minutes.ToString("00", CultureInfo.InvariantCulture),
            ":",
            timeOfDay.Seconds.ToString("00", CultureInfo.InvariantCulture));
        int fractionalTicks = (int)(timeOfDay.Ticks % TimeSpan.TicksPerSecond);
        if (fractionalTicks == 0)
        {
            return baseText;
        }

        return baseText + "." + fractionalTicks.ToString("0000000", CultureInfo.InvariantCulture).TrimEnd('0');
    }
}
