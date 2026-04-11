using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace WineFix.Patches
{
    /// <summary>
    /// Fixes Wine assembly loading bug which throws an exception in OnMainWindowLoaded
    /// when checking for previous package installation, which causes prefs saving to fail.
    /// Uses a transpiler to replace the call to HasPreviousPackageInstalled() with 'false'
    /// </summary>
    public static class MainWindowLoadedPatch
    {
        public static void ApplyPatches(Harmony harmony)
        {
            Logger.Info("Applying MainWindowLoaded patch (Wine fix)...");

            var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

            if (serifAssembly == null)
            {
                Logger.Error("Serif.Affinity assembly not found");
                return;
            }

            var applicationType = serifAssembly.GetType("Serif.Affinity.Application");
            if (applicationType == null)
            {
                Logger.Error("Application type not found");
                return;
            }
            
            var onMainWindowLoaded = applicationType.GetMethod("OnMainWindowLoaded", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (onMainWindowLoaded != null)
            {
                var transpiler = typeof(MainWindowLoadedPatch).GetMethod(nameof(OnMainWindowLoaded_Transpiler),
                    BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(onMainWindowLoaded, transpiler: new HarmonyMethod(transpiler));
                Logger.Info("Patched OnMainWindowLoaded to skip HasPreviousPackageInstalled call");
            }
            else
            {
                Logger.Error("OnMainWindowLoaded method not found");
            }
        }

        // Transpiler that replaces the call to HasPreviousPackageInstalled() with loading 'false'
        public static IEnumerable<CodeInstruction> OnMainWindowLoaded_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool patchedPackageCheck = false;

            for (int i = 0; i < codes.Count; i++)
            {
                var instruction = codes[i];
                
                // Look for: call instance bool Serif.Affinity.Application::HasPreviousPackageInstalled()
                // (Index 1 in the IL - this is a non-virtual call, not callvirt!)
                if (!patchedPackageCheck &&
                    (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                    instruction.operand is MethodInfo method &&
                    method.Name == "HasPreviousPackageInstalled")
                {
                    Logger.Debug($"Found HasPreviousPackageInstalled call at instruction {i}");

                    // Replace: ldarg.0 + call HasPreviousPackageInstalled
                    // With:    ldc.i4.0 (just load false, skip the ldarg.0 before it)
                    // Check if previous instruction is ldarg.0
                    if (i > 0 && codes[i - 1].opcode == OpCodes.Ldarg_0)
                    {
                        // Create new instruction but preserve labels from the old one
                        var newLoadFalse = new CodeInstruction(OpCodes.Ldc_I4_0);
                        newLoadFalse.labels.AddRange(codes[i - 1].labels); // Transfer labels!
                        codes[i - 1] = newLoadFalse;

                        var newNop = new CodeInstruction(OpCodes.Nop);
                        newNop.labels.AddRange(codes[i].labels); // Transfer labels!
                        codes[i] = newNop;

                        Logger.Debug($"Replaced ldarg.0 + HasPreviousPackageInstalled with 'ldc.i4.0' (false)");
                    }
                    else
                    {
                        // Fallback: just replace the call
                        codes[i] = new CodeInstruction(OpCodes.Pop);
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4_0));
                        Logger.Debug($"Replaced HasPreviousPackageInstalled call with 'false' (fallback)");
                    }
                    
                    patchedPackageCheck = true;
                    continue;
                }
            }

            if (!patchedPackageCheck)
            {
                Logger.Warning($"WARNING: Could not find HasPreviousPackageInstalled call to patch");
            }

            return codes.AsEnumerable();
        }
    }
}
