using System.Globalization;
using System.Text;

namespace TuiDo.Domain;

/// <summary>
/// Turns what the user typed in the "Icon" field into a glyph.
/// Accepts the glyph itself, a hex code point ("f522", "0xf522") or "\uXXXX" escapes (surrogate pairs included).
/// </summary>
internal static class IconParser
{
    public const string DefaultIcon = "\uf022";

    public static string Parse(string? text)
    {
        var s = (text ?? "").Trim();
        if (s.Length == 0) return DefaultIcon;

        if (TryUnescape(s, out var unescaped)) return unescaped;

        var hex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
        if (hex.Length is >= 4 and <= 6 && hex.All(Uri.IsHexDigit))
        {
            var codePoint = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (codePoint is > 0 and <= 0x10FFFF && codePoint is not (>= 0xD800 and <= 0xDFFF))
                return char.ConvertFromUtf32(codePoint);
        }

        return s;
    }

    private static bool TryUnescape(string s, out string result)
    {
        var sb = new StringBuilder();
        var found = false;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 5 < s.Length && s[i + 1] == 'u'
                && int.TryParse(s.AsSpan(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var unit))
            {
                sb.Append((char)unit);
                i += 5;
                found = true;
            }
            else sb.Append(s[i]);
        }

        result = sb.ToString();
        return found;
    }
}
