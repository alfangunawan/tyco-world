using System.Globalization;

namespace TycoWorld.Gameplay
{
    /// <summary>When each kind of shop serves (spec #6 §2): most close at night, the 24-hour minimarket never does.</summary>
    public static class OpeningHours
    {
        public static void For(ShopType type, out float open, out float close)
        {
            switch (type)
            {
                case ShopType.Warung: open = 6f; close = 22f; break;
                case ShopType.WarungMakan: open = 6f; close = 21f; break;
                case ShopType.Distro: open = 9f; close = 21f; break;
                case ShopType.Konter: open = 8f; close = 22f; break;
                case ShopType.Apotek: open = 8f; close = 21f; break;
                case ShopType.Bengkel: open = 8f; close = 17f; break;
                default: open = 0f; close = 24f; break;
            }
        }

        public static bool IsOpen(ShopType type, float hours)
        {
            For(type, out float open, out float close);
            if (close - open >= 24f) return true;
            float h = ((hours % 24f) + 24f) % 24f;
            return open <= close ? h >= open && h < close : h >= open || h < close;
        }

        public static string Describe(ShopType type)
        {
            For(type, out float open, out float close);
            if (close - open >= 24f) return "Buka 24 jam";
            return "Buka " + Hour(open) + "-" + Hour(close);
        }

        static string Hour(float h) => ((int)h).ToString("00", CultureInfo.InvariantCulture) + ".00";
    }
}
