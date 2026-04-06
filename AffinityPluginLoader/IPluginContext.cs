using System;
using HarmonyLib;
using AffinityPluginLoader.Settings;

namespace AffinityPluginLoader
{
    /// <summary>
    /// Context passed to plugins at each loading stage.
    /// </summary>
    public interface IPluginContext
    {
        /// <summary>The plugin's own settings store (null if no settings defined).</summary>
        SettingsStore Settings { get; }

        /// <summary>The shared Harmony instance for this plugin.</summary>
        Harmony Harmony { get; }

        /// <summary>The current loading stage.</summary>
        LoadStage CurrentStage { get; }

        /// <summary>
        /// Apply a Harmony patch with automatic deferral. If the action throws
        /// TypeLoadException (transitive dependency not yet loaded), it is
        /// automatically retried on subsequent assembly loads.
        /// </summary>
        void Patch(string description, Action<Harmony> patchAction);

        void Log(string message);
        void LogWarning(string message);
        void LogError(string message, Exception ex = null);
    }
}
