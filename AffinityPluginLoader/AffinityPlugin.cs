using HarmonyLib;
using AffinityPluginLoader.Settings;

namespace AffinityPluginLoader
{
    /// <summary>
    /// Base class for all APL plugins. Extend this and override Initialize() to apply Harmony patches.
    /// Optionally override DefineSettings() to expose configuration in the preferences dialog.
    /// </summary>
    public abstract class AffinityPlugin
    {
        /// <summary>
        /// Called when the plugin is loaded. Apply Harmony patches here.
        /// </summary>
        public abstract void Initialize(Harmony harmony);

        /// <summary>
        /// Override to define configuration options for this plugin.
        /// Returns null by default (no settings / no preferences tab).
        /// </summary>
        public virtual PluginSettingsDefinition DefineSettings() => null;

        /// <summary>
        /// Override to provide custom XAML for the plugin's preferences tab.
        /// When null (default), the preferences page is auto-generated from DefineSettings().
        /// </summary>
        public virtual string GetCustomPreferencesXaml() => null;
    }
}
