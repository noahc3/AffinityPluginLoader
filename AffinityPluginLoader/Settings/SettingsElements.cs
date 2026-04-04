using System.Collections.Generic;

namespace AffinityPluginLoader.Settings
{
    /// <summary>
    /// Marker interface for all elements that can appear in a plugin's preferences page.
    /// </summary>
    public interface ISettingsElement { }

    // ── Organizational elements (no stored value) ──

    public class SectionHeader : ISettingsElement
    {
        public string Title { get; set; }
    }

    public class InlineText : ISettingsElement
    {
        public string Text { get; set; }
    }

    public class InlineMutedText : ISettingsElement
    {
        public string Text { get; set; }
    }

    public class InlineXaml : ISettingsElement
    {
        public string Xaml { get; set; }
        public object DataContext { get; set; }
    }

    // ── Setting base ──

    public abstract class SettingDefinition : ISettingsElement
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool RestartRequired { get; set; }
        public string Section { get; set; }
    }

    // ── Typed settings ──

    public class BoolSetting : SettingDefinition
    {
        public bool DefaultValue { get; set; }
    }

    public class StringSetting : SettingDefinition
    {
        public string DefaultValue { get; set; } = "";
    }

    public class EnumSetting : SettingDefinition
    {
        public List<EnumOption> Options { get; set; } = new List<EnumOption>();
        public string DefaultValue { get; set; }
    }

    public class EnumOption
    {
        public string Value { get; set; }
        public string DisplayName { get; set; }

        public EnumOption() { }
        public EnumOption(string value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }
    }

    public class SliderSetting : SettingDefinition
    {
        public double DefaultValue { get; set; }
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public int Precision { get; set; }
    }

    public class DropdownSliderSetting : SettingDefinition
    {
        public double DefaultValue { get; set; }
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public int Precision { get; set; }
    }
}
