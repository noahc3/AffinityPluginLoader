using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace AffinityPluginLoader.Core
{
    /// <summary>
    /// Manages loading and tracking of Affinity plugins
    /// </summary>
    public static class PluginManager
    {
        private static List<PluginInfo> _loadedPlugins = new List<PluginInfo>();
        private static bool _initialized = false;

        public static IReadOnlyList<PluginInfo> LoadedPlugins => _loadedPlugins.AsReadOnly();

        public static void Initialize(Harmony harmony)
        {
            if (_initialized)
                return;

            Logger.Info($"PluginManager initializing...");

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
                Description = loaderDescAttr?.Description ?? ""
            };
            _loadedPlugins.Add(loaderInfo);
            Logger.Info($"Added APL to plugin list: {loaderInfo.Name} v{loaderInfo.Version}");

            // Apply loader's own patches (version strings, preferences tab)
            Patches.LoaderPatches.ApplyPatches(harmony, loaderInfo);

            // Load plugins from ./plugins/ directory
            LoadPlugins(harmony);

            _initialized = true;
            Logger.Info($"PluginManager initialized with {_loadedPlugins.Count} plugins");
        }

        private static void LoadPlugins(Harmony harmony)
        {
            try
            {
                // Get the directory where AffinityPluginLoader.dll is located
                string loaderPath = Assembly.GetExecutingAssembly().Location;
                string loaderDir = Path.GetDirectoryName(loaderPath);
                string pluginsDir = Path.Combine(loaderDir, "plugins");

                Logger.Debug($"Looking for plugins in: {pluginsDir}");

                if (!Directory.Exists(pluginsDir))
                {
                    Logger.Info($"Plugins directory not found, creating it...");
                    Directory.CreateDirectory(pluginsDir);
                    return;
                }

                // Load all DLLs in the plugins directory
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

            // Load the assembly
            var assembly = Assembly.LoadFrom(pluginPath);
            var productAttr = assembly.GetCustomAttribute<AssemblyProductAttribute>();
            var versionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var companyAttr = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
            var descAttr = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();

            var pluginInfo = new PluginInfo
            {
                Name = productAttr?.Product ?? assembly.GetName().Name,
                Version = FormatVersion(versionAttr?.InformationalVersion, assembly.GetName().Version),
                Author = companyAttr?.Company ?? "Unknown",
                AssemblyName = assembly.FullName,
                Description = descAttr?.Description ?? ""
            };

            // Look for IAffinityPlugin interface implementation
            var pluginTypes = assembly.GetTypes()
                .Where(t => t.GetInterface("IAffinityPlugin") != null)
                .ToList();

            if (pluginTypes.Any())
            {
                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        var plugin = Activator.CreateInstance(pluginType) as IAffinityPlugin;
                        plugin?.Initialize(harmony);
                        Logger.Info($"Initialized plugin: {pluginType.Name}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to initialize {pluginType.Name}: {ex.Message}");
                    }
                }
            }
            else
            {
                Logger.Info($"No IAffinityPlugin implementation found, plugin loaded but not initialized");
            }

            _loadedPlugins.Add(pluginInfo);
            Logger.Info($"Plugin loaded: {pluginInfo.Name} v{pluginInfo.Version} by {pluginInfo.Author}");
        }

        /// <summary>
        /// Format version string, truncating git hash to 8 chars if present
        /// </summary>
        private static string FormatVersion(string informationalVersion, Version assemblyVersion)
        {
            // If we have an informational version, process it
            if (!string.IsNullOrEmpty(informationalVersion))
            {
                // Check if it contains a git hash (format: "version+hash")
                int plusIndex = informationalVersion.IndexOf('+');
                if (plusIndex > 0 && plusIndex < informationalVersion.Length - 1)
                {
                    string version = informationalVersion.Substring(0, plusIndex);
                    string hash = informationalVersion.Substring(plusIndex + 1);

                    // Truncate hash to 8 chars if longer
                    if (hash.Length > 8)
                    {
                        hash = hash.Substring(0, 8);
                    }

                    return $"{version}+{hash}";
                }

                // No git hash, return as-is
                return informationalVersion;
            }

            // Fallback to assembly version
            return assemblyVersion?.ToString() ?? "0.0.0";
        }
    }

    /// <summary>
    /// Information about a loaded plugin
    /// </summary>
    public class PluginInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string AssemblyName { get; set; }
        public string Description { get; set; }
    }
}
