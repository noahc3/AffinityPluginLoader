using HarmonyLib;
using AffinityPluginLoader;
using AffinityPluginLoader.Settings;

namespace WineFix
{
    public class WineFixPlugin : AffinityPlugin
    {
        public const string SettingForceSyncFontEnum = "force_sync_font_enum";

        public override PluginSettingsDefinition DefineSettings()
        {
            return new PluginSettingsDefinition("winefix")
                .AddSection("Crash Fixes")
                .AddBool(SettingForceSyncFontEnum, "Force synchronous font enumeration",
                    defaultValue: true,
                    restartRequired: true,
                    description: "Disable parallel font enumeration to significantly reduce frequency of startup crashes. May increase application startup time on systems with lots of fonts.");
        }

        public override void OnPatch(Harmony harmony, IPluginContext context)
        {
            context.Patch("MainWindowLoaded fix",
                h => Patches.MainWindowLoadedPatch.ApplyPatches(h));

            if (context.Settings.GetEffectiveValue<bool>(SettingForceSyncFontEnum))
            {
                context.Patch("FontEnumeration fix",
                    h => Patches.FontEnumerationPatch.ApplyPatches(h));
            }
        }
    }
}
