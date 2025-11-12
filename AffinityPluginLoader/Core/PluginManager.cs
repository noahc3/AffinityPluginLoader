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

            FileLog.Log($"PluginManager initializing...\n");

            // Add AffinityPluginLoader itself as the first plugin
            var loaderAssembly = Assembly.GetExecutingAssembly();
            var loaderNameAttr = loaderAssembly.GetCustomAttribute<AssemblyTitleAttribute>();
            var loaderVersionAttr = loaderAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            var loaderCompanyAttr = loaderAssembly.GetCustomAttribute<AssemblyCompanyAttribute>();
            
            var loaderInfo = new PluginInfo
            {
                Name = loaderNameAttr?.Title ?? "AffinityPluginLoader",
                Version = loaderVersionAttr?.Version ?? loaderAssembly.GetName().Version?.ToString() ?? "0.1.0.1",
                Author = loaderCompanyAttr?.Company ?? "AffinityPluginLoader",
                AssemblyName = loaderAssembly.FullName
            };
            _loadedPlugins.Add(loaderInfo);
            FileLog.Log($"Added AffinityPluginLoader to plugin list: {loaderInfo.Name} v{loaderInfo.Version}\n");

            // Apply loader's own patches (version strings, preferences tab)
            Patches.LoaderPatches.ApplyPatches(harmony);

            // Load plugins from ./plugins/ directory
            LoadPlugins(harmony);

            _initialized = true;
            FileLog.Log($"PluginManager initialized with {_loadedPlugins.Count} plugins\n");
        }

        private static void LoadPlugins(Harmony harmony)
        {
            try
            {
                // Get the directory where AffinityPluginLoader.dll is located
                string loaderPath = Assembly.GetExecutingAssembly().Location;
                string loaderDir = Path.GetDirectoryName(loaderPath);
                string pluginsDir = Path.Combine(loaderDir, "plugins");

                FileLog.Log($"Looking for plugins in: {pluginsDir}\n");

                if (!Directory.Exists(pluginsDir))
                {
                    FileLog.Log($"Plugins directory not found, creating it...\n");
                    Directory.CreateDirectory(pluginsDir);
                    return;
                }

                // Load all DLLs in the plugins directory
                var pluginFiles = Directory.GetFiles(pluginsDir, "*.dll");
                FileLog.Log($"Found {pluginFiles.Length} DLL files in plugins directory\n");

                foreach (var pluginFile in pluginFiles)
                {
                    try
                    {
                        LoadPlugin(pluginFile, harmony);
                    }
                    catch (Exception ex)
                    {
                        FileLog.Log($"Failed to load plugin {Path.GetFileName(pluginFile)}: {ex.Message}\n");
                    }
                }
            }
            catch (Exception ex)
            {
                FileLog.Log($"Error loading plugins: {ex.Message}\n{ex.StackTrace}\n");
            }
        }

        private static void LoadPlugin(string pluginPath, Harmony harmony)
        {
            FileLog.Log($"Loading plugin: {Path.GetFileName(pluginPath)}\n");

            // Load the assembly
            var assembly = Assembly.LoadFrom(pluginPath);
            
            // Get plugin metadata from assembly attributes
            var nameAttr = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
            var versionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            var companyAttr = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();

            var pluginInfo = new PluginInfo
            {
                Name = nameAttr?.Title ?? assembly.GetName().Name,
                Version = versionAttr?.Version ?? assembly.GetName().Version?.ToString() ?? "1.0.0",
                Author = companyAttr?.Company ?? "Unknown",
                AssemblyName = assembly.FullName
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
                        FileLog.Log($"  Initialized plugin: {pluginType.Name}\n");
                    }
                    catch (Exception ex)
                    {
                        FileLog.Log($"  Failed to initialize {pluginType.Name}: {ex.Message}\n");
                    }
                }
            }
            else
            {
                FileLog.Log($"  No IAffinityPlugin implementation found, plugin loaded but not initialized\n");
            }

            _loadedPlugins.Add(pluginInfo);
            FileLog.Log($"  Plugin loaded: {pluginInfo.Name} v{pluginInfo.Version} by {pluginInfo.Author}\n");
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
    }
}
