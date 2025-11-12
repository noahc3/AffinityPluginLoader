using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace AffinityPluginLoader.Patches
{
    /// <summary>
    /// Patches for Affinity application (version strings, etc.)
    /// </summary>
    public static class LoaderPatches
    {
        private static Harmony _harmony;
        private static bool _patchesApplied = false;

        public static void ApplyPatches(Harmony harmony)
        {
            _harmony = harmony;
            
            FileLog.Log($"Applying AffinityPluginLoader patches...\n");
            
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
                    FileLog.Log($"ERROR: Serif.Affinity assembly not found\n");
                    return;
                }
                
                FileLog.Log($"Found Serif.Affinity assembly: {serifAssembly.GetName().Version}\n");
                
                // Get the Application type
                var applicationType = serifAssembly.GetType("Serif.Affinity.Application");
                if (applicationType == null)
                {
                    FileLog.Log($"ERROR: Application type not found\n");
                    return;
                }
                
                // Patch GetCurrentVerboseVersionString (used in splash screen)
                var getVerboseVersionString = applicationType.GetMethod("GetCurrentVerboseVersionString", BindingFlags.Public | BindingFlags.Instance);
                if (getVerboseVersionString != null)
                {
                    var postfix = typeof(LoaderPatches).GetMethod(nameof(GetVerboseVersionString_Postfix), BindingFlags.Static | BindingFlags.Public);
                    _harmony.Patch(getVerboseVersionString, postfix: new HarmonyMethod(postfix));
                    FileLog.Log($"Patched GetCurrentVerboseVersionString\n");
                }
                
                _patchesApplied = true;
                FileLog.Log($"Version patches applied successfully!\n");
            }
            catch (Exception ex)
            {
                FileLog.Log($"Failed to apply version patches: {ex.Message}\n{ex.StackTrace}\n");
            }
        }

        // Postfix for GetCurrentVerboseVersionString (splash screen)
        public static void GetVerboseVersionString_Postfix(ref string __result)
        {
            __result = __result + " (AffinityPluginLoader 0.1.0.1)";
        }
    }
}
