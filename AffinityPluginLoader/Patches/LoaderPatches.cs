using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace AffinityPluginLoader.Patches
{
    /// <summary>
    /// APL's own Harmony patches (version strings).
    /// Applied during Stage 1 by PluginManager. TypeLoadException propagates for auto-deferral.
    /// </summary>
    public static class LoaderPatches
    {
        private static string _assemblyVersion = "";

        public static void ApplyVersionPatches(Harmony harmony, PluginInfo plugin)
        {
            _assemblyVersion = plugin?.Version ?? "unknown";

            var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

            if (serifAssembly == null)
            {
                Logger.Error("Serif.Affinity assembly not found for version patches");
                return;
            }

            var applicationType = serifAssembly.GetType("Serif.Affinity.Application");
            if (applicationType == null)
            {
                Logger.Error("Application type not found");
                return;
            }

            var method = applicationType.GetMethod("GetCurrentVerboseVersionString",
                BindingFlags.Public | BindingFlags.Instance);

            if (method != null)
            {
                harmony.Patch(method,
                    postfix: new HarmonyMethod(typeof(LoaderPatches), nameof(GetVerboseVersionString_Postfix)));
                Logger.Info("Patched GetCurrentVerboseVersionString");
            }
        }

        public static void GetVerboseVersionString_Postfix(ref string __result)
        {
            __result += $" (APL {_assemblyVersion})";
        }
    }
}
