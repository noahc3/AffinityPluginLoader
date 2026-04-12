using System;
using System.Collections.Generic;
using HarmonyLib;
using AffinityPluginLoader;
using AffinityPluginLoader.Core;
using AffinityPluginLoader.Settings;

namespace WineFix
{
    /// <summary>
    /// WineFix Plugin - Bug fixes for running Affinity under Wine
    /// </summary>
    public class WineFixPlugin : AffinityPlugin
    {
        public const string PluginId = "winefix";
        public const string ColorPickerMagnifierFixKey = "color_picker_magnifier_fix";
        public const string ColorPickerModeKey = "color_picker_sampling_mode";
        public const string BezierRenderingFixKey = "bezier_rendering_fix";
        public const string CollinearJoinFixKey = "collinear_join_fix";
        public const string SettingForceSyncFontEnum = "force_sync_font_enum";
        public const string SettingCanvaSignInHelper = "canva_sign_in_helper";

        public override PluginSettingsDefinition DefineSettings()
        {
            return new PluginSettingsDefinition(PluginId)
                .AddSection("Patches")
                .AddBool(SettingCanvaSignInHelper, "Canva sign-in helper",
                    defaultValue: true,
                    restartRequired: true,
                    description: "Patch the Canva sign-in dialog to include a helper textbox input and instructions to complete Canva sign-in without needing a protocol URL handler.")
                .AddBool(BezierRenderingFixKey, "Bezier curves: Fix tool preview path rendering",
                    defaultValue: true,
                    restartRequired: true,
                    description: "Apply patch to subdivide cubic bezier curves into quadratic segments for more accurate preview path rendering (eg. pen tool)")
                .AddBool(CollinearJoinFixKey, "Bezier curves: Fix collinear subdivision flicker artifacts",
                    defaultValue: true,
                    restartRequired: true,
                    description: "Apply patch to fix tangent line flicker artifacts between collinear bezier curve subdivisions.")
                .AddEnum(ColorPickerMagnifierFixKey, "Color picker: Wayland zoom magnifier fix",
                    new List<EnumOption>
                    {
                        new EnumOption("auto", "Auto"),
                        new EnumOption("enabled", "Enabled"),
                        new EnumOption("disabled", "Disabled")
                    },
                    defaultValue: "auto",
                    restartRequired: true,
                    description: "Patch the color picker zoom preview to work under Wayland. This should only be enabled when running under Wayland or XWayland, enabling this on X11 desktop environments will prevent the zoom preview from displaying content outside the bounds of the window canvas.\n- **Auto:** Automatically apply if Wayland or XWayland is detected.\n- **Enabled:** Always apply.\n- **Disabled:** Never apply.")
                .AddEnum(ColorPickerModeKey, "Color picker: color value sampling mode",
                    new List<EnumOption>
                    {
                        new EnumOption("native", "Native"),
                        new EnumOption("exact", "Exact")
                    },
                    defaultValue: "native",
                    description: "- **Native:** Use Affinity's built-in color sampling. Colors sampled within the canvas bounds will use the native document color space, but the color of the highlighted pixel in the zoom preview may differ slightly from the actual color value sampled.\n- **Exact:** Pick the exact color of the highlighted pixel in the zoom preview. Samples from a screen capture in sRGB rather than the document's native color space. May be more intuitive, but not recommended when editing documents using CMYK or wide-gamut color spaces.")
                .AddSection("Crash Fixes")
                .AddBool(SettingForceSyncFontEnum, "Force synchronous font enumeration",
                    defaultValue: true,
                    restartRequired: true,
                    description: "Disable parallel font enumeration to significantly reduce frequency of startup crashes. May increase application startup time on systems with lots of fonts.");
        }

        public override void OnPatch(Harmony harmony, IPluginContext context)
        {
            // Since these patch native code that we load ourselves,
            // we don't need to apply these with the defferal logic.
            if (context.Settings.GetEffectiveValue<bool>(BezierRenderingFixKey))
            {
                Patches.BezierRenderingPatch.Apply();
            }

            if (context.Settings.GetEffectiveValue<bool>(CollinearJoinFixKey))
            {
                Patches.CollinearJoinPatch.Apply();
            }

            context.Patch("MainWindowLoaded fix",
                h => Patches.MainWindowLoadedPatch.ApplyPatches(h));

            if (context.Settings.GetEffectiveValue<bool>(SettingForceSyncFontEnum))
            {
                context.Patch("FontEnumeration fix",
                    h => Patches.FontEnumerationPatch.ApplyPatches(h));
            }

            var magnifierFix = context.Settings.GetEffectiveValue<string>(ColorPickerMagnifierFixKey);
            bool applyMagnifierFix = magnifierFix == "enabled" ||
                (magnifierFix == "auto" && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")));

            if (applyMagnifierFix)
            {
                context.Patch("ColorPicker Wayland fix",
                    h => Patches.ColorPickerWaylandPatch.ApplyPatches(h));
            }
            else
            {
                Logger.Info("Skipping ColorPicker Wayland fix (setting: " + magnifierFix + ")");
            }

            if (context.Settings.GetEffectiveValue<bool>(SettingCanvaSignInHelper))
            {
                context.Patch("Canva sign-in paste URL fix",
                    h => Patches.CanvaSignInPatch.ApplyPatches(h));
            }
            else
            {
                Logger.Info("Skipping Canva sign-in helper (disabled in settings)");
            }
        }
    }
}
