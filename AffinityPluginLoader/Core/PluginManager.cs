using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using AffinityPluginLoader.Settings;

namespace AffinityPluginLoader.Core
{
    public class PluginInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string AssemblyName { get; set; }
        public string Description { get; set; }
        public bool HasSettings { get; set; }
        public string PluginId { get; set; }
    }

    public static class PluginManager
    {
        private static readonly List<PluginInfo> _loadedPlugins = new List<PluginInfo>();
        private static readonly Dictionary<string, SettingsStore> _settingsStores = new Dictionary<string, SettingsStore>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, AffinityPlugin> _pluginInstances = new Dictionary<string, AffinityPlugin>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, PluginContext> _pluginContexts = new Dictionary<string, PluginContext>(StringComparer.OrdinalIgnoreCase);
        private static string _configDirectory;
        private static Harmony _harmony;
        private static LoadStage _currentStage = (LoadStage)(-1);

        public static IReadOnlyList<PluginInfo> LoadedPlugins => _loadedPlugins.AsReadOnly();
        public static IReadOnlyDictionary<string, SettingsStore> PluginSettings => _settingsStores;
        public static LoadStage CurrentStage => _currentStage;

        public static SettingsStore GetSettingsStore(string pluginId)
        {
            _settingsStores.TryGetValue(pluginId, out var store);
            return store;
        }

        public static AffinityPlugin GetPluginInstance(string pluginId)
        {
            _pluginInstances.TryGetValue(pluginId, out var instance);
            return instance;
        }

        /// <summary>
        /// Stage 0: Discover plugins, load settings from TOML, call OnLoad.
        /// No Affinity assemblies required.
        /// </summary>
        public static void RunStageLoad()
        {
            _currentStage = LoadStage.Load;
            Logger.Info("=== Stage 0: OnLoad ===");

            _harmony = new Harmony("dev.ncuroe.affinitypluginloader");

            // Determine config directory
            var loaderPath = Assembly.GetExecutingAssembly().Location;
            var loaderDir = Path.GetDirectoryName(loaderPath);
            _configDirectory = Path.Combine(loaderDir, "apl", "config");

            // Register APL itself as a plugin
            RegisterAplAsPlugin();

            // Register APL's own settings
            RegisterSettings(AplSettings.PluginId, AplSettings.CreateDefinition());

            // Discover and load plugin DLLs (creates instances, loads settings, but does NOT patch)
            DiscoverPlugins();

            // Call OnLoad on all plugins
            foreach (var kvp in _pluginInstances)
            {
                var ctx = GetOrCreateContext(kvp.Key);
                ctx.CurrentStage = LoadStage.Load;
                try
                {
                    kvp.Value.OnLoad(ctx);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in {kvp.Key}.OnLoad", ex);
                }
            }

            Logger.Info($"Stage 0 complete: {_loadedPlugins.Count} plugins discovered, settings loaded");
        }

        /// <summary>
        /// Stage 1: Serif assemblies are loaded. Apply Harmony patches.
        /// </summary>
        public static void RunStagePatch()
        {
            _currentStage = LoadStage.Patch;
            Logger.Info("=== Stage 1: OnPatch ===");

            // Hook Affinity lifecycle methods for Stages 2-4
            RunWithAutoDefer("APL:LifecyclePatches", () =>
                Patches.LifecyclePatches.ApplyPatches(_harmony));

            // Apply APL's own patches as separate deferrable units
            var aplInfo = _loadedPlugins.FirstOrDefault(p => p.PluginId == "apl");
            RunWithAutoDefer("APL:VersionPatches", () =>
                Patches.LoaderPatches.ApplyVersionPatches(_harmony, aplInfo));
            RunWithAutoDefer("APL:PreferencesPatches", () =>
                Patches.PreferencesPatches.ApplyPatches(_harmony));

            // Call OnPatch on all plugins
            foreach (var kvp in _pluginInstances)
            {
                var id = kvp.Key;
                var plugin = kvp.Value;
                var ctx = GetOrCreateContext(id);
                ctx.CurrentStage = LoadStage.Patch;

                RunWithAutoDefer($"{id}:OnPatch", () => plugin.OnPatch(_harmony, ctx));
            }

            Logger.Info("Stage 1 complete: patches applied");
        }

        /// <summary>
        /// Run Stages 2-4. Called by lifecycle postfix patches.
        /// </summary>
        public static void RunStage(LoadStage stage)
        {
            _currentStage = stage;
            var stageName = stage.ToString();
            Logger.Info($"=== Stage {(int)stage}: {stageName} ===");

            foreach (var kvp in _pluginInstances)
            {
                var ctx = GetOrCreateContext(kvp.Key);
                ctx.CurrentStage = stage;
                try
                {
                    switch (stage)
                    {
                        case LoadStage.ServicesReady:
                            kvp.Value.OnServicesReady(ctx);
                            break;
                        case LoadStage.Ready:
                            kvp.Value.OnReady(ctx);
                            break;
                        case LoadStage.UiReady:
                            kvp.Value.OnUiReady(ctx);
                            break;
                        case LoadStage.StartupComplete:
                            kvp.Value.OnStartupComplete(ctx);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in {kvp.Key}.{stageName}", ex);
                }
            }

            Logger.Info($"Stage {(int)stage} complete");
        }

        /// <summary>
        /// Run an action. If it throws TypeLoadException (transitive dependency not loaded),
        /// automatically defer it for retry on subsequent assembly loads.
        /// Other exceptions are logged and swallowed.
        /// </summary>
        internal static void RunWithAutoDefer(string description, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex) when (EntryPoint.IsTypeLoadException(ex))
            {
                EntryPoint.AddDeferredPatch(description, action);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in {description}", ex);
            }
        }

        /// <summary>
        /// Register settings for a plugin. Creates and loads the SettingsStore from TOML.
        /// </summary>
        public static void RegisterSettings(string pluginId, PluginSettingsDefinition definition)
        {
            if (definition == null)
                return;

            var store = new SettingsStore(definition, _configDirectory);
            store.AssignSections();
            store.Load();
            store.Save(); // Write defaults so users can edit TOML manually
            _settingsStores[pluginId] = store;

            var info = _loadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
            if (info != null)
                info.HasSettings = true;

            Logger.Info($"Registered settings for plugin: {pluginId}");
        }

        // ── Private helpers ──

        private static void RegisterAplAsPlugin()
        {
            var asm = Assembly.GetExecutingAssembly();
            var product = asm.GetCustomAttribute<AssemblyProductAttribute>();
            var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var company = asm.GetCustomAttribute<AssemblyCompanyAttribute>();
            var desc = asm.GetCustomAttribute<AssemblyDescriptionAttribute>();

            _loadedPlugins.Add(new PluginInfo
            {
                Name = product?.Product ?? asm.GetName().Name,
                Version = FormatVersion(version?.InformationalVersion, asm.GetName().Version),
                Author = company?.Company ?? "Unknown",
                AssemblyName = asm.FullName,
                Description = desc?.Description ?? "",
                PluginId = "apl"
            });
        }

        private static void DiscoverPlugins()
        {
            var loaderDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var pluginsDir = Path.Combine(loaderDir, "plugins");

            if (!Directory.Exists(pluginsDir))
            {
                Logger.Info("Plugins directory not found, creating it...");
                Directory.CreateDirectory(pluginsDir);
                return;
            }

            foreach (var pluginFile in Directory.GetFiles(pluginsDir, "*.dll"))
            {
                try
                {
                    LoadPluginAssembly(pluginFile);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to load plugin {Path.GetFileName(pluginFile)}: {ex.Message}");
                }
            }
        }

        private static void LoadPluginAssembly(string pluginPath)
        {
            Logger.Debug($"Loading plugin: {Path.GetFileName(pluginPath)}");

            var assembly = Assembly.LoadFrom(pluginPath);
            var product = assembly.GetCustomAttribute<AssemblyProductAttribute>();
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
            var desc = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();

            var pluginName = product?.Product ?? assembly.GetName().Name;
            var pluginId = pluginName.ToLowerInvariant().Replace(' ', '-');

            var pluginInfo = new PluginInfo
            {
                Name = pluginName,
                Version = FormatVersion(version?.InformationalVersion, assembly.GetName().Version),
                Author = company?.Company ?? "Unknown",
                AssemblyName = assembly.FullName,
                Description = desc?.Description ?? "",
                PluginId = pluginId
            };

            // Find and instantiate AffinityPlugin implementations
            var pluginTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(AffinityPlugin).IsAssignableFrom(t))
                .ToList();

            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    var plugin = (AffinityPlugin)Activator.CreateInstance(pluginType);
                    _pluginInstances[pluginId] = plugin;

                    // Wire up settings immediately (before OnLoad)
                    var settingsDef = plugin.DefineSettings();
                    if (settingsDef != null)
                        RegisterSettings(pluginId, settingsDef);

                    Logger.Info($"Discovered plugin: {pluginType.Name}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to instantiate {pluginType.Name}: {ex.Message}");
                }
            }

            _loadedPlugins.Add(pluginInfo);
            Logger.Info($"Plugin loaded: {pluginInfo.Name} v{pluginInfo.Version} by {pluginInfo.Author}");
        }

        private static PluginContext GetOrCreateContext(string pluginId)
        {
            if (!_pluginContexts.TryGetValue(pluginId, out var ctx))
            {
                _settingsStores.TryGetValue(pluginId, out var store);
                ctx = new PluginContext(pluginId, _harmony, store);
                _pluginContexts[pluginId] = ctx;
            }
            return ctx;
        }

        internal static string FormatVersion(string informationalVersion, Version assemblyVersion)
        {
            if (!string.IsNullOrEmpty(informationalVersion))
            {
                int plusIndex = informationalVersion.IndexOf('+');
                if (plusIndex > 0 && plusIndex < informationalVersion.Length - 1)
                {
                    string ver = informationalVersion.Substring(0, plusIndex);
                    string hash = informationalVersion.Substring(plusIndex + 1);
                    if (hash.Length > 8)
                        hash = hash.Substring(0, 8);
                    return $"{ver}+{hash}";
                }
                return informationalVersion;
            }
            return assemblyVersion?.ToString() ?? "0.0.0";
        }
    }
}
