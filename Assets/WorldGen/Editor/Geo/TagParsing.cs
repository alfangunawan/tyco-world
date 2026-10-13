using System;
using System.Globalization;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Culture-invariant parsing of numeric OSM tag values such as "6", "6 m", "6.5", "10;12".</summary>
    public static class TagParsing
    {
        public static bool TryParseLeadingFloat(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();
            int i = 0;
            bool seenDigit = false, seenDot = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (c >= '0' && c <= '9') { seenDigit = true; i++; }
                else if (c == '.' && !seenDot) { seenDot = true; i++; }
                else break;
            }
            if (!seenDigit) return false;
            if (!float.TryParse(s.Substring(0, i), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            return value > 0f && !float.IsInfinity(value);
        }

        public static bool TryParseCount(string text, int max, out int value)
        {
            value = 0;
            if (!TryParseLeadingFloat(text, out float f)) return false;
            value = (int)Math.Floor(f + 0.5f);
            return value >= 1 && value <= max;
        }

        public static bool TryParseLevels(string text, out int levels) => TryParseCount(text, 200, out levels);
    }
}
