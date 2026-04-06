using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace WineFix.Patches
{
    /// <summary>
    /// Disables parallel font enumeration on Wine to avoid an intermittent
    /// access violation (0xC0000005) in libkernel.dll during startup font loading.
    /// </summary>
    public static class FontEnumerationPatch
    {
        public static void ApplyPatches(Harmony harmony)
        {
            Logger.Info("Applying FontEnumeration patch (Wine fix)...");

            var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

            if (serifAssembly == null)
            {
                Logger.Error("Serif.Affinity assembly not found");
                return;
            }

            var appType = serifAssembly.GetType("Serif.Affinity.Application");
            if (appType == null)
            {
                Logger.Error("Serif.Affinity.Application type not found");
                return;
            }

            var prop = appType.GetProperty("ParallelFontEnumerationDisabled",
                BindingFlags.Public | BindingFlags.Instance);

            if (prop?.GetGetMethod() != null)
            {
                harmony.Patch(prop.GetGetMethod(),
                    postfix: new HarmonyMethod(typeof(FontEnumerationPatch), nameof(ForceDisabled)));
                Logger.Info("Patched ParallelFontEnumerationDisabled to return true");
            }
            else
            {
                Logger.Error("ParallelFontEnumerationDisabled property not found");
            }
        }

        public static void ForceDisabled(ref bool __result)
        {
            __result = true;
        }
    }
}
