using System;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace WineFix
{
    /// <summary>
    /// WineFix Plugin - Bug fixes for running Affinity under Wine
    /// </summary>
    public class WineFixPlugin : AffinityPluginLoader.IAffinityPlugin
    {
        public void Initialize(Harmony harmony)
        {
            try
            {
                Logger.Info($"WineFix plugin initializing...");

                // Apply Wine compatibility patches
                Patches.MainWindowLoadedPatch.ApplyPatches(harmony);

                Logger.Info($"WineFix plugin initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Error initializing WineFix", ex);
            }
        }
    }
}
