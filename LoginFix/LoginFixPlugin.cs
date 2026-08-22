using HarmonyLib;
using AffinityPluginLoader;
using AffinityPluginLoader.Settings;

namespace LoginFix
{
    /// <summary>
    /// Works around a Wine WinRT gap that crashes the Canva sign-in callback.
    /// </summary>
    public class LoginFixPlugin : AffinityPlugin
    {
        public const string PluginId = "loginfix";

        public override PluginSettingsDefinition DefineSettings()
        {
            return new PluginSettingsDefinition(PluginId);
        }

        public override void OnPatch(Harmony harmony, IPluginContext context)
        {
            context.Patch("ProcessCommandLineArguments fix",
                h => Patches.ProcessCommandLineArgumentsPatch.ApplyPatches(h));
        }
    }
}
