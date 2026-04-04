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
        public const string ForceWpfControls = "force_wpf_controls";

        // Plugin list XAML shown via InlineXaml element
        private const string PluginListXaml = @"
<Border xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        BorderThickness=""0""
        Margin=""10,1,10,10"">
    <ListBox BorderThickness=""0"" Padding=""4""
             ItemsSource=""{Binding}"">
        <ListBox.ItemContainerStyle>
            <Style TargetType=""ListBoxItem"">
                <Setter Property=""Template"">
                    <Setter.Value>
                        <ControlTemplate TargetType=""ListBoxItem"">
                            <StackPanel Margin=""5"">
                                <TextBlock Text=""{Binding Name}""
                                           FontWeight=""Bold"" FontSize=""11""
                                           Foreground=""White""/>
                                <TextBlock Foreground=""White"" FontSize=""9"" Margin=""0,2,0,0"">
                                    <Run Text=""Version: ""/><Run Text=""{Binding Version}""/>
                                </TextBlock>
                                <TextBlock Foreground=""White"" FontSize=""9"" Margin=""0,2,0,0"">
                                    <Run Text=""Author: ""/><Run Text=""{Binding Author}""/>
                                </TextBlock>
                                <TextBlock Foreground=""LightGray"" FontSize=""9"" Margin=""0,2,0,0"">
                                    <Run Text=""{Binding Description}""/>
                                </TextBlock>
                            </StackPanel>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>
        </ListBox.ItemContainerStyle>
    </ListBox>
</Border>";

        public static PluginSettingsDefinition CreateDefinition()
        {
            return new PluginSettingsDefinition(PluginId)
                .AddSection("Loaded Plugins")
                .AddInlineXaml(PluginListXaml, PluginManager.LoadedPlugins)
                .AddSection("Advanced")
                .AddBool(ForceWpfControls, "Force WPF fallback controls",
                    description: "Use standard WPF controls instead of Affinity's built-in controls for plugin preferences pages.",
                    defaultValue: false);
        }
    }
}
