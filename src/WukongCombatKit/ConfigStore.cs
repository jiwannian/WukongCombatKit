using System;
using System.IO;
using WukongCombatKit.Core;

namespace WukongCombatKit
{
    public static class ConfigStore
    {
        public static readonly string RelativeConfigPath = Path.Combine("CSharpLoader", "Mods", "WukongCombatKit", "config.json");
        public static CombatKitConfig Current { get; private set; } = CombatKitConfig.CreateDefault();

        public static CombatKitConfig Load()
        {
            try
            {
                if (File.Exists(RelativeConfigPath))
                {
                    Current = CombatKitConfig.Parse(File.ReadAllText(RelativeConfigPath));
                }
                else
                {
                    Current = CombatKitConfig.CreateDefault();
                }
            }
            catch (Exception ex)
            {
                Current = CombatKitConfig.CreateDefault();
                ModLog.Error("Config load failed: " + ex.Message);
            }

            return Current;
        }

        public static void Reload()
        {
            Load();
        }
    }
}
