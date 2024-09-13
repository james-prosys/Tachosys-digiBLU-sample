using System;

namespace digiDownloadPC
{
    internal static class NationAlpha
    {

        private static readonly string[] nationAlphas = new string[] {
            " ", "A", "AL", "AND", "ARM", "AZ", "B", "BG", "BIH", "BY", "CH", "CY", "CZ", "D", "DK", "E", "EST", "F", "FIN", "FL", "FO", "UK", "GE", "GR", "H", "HR",
            "I", "IRL", "IS", "KZ", "L", "LT", "LV", "M", "MC", "MD", "MK", "N", "NL", "P", "PL", "RO", "RSM", "RUS", "S", "SK", "SLO", "TM", "TR", "UA", "V", "YU", "MNE", "SRB", "UZ", "TJ" };

        public static string GetNationAlpha(byte code)
        {
            if (code < nationAlphas.Length) {
                return nationAlphas[code];
            }
            else {
                switch (code) {
                    case 0xfd:
                        // European Community
                        return "EC";
                    case 0xfe:
                        // Rest of Europe
                        return "EUR";
                    case 0xff:
                        // Rest of World
                        return "WLD";
                    default:
                        return "RFU";
                }
            }

        }

    }

    internal static class NationCodes
    {
        public static string ToNationDescription(byte nationCode)
        {
            if (nationCode == 0x0)
                return Properties.NationCodes.ResourceManager.GetString("NATION_0");
            else
                return Properties.NationCodes.ResourceManager.GetString("NATION_" + NationAlpha.GetNationAlpha(nationCode));
        }
    }
}
