using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace AffinityPluginLoader.Patches
{
    /// <summary>
    /// Patches for Affinity application (version strings, etc.)
    /// </summary>
    public static class LoaderPatches
    {
        private static Harmony _harmony;
        private static bool _patchesApplied = false;
        private static string _assemblyVersion = "";

        public static void ApplyPatches(Harmony harmony, PluginInfo plugin)
        {
            _harmony = harmony;

            Logger.Info($"Applying Affinity Plugin Loader patches...");

            _assemblyVersion = plugin.Version ?? "not found";

            // Apply version string patches
            ApplyVersionPatches();

            // Apply preferences dialog patches
            PreferencesPatches.ApplyPatches(harmony);
        }

        private static void ApplyVersionPatches()
        {
            if (_patchesApplied)
                return;

            try
            {
                // Find the Serif.Affinity assembly
                var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

                if (serifAssembly == null)
                {
                    Logger.Error($"ERROR: Serif.Affinity assembly not found");
                    return;
                }

                Logger.Info($"Found Serif.Affinity assembly: {serifAssembly.GetName().Version}");

                // Get the Application type
                var applicationType = serifAssembly.GetType("Serif.Affinity.Application");
                if (applicationType == null)
                {
                    Logger.Error($"ERROR: Application type not found");
                    return;
                }
                
                // Patch GetCurrentVerboseVersionString (used in splash screen)
                var getVerboseVersionString = applicationType.GetMethod("GetCurrentVerboseVersionString", BindingFlags.Public | BindingFlags.Instance);
                if (getVerboseVersionString != null)
                {
                    var postfix = typeof(LoaderPatches).GetMethod(nameof(GetVerboseVersionString_Postfix), BindingFlags.Static | BindingFlags.Public);
                    _harmony.Patch(getVerboseVersionString, postfix: new HarmonyMethod(postfix));
                    Logger.Info($"Patched GetCurrentVerboseVersionString");
                }

                _patchesApplied = true;
                Logger.Info($"Version patches applied successfully!");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply version patches", ex);
            }
        }

        // Postfix for GetCurrentVerboseVersionString (splash screen)
        public static void GetVerboseVersionString_Postfix(ref string __result)
        {
            __result = __result + $" (APL {_assemblyVersion})";
        }
    }
}
