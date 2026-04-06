using System;
using HarmonyLib;
using AffinityPluginLoader.Core;
using AffinityPluginLoader.Settings;

namespace AffinityPluginLoader
{
    internal class PluginContext : IPluginContext
    {
        private readonly string _pluginId;

        public SettingsStore Settings { get; }
        public Harmony Harmony { get; }
        public LoadStage CurrentStage { get; internal set; }

        public PluginContext(string pluginId, Harmony harmony, SettingsStore settings)
        {
            _pluginId = pluginId;
            Harmony = harmony;
            Settings = settings;
        }

        public void Patch(string description, Action<Harmony> patchAction)
        {
            var label = $"{_pluginId}:{description}";
            var harmony = Harmony;
            PluginManager.RunWithAutoDefer(label, () => patchAction(harmony));
        }

        public void Log(string message) => Logger.Info($"[{_pluginId}] {message}");
        public void LogWarning(string message) => Logger.Warning($"[{_pluginId}] {message}");
        public void LogError(string message, Exception ex) => Logger.Error($"[{_pluginId}] {message}", ex);
    }
}
