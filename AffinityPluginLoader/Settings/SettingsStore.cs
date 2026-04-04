using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Tommy;
using AffinityPluginLoader.Core;

namespace AffinityPluginLoader.Settings
{
    /// <summary>
    /// Reads and writes plugin settings to a TOML file on disk.
    /// Supports environment variable overrides via APL__PLUGINID__KEY.
    /// </summary>
    public class SettingsStore
    {
        private readonly PluginSettingsDefinition _definition;
        private readonly string _filePath;
        private readonly string _envPrefix;
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public SettingsStore(PluginSettingsDefinition definition, string configDirectory)
        {
            _definition = definition;
            _filePath = Path.Combine(configDirectory, $"{definition.PluginId}.toml");
            _envPrefix = $"APL__{definition.PluginId.ToUpperInvariant()}__";
        }

        public string FilePath => _filePath;

        // ── Read/Write ──

        public void Load()
        {
            // Start with defaults
            foreach (var setting in _definition.GetSettings())
                _values[setting.Key] = GetDefaultValue(setting);

            if (!File.Exists(_filePath))
            {
                Logger.Debug($"Config file not found, using defaults: {_filePath}");
                return;
            }

            try
            {
                TomlTable table;
                using (var reader = File.OpenText(_filePath))
                    table = TOML.Parse(reader);

                foreach (var setting in _definition.GetSettings())
                {
                    var node = FindNode(table, setting);
                    if (node == null || !node.HasValue)
                        continue;

                    var parsed = ParseNode(node, setting);
                    if (parsed != null)
                        _values[setting.Key] = parsed;
                }

                Logger.Debug($"Loaded config: {_filePath}");
            }
            catch (TomlParseException ex)
            {
                Logger.Warning($"TOML parse errors in {_filePath}, using defaults for invalid values");
                // Try to use partially parsed table
                foreach (var setting in _definition.GetSettings())
                {
                    var node = FindNode(ex.ParsedTable, setting);
                    if (node == null || !node.HasValue)
                        continue;

                    var parsed = ParseNode(node, setting);
                    if (parsed != null)
                        _values[setting.Key] = parsed;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load config {_filePath}", ex);
            }
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var table = new TomlTable();
                string currentSection = null;
                TomlTable currentTable = table;

                foreach (var element in _definition.Elements)
                {
                    if (element is SectionHeader section)
                    {
                        currentSection = SanitizeTomlKey(section.Title);
                        currentTable = new TomlTable();
                        table[currentSection] = currentTable;
                        continue;
                    }

                    if (!(element is SettingDefinition setting))
                        continue;

                    var node = CreateNode(setting);
                    node.Comment = BuildComment(setting);
                    currentTable[setting.Key] = node;
                }

                using (var writer = File.CreateText(_filePath))
                {
                    table.WriteTo(writer);
                    writer.Flush();
                }

                Logger.Debug($"Saved config: {_filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save config {_filePath}", ex);
            }
        }

        // ── Value access ──

        public T GetValue<T>(string key)
        {
            if (_values.TryGetValue(key, out var value))
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);

            var setting = _definition.GetSettings().FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (setting != null)
                return (T)Convert.ChangeType(GetDefaultValue(setting), typeof(T), CultureInfo.InvariantCulture);

            return default;
        }

        public void SetValue<T>(string key, T value)
        {
            _values[key] = value;
        }

        /// <summary>
        /// Returns the effective value, considering environment variable overrides.
        /// </summary>
        public T GetEffectiveValue<T>(string key)
        {
            if (TryGetEnvValue(key, out var envValue))
            {
                try
                {
                    var setting = _definition.GetSettings().FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                    var parsed = ParseEnvString(envValue, setting);
                    if (parsed != null)
                        return (T)Convert.ChangeType(parsed, typeof(T), CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to parse env override for {key}: {ex.Message}");
                }
            }

            return GetValue<T>(key);
        }

        public bool IsOverriddenByEnv(string key)
        {
            return TryGetEnvValue(key, out _);
        }

        public string GetEnvOverrideRaw(string key)
        {
            TryGetEnvValue(key, out var value);
            return value;
        }

        public string GetEnvVarName(string key)
        {
            return _envPrefix + key.ToUpperInvariant();
        }

        // ── Private helpers ──

        private bool TryGetEnvValue(string key, out string value)
        {
            var envName = GetEnvVarName(key);
            value = Environment.GetEnvironmentVariable(envName);
            return value != null;
        }

        private object ParseEnvString(string envValue, SettingDefinition setting)
        {
            if (setting is BoolSetting)
            {
                if (bool.TryParse(envValue, out var b)) return b;
                if (envValue == "1") return true;
                if (envValue == "0") return false;
                return null;
            }
            if (setting is StringSetting)
                return envValue;
            if (setting is EnumSetting enumSetting)
            {
                if (enumSetting.Options.Any(o => o.Value.Equals(envValue, StringComparison.OrdinalIgnoreCase)))
                    return envValue;
                return null;
            }
            if (setting is SliderSetting || setting is DropdownSliderSetting)
            {
                if (double.TryParse(envValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    return d;
                return null;
            }
            return envValue;
        }

        private static object GetDefaultValue(SettingDefinition setting)
        {
            switch (setting)
            {
                case BoolSetting b: return b.DefaultValue;
                case StringSetting s: return s.DefaultValue;
                case EnumSetting e: return e.DefaultValue;
                case SliderSetting sl: return sl.DefaultValue;
                case DropdownSliderSetting ds: return ds.DefaultValue;
                default: return null;
            }
        }

        private TomlNode FindNode(TomlTable table, SettingDefinition setting)
        {
            // Check if the setting is inside a section (TOML table)
            if (setting.Section != null)
            {
                var sectionKey = SanitizeTomlKey(setting.Section);
                if (table.HasKey(sectionKey) && table[sectionKey].IsTable)
                    return table[sectionKey][setting.Key];
            }

            // Also try flat lookup for backwards compatibility
            if (table.HasKey(setting.Key))
                return table[setting.Key];

            // Search all tables
            foreach (var key in table.Keys)
            {
                var child = table[key];
                if (child.IsTable && child.HasKey(setting.Key))
                    return child[setting.Key];
            }

            return null;
        }

        private static object ParseNode(TomlNode node, SettingDefinition setting)
        {
            try
            {
                switch (setting)
                {
                    case BoolSetting _ when node.IsBoolean:
                        return node.AsBoolean.Value;
                    case StringSetting _ when node.IsString:
                        return node.AsString.Value;
                    case EnumSetting _ when node.IsString:
                        return node.AsString.Value;
                    case SliderSetting _ when node.IsFloat:
                        return node.AsFloat.Value;
                    case SliderSetting _ when node.IsInteger:
                        return (double)node.AsInteger.Value;
                    case DropdownSliderSetting _ when node.IsFloat:
                        return node.AsFloat.Value;
                    case DropdownSliderSetting _ when node.IsInteger:
                        return (double)node.AsInteger.Value;
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private TomlNode CreateNode(SettingDefinition setting)
        {
            var value = _values.ContainsKey(setting.Key) ? _values[setting.Key] : GetDefaultValue(setting);

            switch (setting)
            {
                case BoolSetting _:
                    return new TomlBoolean { Value = Convert.ToBoolean(value) };
                case StringSetting _:
                case EnumSetting _:
                    return new TomlString { Value = Convert.ToString(value, CultureInfo.InvariantCulture) };
                case SliderSetting _:
                case DropdownSliderSetting _:
                    return new TomlFloat { Value = Convert.ToDouble(value, CultureInfo.InvariantCulture) };
                default:
                    return new TomlString { Value = value?.ToString() ?? "" };
            }
        }

        private static string BuildComment(SettingDefinition setting)
        {
            var parts = new List<string>();
            parts.Add(setting.DisplayName);

            if (!string.IsNullOrEmpty(setting.Description))
                parts.Add(setting.Description);

            switch (setting)
            {
                case BoolSetting _:
                    parts.Add("Values: true, false");
                    break;
                case EnumSetting e:
                    var opts = string.Join(", ", e.Options.Select(o => o.Value));
                    parts.Add($"Values: {opts}");
                    break;
                case SliderSetting sl:
                    parts.Add($"Range: {sl.Minimum.ToString(CultureInfo.InvariantCulture)} - {sl.Maximum.ToString(CultureInfo.InvariantCulture)}");
                    break;
                case DropdownSliderSetting ds:
                    parts.Add($"Range: {ds.Minimum.ToString(CultureInfo.InvariantCulture)} - {ds.Maximum.ToString(CultureInfo.InvariantCulture)}");
                    break;
            }

            return string.Join("\n", parts);
        }

        /// <summary>
        /// Track which section each setting belongs to during Save().
        /// Called during definition iteration to assign sections to settings.
        /// </summary>
        internal void AssignSections()
        {
            string currentSection = null;
            foreach (var element in _definition.Elements)
            {
                if (element is SectionHeader section)
                {
                    currentSection = section.Title;
                    continue;
                }
                if (element is SettingDefinition setting)
                {
                    setting.Section = currentSection;
                }
            }
        }

        private static string SanitizeTomlKey(string name)
        {
            // Convert to lowercase, replace spaces with hyphens
            return name.ToLowerInvariant().Replace(' ', '-');
        }
    }
}
