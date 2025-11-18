using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using AffinityPluginLoader.Core;
using AffinityPluginLoader.UI;

namespace AffinityPluginLoader.Patches
{
    /// <summary>
    /// Patches for adding the Plugins tab to Affinity's Preferences dialog
    /// </summary>
    public static class PreferencesPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            try
            {
                Logger.Info($"Applying PreferencesDialog patches");

                // Find the Serif.Affinity assembly
                var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

                if (serifAssembly == null)
                {
                    Logger.Error($"ERROR: Serif.Affinity assembly not found for preferences patch");
                    return;
                }

                // Get the PreferencesDialog type
                var preferencesDialogType = serifAssembly.GetType("Serif.Affinity.UI.Dialogs.Preferences.PreferencesDialog");
                if (preferencesDialogType == null)
                {
                    Logger.Error($"ERROR: PreferencesDialog type not found");
                    return;
                }
                
                // Find the constructor - it takes a Type parameter with default value
                var constructor = preferencesDialogType.GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(Type) },
                    null);
                
                if (constructor != null)
                {
                    Logger.Info($"Found PreferencesDialog constructor");
                    var postfix = typeof(PreferencesPatches).GetMethod(nameof(PreferencesDialog_Constructor_Postfix), BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(constructor, postfix: new HarmonyMethod(postfix));
                    Logger.Info($"Patched PreferencesDialog constructor");
                }
                else
                {
                    Logger.Error($"ERROR: PreferencesDialog constructor not found");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply preferences patches", ex);
            }
        }

        // Postfix for PreferencesDialog constructor
        public static void PreferencesDialog_Constructor_Postfix(object __instance)
        {
            try
            {
                Logger.Debug($"PreferencesDialog constructor postfix called");

                // Get the type of the dialog
                var dialogType = __instance.GetType();

                // Find the property that holds the pages (it's called "Pages")
                var pagesProperty = dialogType.GetProperty("Pages", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (pagesProperty != null)
                {
                    var pages = pagesProperty.GetValue(__instance);
                    if (pages is System.Collections.IList pageList)
                    {
                        Logger.Debug($"Found Pages property with {pageList.Count} existing pages");
                        
                        // Add a separator before the Affinity Plugin Loader tab
                        var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");
                        
                        if (serifAssembly != null)
                        {
                            var separatorType = serifAssembly.GetType("Serif.Affinity.UI.Dialogs.Preferences.PreferencesPageSeparator");
                            if (separatorType != null)
                            {
                                var separator = Activator.CreateInstance(separatorType);
                                
                                // Set Index property for separator
                                var indexProperty = separatorType.GetProperty("Index");
                                if (indexProperty != null)
                                {
                                    indexProperty.SetValue(separator, pageList.Count);
                                }

                                pageList.Add(separator);
                                Logger.Debug($"Added separator to preferences dialog");
                            }
                            else
                            {
                                Logger.Debug($"PreferencesPageSeparator type not found, skipping separator");
                            }
                        }
                        
                        // Create plugins page using factory to avoid loading Serif.Affinity.dll early
                        var pluginsPage = PluginsPreferencesPageFactory.CreatePage();
                        
                        if (pluginsPage != null)
                        {
                            // Set Index property via reflection
                            var indexProperty = pluginsPage.GetType().GetProperty("Index");
                            if (indexProperty != null)
                            {
                                indexProperty.SetValue(pluginsPage, pageList.Count);
                            }

                            pageList.Add(pluginsPage);
                            Logger.Info($"Added Affinity Plugin Loader tab to preferences dialog");
                        }
                    }
                    else
                    {
                        Logger.Debug($"Pages property is not IList: {pages?.GetType()?.FullName}");
                    }
                }
                else
                {
                    Logger.Debug($"Could not find Pages property in PreferencesDialog");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error in PreferencesDialog postfix", ex);
            }
        }
    }
}
