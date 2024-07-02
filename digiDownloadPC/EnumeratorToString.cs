using System;
using Tachosys.Files;

namespace digiDownloadPC
{
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
