using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using AffinityPluginLoader.Core;
using AffinityPluginLoader.UI;

namespace AffinityPluginLoader.Patches
{
    public static class PreferencesPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            try
            {
                Logger.Info($"Applying PreferencesDialog patches");

                var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

                if (serifAssembly == null)
                {
                    Logger.Error($"ERROR: Serif.Affinity assembly not found for preferences patch");
                    return;
                }

                var preferencesDialogType = serifAssembly.GetType("Serif.Affinity.UI.Dialogs.Preferences.PreferencesDialog");
                if (preferencesDialogType == null)
                {
                    Logger.Error($"ERROR: PreferencesDialog type not found");
                    return;
                }

                // Patch constructor to inject tabs
                var constructor = preferencesDialogType.GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(Type) },
                    null);

                if (constructor != null)
                {
                    var postfix = typeof(PreferencesPatches).GetMethod(nameof(PreferencesDialog_Constructor_Postfix), BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(constructor, postfix: new HarmonyMethod(postfix));
                    Logger.Info($"Patched PreferencesDialog constructor");
                }
                else
                {
                    Logger.Error($"ERROR: PreferencesDialog constructor not found");
                }

                // Patch OnClosed to save settings when dialog closes
                var onClosed = preferencesDialogType.GetMethod("OnClosed", BindingFlags.NonPublic | BindingFlags.Instance);
                if (onClosed != null)
                {
                    var savePostfix = typeof(PreferencesPatches).GetMethod(nameof(OnClosed_Postfix), BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(onClosed, postfix: new HarmonyMethod(savePostfix));
                    Logger.Info($"Patched PreferencesDialog.OnClosed for settings save");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply preferences patches", ex);
            }
        }

        public static void PreferencesDialog_Constructor_Postfix(object __instance)
        {
            try
            {
                var dialogType = __instance.GetType();
                var pagesProperty = dialogType.GetProperty("Pages", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (pagesProperty == null) return;

                var pages = pagesProperty.GetValue(__instance);
                if (pages is not IList pageList) return;

                Logger.Debug($"Found Pages property with {pageList.Count} existing pages");

                var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");
                if (serifAssembly == null) return;

                var separatorType = serifAssembly.GetType("Serif.Affinity.UI.Dialogs.Preferences.PreferencesPageSeparator");
                var preferencesPageType = serifAssembly.GetType("Serif.Affinity.UI.Dialogs.Preferences.PreferencesPage");

                // Add separator before APL tabs
                AddSeparator(pageList, separatorType);

                // Inject a tab for each plugin that has settings
                foreach (var kvp in PluginManager.PluginSettings)
                {
                    var pluginId = kvp.Key;
                    var store = kvp.Value;

                    // Find the plugin info for the display name
                    var pluginInfo = PluginManager.LoadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
                    var pageName = pluginInfo?.Name ?? pluginId;

                    // Check if plugin provides custom XAML
                    var pluginInstance = PluginManager.GetPluginInstance(pluginId);
                    string customXaml = pluginInstance?.GetCustomPreferencesXaml();

                    object page;
                    if (customXaml != null)
                    {
                        page = CreateCustomXamlPage(customXaml, pageName, preferencesPageType);
                    }
                    else
                    {
                        // Find the definition - re-create it from the plugin or use APL's
                        var definition = pluginInstance?.DefineSettings();
                        if (definition == null && pluginId == AplSettings.PluginId)
                            definition = AplSettings.CreateDefinition();
                        if (definition == null) continue;

                        page = PluginPreferencesPageFactory.CreatePage(pageName, definition, store);
                    }

                    if (page != null)
                    {
                        SetIndex(page, pageList.Count);
                        pageList.Add(page);
                        Logger.Info($"Added preferences tab: {pageName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error in PreferencesDialog postfix", ex);
            }
        }

        /// <summary>
        /// Postfix on OnClosed — saves all plugin settings to disk.
        /// </summary>
        public static void OnClosed_Postfix()
        {
            try
            {
                foreach (var kvp in PluginManager.PluginSettings)
                {
                    kvp.Value.Save();
                    Logger.Debug($"Saved settings for plugin: {kvp.Key}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error saving settings on ApplyChanges", ex);
            }
        }

        // ── Helpers ──

        private static void AddSeparator(IList pageList, Type separatorType)
        {
            if (separatorType == null) return;
            try
            {
                var separator = Activator.CreateInstance(separatorType);
                SetIndex(separator, pageList.Count);
                pageList.Add(separator);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Could not add separator: {ex.Message}");
            }
        }

        private static void SetIndex(object page, int index)
        {
            page.GetType().GetProperty("Index")?.SetValue(page, index);
        }

        private static object CreateCustomXamlPage(string xaml, string pageName, Type preferencesPageType)
        {
            try
            {
                if (preferencesPageType == null) return null;

                var page = (System.Windows.Controls.Grid)Activator.CreateInstance(preferencesPageType);
                preferencesPageType.GetProperty("PageName")?.SetValue(page, pageName);

                var content = System.Windows.Markup.XamlReader.Parse(xaml) as System.Windows.Controls.Grid;
                if (content == null) return null;

                // Move children from parsed XAML into the PreferencesPage grid
                while (content.Children.Count > 0)
                {
                    var child = content.Children[0];
                    content.Children.RemoveAt(0);
                    page.Children.Add(child);
                }

                return page;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating custom XAML page '{pageName}'", ex);
                return null;
            }
        }
    }
}
