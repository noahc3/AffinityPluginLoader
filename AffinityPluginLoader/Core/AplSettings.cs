using System.Collections.Generic;
using AffinityPluginLoader.Settings;

namespace AffinityPluginLoader.Core
{
    /// <summary>
    /// Defines APL's own settings, dogfooding the Preferences API.
    /// </summary>
    internal static class AplSettings
    {
        public const string PluginId = "apl";

        // Setting keys
        public const string FileLogging = "file_logging";
        public const string LogLevel = "log_level";
        public const string ForceWpfControls = "force_wpf_controls";

        // Plugin list XAML — each plugin rendered as its own PanelStyle box
        private const string PluginListXaml = @"
<ItemsControl xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
              xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
              xmlns:schemes=""clr-namespace:Serif.Affinity.Resources.Schemes;assembly=Serif.Affinity""
              ItemsSource=""{Binding}""
              Background=""#00FFFFFF"" BorderThickness=""0"" IsTabStop=""False"">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Border Margin=""10,1,10,1""
                    Background=""{DynamicResource {x:Static schemes:SchemeManager.Brush_DialogBackground}}""
                    CornerRadius=""3"">
                <Grid Margin=""10"">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width=""*"" />
                        <ColumnDefinition Width=""Auto"" />
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition Height=""Auto"" />
                        <RowDefinition Height=""Auto"" />
                        <RowDefinition Height=""Auto"" />
                    </Grid.RowDefinitions>
                    <StackPanel Orientation=""Horizontal"">
                        <TextBlock FontWeight=""Bold"" Text=""{Binding Name}""
                                   Foreground=""{DynamicResource {x:Static schemes:SchemeManager.Brush_BaseForeground}}"" />
                        <TextBlock Margin=""6,0,0,0"" FontSize=""9"" VerticalAlignment=""Center""
                                   Text=""{Binding Version, StringFormat=v{0}}""
                                   Foreground=""{DynamicResource {x:Static schemes:SchemeManager.Brush_LabelForeground}}"" />
                    </StackPanel>
                    <TextBlock Grid.Row=""1"" Text=""{Binding Author}"" FontSize=""9""
                               Foreground=""{DynamicResource {x:Static schemes:SchemeManager.Brush_LabelForeground}}"" />
                    <TextBlock Grid.Row=""2"" Grid.ColumnSpan=""2"" TextWrapping=""Wrap""
                               Margin=""0,8,0,0""
                               Text=""{Binding Description}""
                               Foreground=""{DynamicResource {x:Static schemes:SchemeManager.Brush_BaseForeground}}"" />
                </Grid>
            </Border>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>";

        public static PluginSettingsDefinition CreateDefinition()
        {
            return new PluginSettingsDefinition(PluginId)
                .AddSection("Loaded Plugins")
                .AddInlineXaml(PluginListXaml, PluginManager.LoadedPlugins)
                .AddSection("Logging")
                .AddBool(FileLogging, "Enable logging to file",
                    defaultValue: false,
                    description: "Write APL and plugin log output to plugins/logs/apl.latest.log.")
                .AddEnum(LogLevel, "Log level",
                    new List<EnumOption>
                    {
                        new EnumOption("DEBUG", "Debug"),
                        new EnumOption("INFO", "Info"),
                        new EnumOption("WARNING", "Warning"),
                        new EnumOption("ERROR", "Error"),
                        new EnumOption("NONE", "None"),
                    },
                    defaultValue: "INFO",
                    description: "Minimum severity level for log messages.")
                .AddSection("Advanced")
                .AddBool(ForceWpfControls, "Force WPF fallback controls",
                    description: "Use standard WPF controls instead of Affinity's built-in controls for plugin preferences pages.",
                    defaultValue: false);
        }
    }
}
