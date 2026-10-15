using System.Globalization;
using System.Text;

namespace TycoWorld.Gameplay
{
    /// <summary>"Rp 12.000": Indonesian thousands dots, no decimals, culture-independent (spec #6 §2).</summary>
    public static class RupiahFormat
    {
        public static string Format(long amount)
        {
            bool negative = amount < 0;
            string digits = (negative ? -amount : amount).ToString(CultureInfo.InvariantCulture);
            var sb = new StringBuilder(digits.Length + 6);
            if (negative) sb.Append('-');
            sb.Append("Rp ");
            int lead = digits.Length % 3;
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (i - lead) % 3 == 0) sb.Append('.');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }
    }
}
