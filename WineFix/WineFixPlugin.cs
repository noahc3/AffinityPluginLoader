using HarmonyLib;
using AffinityPluginLoader;

namespace WineFix
{
    /// <summary>
    /// WineFix Plugin - Bug fixes for running Affinity under Wine
    /// </summary>
    public class WineFixPlugin : AffinityPlugin
    {
        public override void OnPatch(Harmony harmony, IPluginContext context)
        {
            context.Patch("MainWindowLoaded fix",
                h => Patches.MainWindowLoadedPatch.ApplyPatches(h));
        }
    }
}
