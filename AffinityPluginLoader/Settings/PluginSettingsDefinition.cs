using System.Collections.Generic;

namespace AffinityPluginLoader.Settings
{
    /// <summary>
    /// Defines the settings and layout elements for a plugin's preferences page.
    /// Use the fluent Add* methods to build the ordered list of elements.
    /// </summary>
    public class PluginSettingsDefinition
    {
        public string PluginId { get; }
        public List<ISettingsElement> Elements { get; } = new List<ISettingsElement>();

        public PluginSettingsDefinition(string pluginId)
        {
            PluginId = pluginId;
        }

        public PluginSettingsDefinition AddSection(string title)
        {
            Elements.Add(new SectionHeader { Title = title });
            return this;
        }

        public PluginSettingsDefinition AddInlineText(string text)
        {
            Elements.Add(new InlineText { Text = text });
            return this;
        }

        public PluginSettingsDefinition AddInlineMutedText(string text)
        {
            Elements.Add(new InlineMutedText { Text = text });
            return this;
        }

        public PluginSettingsDefinition AddInlineXaml(string xaml, object dataContext = null)
        {
            Elements.Add(new InlineXaml { Xaml = xaml, DataContext = dataContext });
            return this;
        }

        public PluginSettingsDefinition AddBool(string key, string displayName,
            bool defaultValue = false, string description = null, bool restartRequired = false)
        {
            Elements.Add(new BoolSetting
            {
                Key = key,
                DisplayName = displayName,
                DefaultValue = defaultValue,
                Description = description,
                RestartRequired = restartRequired
            });
            return this;
        }

        public PluginSettingsDefinition AddString(string key, string displayName,
            string defaultValue = "", string description = null, bool restartRequired = false)
        {
            Elements.Add(new StringSetting
            {
                Key = key,
                DisplayName = displayName,
                DefaultValue = defaultValue,
                Description = description,
                RestartRequired = restartRequired
            });
            return this;
        }

        public PluginSettingsDefinition AddEnum(string key, string displayName,
            List<EnumOption> options, string defaultValue = null,
            string description = null, bool restartRequired = false)
        {
            Elements.Add(new EnumSetting
            {
                Key = key,
                DisplayName = displayName,
                Options = options,
                DefaultValue = defaultValue ?? (options.Count > 0 ? options[0].Value : ""),
                Description = description,
                RestartRequired = restartRequired
            });
            return this;
        }

        public PluginSettingsDefinition AddSlider(string key, string displayName,
            double minimum, double maximum, double defaultValue = 0,
            int precision = 0, string description = null, bool restartRequired = false)
        {
            Elements.Add(new SliderSetting
            {
                Key = key,
                DisplayName = displayName,
                Minimum = minimum,
                Maximum = maximum,
                DefaultValue = defaultValue,
                Precision = precision,
                Description = description,
                RestartRequired = restartRequired
            });
            return this;
        }

        public PluginSettingsDefinition AddDropdownSlider(string key, string displayName,
            double minimum, double maximum, double defaultValue = 0,
            int precision = 0, string description = null, bool restartRequired = false)
        {
            Elements.Add(new DropdownSliderSetting
            {
                Key = key,
                DisplayName = displayName,
                Minimum = minimum,
                Maximum = maximum,
                DefaultValue = defaultValue,
                Precision = precision,
                Description = description,
                RestartRequired = restartRequired
            });
            return this;
        }

        /// <summary>
        /// Returns all SettingDefinition elements (for serialization/store purposes).
        /// </summary>
        public IEnumerable<SettingDefinition> GetSettings()
        {
            foreach (var element in Elements)
            {
                if (element is SettingDefinition setting)
                    yield return setting;
            }
        }
    }
}
