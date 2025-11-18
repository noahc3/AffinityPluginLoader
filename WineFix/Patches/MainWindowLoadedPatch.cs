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
            try
            {
                Logger.Info($"Applying MainWindowLoaded patch (Wine fix)...");

                // Find the Serif.Affinity assembly
                var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

                if (serifAssembly == null)
                {
                    Logger.Error($"ERROR: Serif.Affinity assembly not found");
                    return;
                }

                // Get the Application type
                var applicationType = serifAssembly.GetType("Serif.Affinity.Application");
                if (applicationType == null)
                {
                    Logger.Error($"ERROR: Application type not found");
                    return;
                }
                
                // Find OnMainWindowLoaded method
                var onMainWindowLoaded = applicationType.GetMethod("OnMainWindowLoaded", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (onMainWindowLoaded != null)
                {
                    // Use transpiler to modify the IL
                    var transpiler = typeof(MainWindowLoadedPatch).GetMethod(nameof(OnMainWindowLoaded_Transpiler),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(onMainWindowLoaded, transpiler: new HarmonyMethod(transpiler));
                    Logger.Info($"Patched OnMainWindowLoaded to skip HasPreviousPackageInstalled call");
                }
                else
                {
                    Logger.Error($"ERROR: OnMainWindowLoaded method not found");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply MainWindowLoaded patch", ex);
            }
        }

        // Transpiler that replaces the call to HasPreviousPackageInstalled() with loading 'false'
        // and also removes the call to base.OnMainWindowLoaded()
        public static IEnumerable<CodeInstruction> OnMainWindowLoaded_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool patchedPackageCheck = false;
            bool patchedBaseCall = false;

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
                
                // Look for: call instance void Serif.Interop.Persona.Application::OnMainWindowLoaded(...)
                // (Index 22 in the IL - base class is Serif.Interop.Persona.Application, not System.Windows.Application!)
                if (!patchedBaseCall &&
                    instruction.opcode == OpCodes.Call &&
                    instruction.operand is MethodInfo baseMethod &&
                    baseMethod.Name == "OnMainWindowLoaded" &&
                    baseMethod.DeclaringType != null &&
                    (baseMethod.DeclaringType.FullName == "Serif.Interop.Persona.Application" ||
                     baseMethod.DeclaringType.FullName == "System.Windows.Application"))
                {
                    Logger.Debug($"Found base.OnMainWindowLoaded call at instruction {i}");

                    // The IL sequence is (indices 20-22):
                    //   ldarg.0          ; load 'this'
                    //   ldarg.1          ; load 'mainWindow'
                    //   call base.OnMainWindowLoaded
                    // Replace all three with NOPs, preserving labels
                    if (i >= 2 &&
                        codes[i - 2].opcode == OpCodes.Ldarg_0 &&
                        codes[i - 1].opcode == OpCodes.Ldarg_1)
                    {
                        // Create NOPs but preserve labels
                        var nop1 = new CodeInstruction(OpCodes.Nop);
                        nop1.labels.AddRange(codes[i - 2].labels);
                        codes[i - 2] = nop1;

                        var nop2 = new CodeInstruction(OpCodes.Nop);
                        nop2.labels.AddRange(codes[i - 1].labels);
                        codes[i - 1] = nop2;

                        var nop3 = new CodeInstruction(OpCodes.Nop);
                        nop3.labels.AddRange(codes[i].labels);
                        codes[i] = nop3;

                        Logger.Debug($"Removed base.OnMainWindowLoaded call (indices {i-2} to {i})");
                        patchedBaseCall = true;
                    }
                    else
                    {
                        Logger.Warning($"WARNING: Found base.OnMainWindowLoaded but preceding instructions don't match expected pattern");
                        var nop = new CodeInstruction(OpCodes.Nop);
                        nop.labels.AddRange(codes[i].labels);
                        codes[i] = nop;
                        patchedBaseCall = true;
                    }
                    continue;
                }
            }

            if (!patchedPackageCheck)
            {
                Logger.Warning($"WARNING: Could not find HasPreviousPackageInstalled call to patch");
            }

            if (!patchedBaseCall)
            {
                Logger.Warning($"WARNING: Could not find base.OnMainWindowLoaded call to patch");
            }

            return codes.AsEnumerable();
        }
    }
}
