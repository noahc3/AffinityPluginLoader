using System;
using System.Collections.Generic;
using HarmonyLib;
using AffinityPluginLoader.Core;
using AffinityPluginLoader.Settings;

namespace WineFix
{
    /// <summary>
    /// WineFix Plugin - Bug fixes for running Affinity under Wine
    /// </summary>
    public class WineFixPlugin : AffinityPluginLoader.AffinityPlugin
    {
        public const string PluginId = "winefix";
        public const string ColorPickerModeKey = "color_picker_mode";

        public override void Initialize(Harmony harmony)
        {
            try
            {
                Logger.Info("WineFix plugin initializing...");

                Patches.MainWindowLoadedPatch.ApplyPatches(harmony);
                Patches.ColorPickerWaylandPatch.ApplyPatches(harmony);

                Logger.Info("WineFix plugin initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Error initializing WineFix", ex);
            }
        }

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
    }
}
