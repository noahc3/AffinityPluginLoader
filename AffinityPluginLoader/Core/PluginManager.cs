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
        private static List<PluginInfo> _loadedPlugins = new List<PluginInfo>();
        private static Dictionary<string, SettingsStore> _settingsStores = new Dictionary<string, SettingsStore>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, AffinityPlugin> _pluginInstances = new Dictionary<string, AffinityPlugin>(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized = false;
        private static string _configDirectory;

        public static IReadOnlyList<PluginInfo> LoadedPlugins => _loadedPlugins.AsReadOnly();

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

        public static IReadOnlyDictionary<string, SettingsStore> PluginSettings => _settingsStores;

        public static void Initialize(Harmony harmony)
        {
            if (_initialized)
                return;

            Logger.Info($"PluginManager initializing...");

            // Determine config directory: <install-dir>/apl/config
            var loaderPath = Assembly.GetExecutingAssembly().Location;
            var loaderDir = Path.GetDirectoryName(loaderPath);
            _configDirectory = Path.Combine(loaderDir, "apl", "config");

            // Add APL itself as the first plugin
            var loaderAssembly = Assembly.GetExecutingAssembly();
            var loaderProductAttr = loaderAssembly.GetCustomAttribute<AssemblyProductAttribute>();
            var loaderVersionAttr = loaderAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var loaderCompanyAttr = loaderAssembly.GetCustomAttribute<AssemblyCompanyAttribute>();
            var loaderDescAttr = loaderAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>();

            var loaderInfo = new PluginInfo
            {
                Name = loaderProductAttr?.Product ?? loaderAssembly.GetName().Name,
                Version = FormatVersion(loaderVersionAttr?.InformationalVersion, loaderAssembly.GetName().Version),
                Author = loaderCompanyAttr?.Company ?? "Unknown",
                AssemblyName = loaderAssembly.FullName,
                Description = loaderDescAttr?.Description ?? "",
                PluginId = "apl"
            };
            _loadedPlugins.Add(loaderInfo);
            Logger.Info($"Added APL to plugin list: {loaderInfo.Name} v{loaderInfo.Version}");

            // Register APL's own settings (dogfooding the Preferences API)
            RegisterSettings(AplSettings.PluginId, AplSettings.CreateDefinition());

            // Apply loader's own patches (version strings, preferences tab)
            Patches.LoaderPatches.ApplyPatches(harmony, loaderInfo);

            // Load plugins from ./apl/plugins/ directory
            LoadPlugins(harmony);

            _initialized = true;
            Logger.Info($"PluginManager initialized with {_loadedPlugins.Count} plugins");
        }

        /// <summary>
        /// Register settings for a plugin (called by APL for its own settings, or during plugin load).
        /// </summary>
        public static void RegisterSettings(string pluginId, PluginSettingsDefinition definition)
        {
            if (definition == null)
                return;

            var store = new SettingsStore(definition, _configDirectory);
            store.AssignSections();
            store.Load();
            store.Save(); // Write defaults to disk so users can edit the TOML manually
            _settingsStores[pluginId] = store;

            // Update PluginInfo
            var info = _loadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
            if (info != null)
                info.HasSettings = true;

            Logger.Info($"Registered settings for plugin: {pluginId}");
        }

        private static void LoadPlugins(Harmony harmony)
        {
            try
            {
                var loaderPath = Assembly.GetExecutingAssembly().Location;
                var loaderDir = Path.GetDirectoryName(loaderPath);
                var pluginsDir = Path.Combine(loaderDir, "apl", "plugins");

                Logger.Debug($"Looking for plugins in: {pluginsDir}");

                if (!Directory.Exists(pluginsDir))
                {
                    Logger.Info($"Plugins directory not found, creating it...");
                    Directory.CreateDirectory(pluginsDir);
                    return;
                }

                var pluginFiles = Directory.GetFiles(pluginsDir, "*.dll");
                Logger.Debug($"Found {pluginFiles.Length} DLL files in plugins directory");

                foreach (var pluginFile in pluginFiles)
                {
                    try
                    {
                        LoadPlugin(pluginFile, harmony);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to load plugin {Path.GetFileName(pluginFile)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading plugins", ex);
            }
        }

        private static void LoadPlugin(string pluginPath, Harmony harmony)
        {
            Logger.Debug($"Loading plugin: {Path.GetFileName(pluginPath)}");

            var assembly = Assembly.LoadFrom(pluginPath);
            var productAttr = assembly.GetCustomAttribute<AssemblyProductAttribute>();
            var versionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var companyAttr = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
            var descAttr = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();

            var pluginName = productAttr?.Product ?? assembly.GetName().Name;
            var pluginId = pluginName.ToLowerInvariant().Replace(' ', '-');

            var pluginInfo = new PluginInfo
            {
                Name = pluginName,
                Version = FormatVersion(versionAttr?.InformationalVersion, assembly.GetName().Version),
                Author = companyAttr?.Company ?? "Unknown",
                AssemblyName = assembly.FullName,
                Description = descAttr?.Description ?? "",
                PluginId = pluginId
            };

            // Find AffinityPlugin implementations
            var pluginTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(AffinityPlugin).IsAssignableFrom(t))
                .ToList();

            if (pluginTypes.Any())
            {
                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        var plugin = (AffinityPlugin)Activator.CreateInstance(pluginType);
                        plugin.Initialize(harmony);
                        Logger.Info($"Initialized plugin: {pluginType.Name}");

                        _pluginInstances[pluginId] = plugin;

                        // Wire up settings
                        var settingsDef = plugin.DefineSettings();
                        if (settingsDef != null)
                            RegisterSettings(pluginId, settingsDef);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to initialize {pluginType.Name}: {ex.Message}");
                    }
                }
            }
            else
            {
                Logger.Info($"No AffinityPlugin implementation found in {Path.GetFileName(pluginPath)}");
            }

            _loadedPlugins.Add(pluginInfo);
            Logger.Info($"Plugin loaded: {pluginInfo.Name} v{pluginInfo.Version} by {pluginInfo.Author}");
        }

        /// <summary>
        /// Format version string, truncating git hash to 8 chars if present
        /// </summary>
        private static string FormatVersion(string informationalVersion, Version assemblyVersion)
        {
            if (!string.IsNullOrEmpty(informationalVersion))
            {
                int plusIndex = informationalVersion.IndexOf('+');
                if (plusIndex > 0 && plusIndex < informationalVersion.Length - 1)
                {
                    string version = informationalVersion.Substring(0, plusIndex);
                    string hash = informationalVersion.Substring(plusIndex + 1);
                    if (hash.Length > 8)
                        hash = hash.Substring(0, 8);
                    return $"{version}+{hash}";
                }
                return informationalVersion;
            }
            return assemblyVersion?.ToString() ?? "0.0.0";
        }
    }
}
