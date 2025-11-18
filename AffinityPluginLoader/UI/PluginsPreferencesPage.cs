using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using AffinityPluginLoader.Core;

namespace AffinityPluginLoader.UI
{
    /// <summary>
    /// Plugins preferences page - placeholder class to avoid early binding
    /// Actual page is created dynamically by PluginsPreferencesPageFactory
    /// </summary>
    public class PluginsPreferencesPage
    {
        // This class is only used as a type reference
        // The actual UI is created dynamically to avoid loading Serif.Affinity.dll at injection time
    }

    /// <summary>
    /// Factory for creating the preferences page dynamically
    /// Avoids compile-time dependency on Serif.Affinity.UI.Dialogs.Preferences.PreferencesPage
    /// </summary>
    internal static class PluginsPreferencesPageFactory
    {
        public static object CreatePage()
        {
            try
            {
                Logger.Debug($"Creating PluginsPreferencesPage");

                // Find Serif.Affinity assembly (it's already loaded in the Affinity process)
                var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

                if (serifAssembly == null)
                {
                    Logger.Error($"ERROR: Serif.Affinity not found");
                    return null;
                }

                // Get PreferencesPage base type
                var preferencesPageType = serifAssembly.GetType("Serif.Affinity.UI.Dialogs.Preferences.PreferencesPage");
                if (preferencesPageType == null)
                {
                    Logger.Error($"ERROR: PreferencesPage type not found");
                    return null;
                }
                
                // Load XAML first
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "AffinityPluginLoader.UI.PluginsPreferencesPage.xaml";
                Grid content = null;
                
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var xaml = reader.ReadToEnd();
                            content = (Grid)XamlReader.Parse(xaml);
                        }
                    }
                }
                
                if (content == null)
                {
                    Logger.Error($"ERROR: Could not load XAML");
                    return null;
                }
                
                // Now create PreferencesPage instance and copy content into it
                var grid = (Grid)Activator.CreateInstance(preferencesPageType);
                
                // Copy properties from XAML grid
                grid.Margin = content.Margin;
                
                // Copy RowDefinitions (create new instances, don't move them)
                foreach (RowDefinition rowDef in content.RowDefinitions)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = rowDef.Height });
                }
                
                // Move children from XAML grid to our PreferencesPage grid
                while (content.Children.Count > 0)
                {
                    var child = content.Children[0];
                    content.Children.RemoveAt(0);
                    grid.Children.Add(child);
                }
                
                // Find and populate the ListBox
                var pluginListBox = FindName(grid, "PluginListBox") as ListBox;
                if (pluginListBox != null)
                {
                    pluginListBox.ItemsSource = Core.PluginManager.LoadedPlugins;
                }
                
                // Set PageName property via reflection
                var pageNameProperty = preferencesPageType.GetProperty("PageName");
                if (pageNameProperty != null)
                {
                    pageNameProperty.SetValue(grid, "AffinityPluginLoader");
                }

                Logger.Debug($"PluginsPreferencesPage created successfully");
                return grid;
            }
            catch (Exception ex)
            {
                Logger.Error("Error creating PluginsPreferencesPage", ex);
                return null;
            }
        }

        private static UIElement FindName(DependencyObject parent, string name)
        {
            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name)
                    return fe;
                
                var result = FindName(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
