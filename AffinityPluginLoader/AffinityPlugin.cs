using HarmonyLib;
using AffinityPluginLoader.Settings;

namespace AffinityPluginLoader
{
    /// <summary>
    /// Base class for all APL plugins. Override stage methods to participate in the loading pipeline.
    /// </summary>
    public abstract class AffinityPlugin
    {
        /// <summary>
        /// Stage 0: Called immediately after plugin discovery and settings init.
        /// No Affinity types are available yet. Use for early setup that doesn't touch Affinity code.
        /// </summary>
        public virtual void OnLoad(IPluginContext context) { }

        /// <summary>
        /// Stage 1: Called when Serif assemblies are loaded, before Affinity's OnStartup() runs.
        /// Apply Harmony patches here. APL settings are available via context.Settings.
        /// </summary>
        public virtual void OnPatch(Harmony harmony, IPluginContext context) { }

        /// <summary>
        /// Stage 2: Called after Affinity's InitialiseServices() completes.
        /// All Affinity services and settings are available in the DI container.
        /// </summary>
        public virtual void OnServicesReady(IPluginContext context) { }

        /// <summary>
        /// Stage 3: Called after Affinity's OnServicesInitialised() completes.
        /// Full runtime is available including native engine, tools, and effects.
        /// </summary>
        public virtual void OnReady(IPluginContext context) { }

        /// <summary>
        /// Stage 4: Called after the main window is loaded and visible.
        /// Full UI tree is available for custom panels, dialogs, and UI modifications.
        /// </summary>
        public virtual void OnUiReady(IPluginContext context) { }

        /// <summary>
        /// Stage 5: Called after startup is fully complete — splash hidden, app idle.
        /// Safe to show dialogs, toasts, or do any work that should wait until the user
        /// is looking at a fully loaded application.
        /// </summary>
        public virtual void OnStartupComplete(IPluginContext context) { }

        /// <summary>
        /// Override to define configuration options for this plugin.
        /// Returns null by default (no settings / no preferences tab).
        /// Called during Stage 0 before OnLoad.
        /// </summary>
        public virtual PluginSettingsDefinition DefineSettings() => null;

        /// <summary>
        /// Override to provide custom XAML for the plugin's preferences tab.
        /// When null (default), the preferences page is auto-generated from DefineSettings().
        /// </summary>
        public virtual string GetCustomPreferencesXaml() => null;
    }
}
