using System.Collections.Generic;
using HarmonyLib;
using AffinityPluginLoader;
using AffinityPluginLoader.Settings;

namespace WineFix
{
    /// <summary>
    /// WineFix Plugin - Bug fixes for running Affinity under Wine
    /// </summary>
    public class WineFixPlugin : AffinityPlugin
    {
        public const string PluginId = "winefix";
        public const string ColorPickerModeKey = "color_picker_mode";

        public override PluginSettingsDefinition DefineSettings()
        {
            return new PluginSettingsDefinition(PluginId)
                .AddSection("Patches")
                .AddEnum(ColorPickerModeKey, "Color Picker Mode",
                    new List<EnumOption>
                    {
                        new EnumOption("native", "Native"),
                        new EnumOption("exact", "Exact")
                    },
                    defaultValue: "native",
                    description: "Native uses Affinity's built-in color picking which may differ from the zoom preview pixel. Exact picks the exact color of the highlighted pixel in the zoom preview.");
        }

        public override void OnPatch(Harmony harmony, IPluginContext context)
        {
            context.Patch("MainWindowLoaded fix",
                h => Patches.MainWindowLoadedPatch.ApplyPatches(h));

            context.Patch("ColorPicker Wayland fix",
                h => Patches.ColorPickerWaylandPatch.ApplyPatches(h));
        }
    }
}
