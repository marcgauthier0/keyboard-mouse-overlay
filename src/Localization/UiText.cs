using System;
using System.Globalization;

namespace GamingKeypressOverlay.Localization
{
    internal static class UiText
    {
        public static bool IsFrench => string.Equals(
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            "fr",
            StringComparison.OrdinalIgnoreCase);

        public static string Get(string english, string french) => IsFrench ? french : english;
    }
}
