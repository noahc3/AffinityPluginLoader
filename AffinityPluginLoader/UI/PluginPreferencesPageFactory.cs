using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using AffinityPluginLoader.Core;
using AffinityPluginLoader.Settings;

namespace AffinityPluginLoader.UI
{
    /// <summary>
    /// Generates a PreferencesPage from a PluginSettingsDefinition + SettingsStore.
    /// </summary>
    internal static class PluginPreferencesPageFactory
    {
        public static object CreatePage(string pageName, PluginSettingsDefinition definition, SettingsStore store)
        {
            try
            {
                var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");
                if (serifAssembly == null) return null;

                var preferencesPageType = serifAssembly.GetType("Serif.Affinity.UI.Dialogs.Preferences.PreferencesPage");
                if (preferencesPageType == null) return null;

                // Create PreferencesPage instance (it extends Grid)
                var page = (Grid)Activator.CreateInstance(preferencesPageType);

                // Set PageName
                preferencesPageType.GetProperty("PageName")?.SetValue(page, pageName);

                // Build content
                var outerGrid = new Grid { Width = 600 };
                var stackPanel = new StackPanel { Margin = new Thickness(0, 0, 4, 15) };

                // Create a binding source that wraps the SettingsStore
                var bindingSource = new SettingsBindingSource(store);

                foreach (var element in definition.Elements)
                {
                    var uiElement = CreateElement(element, bindingSource, serifAssembly);
                    if (uiElement != null)
                        stackPanel.Children.Add(uiElement);
                }

                outerGrid.Children.Add(stackPanel);
                page.Children.Add(outerGrid);

                Logger.Debug($"Created preferences page: {pageName}");
                return page;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating preferences page '{pageName}'", ex);
                return null;
            }
        }

        private static UIElement CreateElement(ISettingsElement element, SettingsBindingSource bindingSource, System.Reflection.Assembly serifAssembly)
        {
            switch (element)
            {
                case SectionHeader section:
                    return CreateSectionHeader(section, serifAssembly);

                case InlineText text:
                    return CreateInlineText(text);

                case InlineMutedText mutedText:
                    return CreateInlineMutedText(mutedText, serifAssembly);

                case InlineXaml xaml:
                    return CreateInlineXaml(xaml);

                case BoolSetting boolSetting:
                    return CreateBoolRow(boolSetting, bindingSource, serifAssembly);

                case StringSetting stringSetting:
                    return CreateStringRow(stringSetting, bindingSource, serifAssembly);

                case EnumSetting enumSetting:
                    return CreateEnumRow(enumSetting, bindingSource, serifAssembly);

                case SliderSetting sliderSetting:
                    return CreateSliderRow(sliderSetting, bindingSource, serifAssembly);

                case DropdownSliderSetting dropdownSetting:
                    return CreateDropdownSliderRow(dropdownSetting, bindingSource, serifAssembly);

                default:
                    return null;
            }
        }

        // ── Organizational elements ──

        private static UIElement CreateSectionHeader(SectionHeader section, System.Reflection.Assembly serifAssembly)
        {
            var label = new Label { Content = section.Title };
            ApplyStyleFromAssembly(label, serifAssembly, "SectionHeading");
            return label;
        }

        private static UIElement CreateInlineText(InlineText text)
        {
            return new TextBlock
            {
                Text = text.Text,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 5, 10, 5)
            };
        }

        private static UIElement CreateInlineMutedText(InlineMutedText text, System.Reflection.Assembly serifAssembly)
        {
            var label = new Label
            {
                Content = text.Text,
                Margin = new Thickness(10, 2, 10, 2)
            };
            ApplyMutedForeground(label, serifAssembly);
            return label;
        }

        private static UIElement CreateInlineXaml(InlineXaml xaml)
        {
            try
            {
                var parsed = XamlReader.Parse(xaml.Xaml) as UIElement;
                if (parsed is FrameworkElement fe && xaml.DataContext != null)
                    fe.DataContext = xaml.DataContext;
                return parsed;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse inline XAML", ex);
                return new TextBlock { Text = "[XAML parse error]", Foreground = System.Windows.Media.Brushes.Red };
            }
        }

        // ── Setting rows ──

        private static UIElement CreateBoolRow(BoolSetting setting, SettingsBindingSource source, System.Reflection.Assembly serifAssembly)
        {
            var toggle = SerifControlHelper.CreateToggleSwitch();
            var binding = source.CreateBinding(setting.Key);
            toggle.SetBinding(ToggleButton.IsCheckedProperty, binding);
            return WrapInRow(setting, toggle, serifAssembly, source);
        }

        private static UIElement CreateStringRow(StringSetting setting, SettingsBindingSource source, System.Reflection.Assembly serifAssembly)
        {
            TextBox textBox;
            try
            {
                // Create via XAML so DynamicResource bindings to Serif scheme brushes work
                var xaml = @"<TextBox xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                                     xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                                     xmlns:schemes=""clr-namespace:Serif.Affinity.Resources.Schemes;assembly=Serif.Affinity""
                                     Width=""210"" Margin=""2"" BorderThickness=""1""
                                     Foreground=""{DynamicResource {x:Static schemes:SchemeManager.Brush_BaseForeground}}""
                                     Background=""{DynamicResource {x:Static schemes:SchemeManager.Brush_ComboBoxEditableBackground}}""
                                     BorderBrush=""{DynamicResource {x:Static schemes:SchemeManager.Brush_ComboBoxBorder}}"" />";
                textBox = (TextBox)XamlReader.Parse(xaml);
            }
            catch
            {
                textBox = new TextBox { Width = 210, Margin = new Thickness(2) };
            }
            textBox.SetBinding(TextBox.TextProperty, source.CreateBinding(setting.Key));
            return WrapInRow(setting, textBox, serifAssembly, source);
        }

        private static UIElement CreateEnumRow(EnumSetting setting, SettingsBindingSource source, System.Reflection.Assembly serifAssembly)
        {
            var comboBox = new ComboBox { Margin = new Thickness(2) };
            foreach (var option in setting.Options)
                comboBox.Items.Add(new ComboBoxItem { Content = option.DisplayName, Tag = option.Value });

            // Bind SelectedValue to the setting
            comboBox.SelectedValuePath = "Tag";
            comboBox.SetBinding(Selector.SelectedValueProperty, source.CreateBinding(setting.Key));
            return WrapInRow(setting, comboBox, serifAssembly, source);
        }

        private static UIElement CreateSliderRow(SliderSetting setting, SettingsBindingSource source, System.Reflection.Assembly serifAssembly)
        {
            var control = SerifControlHelper.CreateSlider(
                setting.Minimum, setting.Maximum, setting.Precision,
                source.CreateBinding(setting.Key));
            return WrapInRow(setting, control, serifAssembly, source);
        }

        private static UIElement CreateDropdownSliderRow(DropdownSliderSetting setting, SettingsBindingSource source, System.Reflection.Assembly serifAssembly)
        {
            var control = SerifControlHelper.CreateDropdownSlider(
                setting.Minimum, setting.Maximum, setting.Precision,
                source.CreateBinding(setting.Key));
            return WrapInRow(setting, control, serifAssembly, source);
        }

        // ── Row layout helpers ──

        /// <summary>
        /// Wraps a label + control in the standard Affinity preferences row layout:
        /// Border(PanelStyle) > Grid > [Label column | Control column]
        /// </summary>
        private static UIElement WrapInRow(SettingDefinition setting, UIElement control, System.Reflection.Assembly serifAssembly, SettingsBindingSource source)
        {
            var border = new Border();
            ApplyStyleFromAssembly(border, serifAssembly, "PanelStyle");

            var grid = new Grid { Margin = new Thickness(10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Label column - may include description and restart indicator
            var labelPanel = CreateLabelPanel(setting, serifAssembly, source);
            Grid.SetColumn(labelPanel, 0);
            grid.Children.Add(labelPanel);

            // Control column
            var controlElement = control as FrameworkElement;
            if (controlElement != null)
                controlElement.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);

            border.Child = grid;
            return border;
        }

        private static UIElement CreateLabelPanel(SettingDefinition setting, System.Reflection.Assembly serifAssembly, SettingsBindingSource source)
        {
            var hasDescription = !string.IsNullOrEmpty(setting.Description);
            var hasRestart = setting.RestartRequired;
            var hasEnvOverride = source.Store.IsOverriddenByEnv(setting.Key);
            var hasInfo = !string.IsNullOrEmpty(setting.InfoMessage);

            // Simple case: just the label (possibly with info icon)
            if (!hasDescription && !hasRestart && !hasEnvOverride && !hasInfo)
            {
                var textBlock = new TextBlock { Text = setting.DisplayName, VerticalAlignment = VerticalAlignment.Center };
                ApplyTextLabelStyle(textBlock, serifAssembly);
                return textBlock;
            }

            // Complex case: stack with label + optional icons/description/restart
            var panel = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };

            // Main label row (may include env warning icon and/or info icon)
            var needsLabelRow = hasEnvOverride || hasInfo;
            if (needsLabelRow)
            {
                var labelRow = new StackPanel { Orientation = Orientation.Horizontal };
                var nameBlock = new TextBlock { Text = setting.DisplayName };
                ApplyTextLabelStyle(nameBlock, serifAssembly);
                labelRow.Children.Add(nameBlock);

                if (hasInfo)
                {
                    var infoIcon = CreateInfoIcon(setting.InfoMessage, serifAssembly);
                    if (infoIcon != null)
                        labelRow.Children.Add(infoIcon);
                }

                if (hasEnvOverride)
                {
                    var envVarName = source.Store.GetEnvVarName(setting.Key);
                    var envValue = source.Store.GetEnvOverrideRaw(setting.Key);
                    var warningIcon = CreateWarningIcon(envVarName, envValue);
                    if (warningIcon != null)
                        labelRow.Children.Add(warningIcon);
                }

                panel.Children.Add(labelRow);
            }
            else
            {
                var nameBlock = new TextBlock { Text = setting.DisplayName };
                ApplyTextLabelStyle(nameBlock, serifAssembly);
                panel.Children.Add(nameBlock);
            }

            if (hasDescription)
            {
                var descBlock = new TextBlock
                {
                    Text = setting.Description,
                    FontSize = 9,
                    MaxWidth = 470,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                ApplyMutedForeground(descBlock, serifAssembly);
                panel.Children.Add(descBlock);
            }

            if (hasRestart)
            {
                var restartBlock = new TextBlock
                {
                    Text = "*Restart required",
                    FontSize = 9,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                ApplyMutedForeground(restartBlock, serifAssembly);
                panel.Children.Add(restartBlock);
            }

            return panel;
        }

        // ── Icon helpers ──

        private static UIElement CreateInfoIcon(string message, System.Reflection.Assembly serifAssembly)
        {
            try
            {
                var xaml = @"<Grid xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    VerticalAlignment=""Center"" Margin=""5,0,0,0"" Width=""12"" Height=""12""
                    ToolTip=""" + System.Security.SecurityElement.Escape(message) + @""">
                    <Ellipse Width=""12"" Height=""12"" Fill=""#FF4A90D9"" />
                    <TextBlock Text=""i"" FontSize=""8"" FontWeight=""Bold""
                               HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                               Foreground=""#FFFFFFFF"" />
                </Grid>";
                return (UIElement)XamlReader.Parse(xaml);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to create info icon: {ex.Message}");
                return null;
            }
        }

        private static UIElement CreateWarningIcon(string envVarName, string envValue)
        {
            try
            {
                var xaml = @"<Grid xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    VerticalAlignment=""Center"" Margin=""5,0,0,0""
                    ToolTip=""Value overridden by environment variable:&#x0a;" + envVarName + "=" + System.Security.SecurityElement.Escape(envValue) + @"&#x0a;&#x0a;The value shown is the saved value on disk.&#x0a;The env variable value is used until Affinity is relaunched without it."">
                    <Path HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Data=""M8,0.5 L15.5,15.5 L0.5,15.5"" StrokeThickness=""1"" StrokeLineJoin=""Round"" Stroke=""#FF000000"" Fill=""#FFFFE200"" />
                    <Path HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Data=""M0.5,4.5 A0.5,0.5 45 1 1 2.25,4.5L1.75,8.5 L1,8.5 M1.5,11.5 A0.5,0.5 0 1 0 1.5,12.5M1.5,11.5 A0.5,0.5 0 1 1 1.5,12.5"" StrokeThickness=""1"" Stroke=""#FF000000"" Fill=""#FF000000"" />
                </Grid>";
                return (UIElement)XamlReader.Parse(xaml);
            }
            catch
            {
                return null;
            }
        }

        // ── Style helpers ──

        private static void ApplyStyleFromAssembly(FrameworkElement element, System.Reflection.Assembly serifAssembly, string styleKey)
        {
            try
            {
                // Try to load PreferencesStyles.xaml resource dictionary
                var rd = LoadPreferencesStyles(serifAssembly);
                if (rd != null && rd.Contains(styleKey))
                {
                    element.Style = (Style)rd[styleKey];
                    return;
                }
            }
            catch { }

            // Fallback: apply basic styling manually
            if (styleKey == "SectionHeading" && element is Label label)
            {
                label.FontWeight = FontWeights.Bold;
                label.Margin = new Thickness(10, 10, 10, 5);
                label.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else if (styleKey == "PanelStyle" && element is Border border)
            {
                border.Margin = new Thickness(10, 1, 10, 1);
                border.CornerRadius = new CornerRadius(3);
            }
        }

        private static void ApplyTextLabelStyle(TextBlock textBlock, System.Reflection.Assembly serifAssembly)
        {
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.HorizontalAlignment = HorizontalAlignment.Left;
            textBlock.VerticalAlignment = VerticalAlignment.Center;

            try
            {
                var rd = LoadPreferencesStyles(serifAssembly);
                if (rd != null && rd.Contains("TextLabelStyle"))
                    textBlock.Style = (Style)rd["TextLabelStyle"];
            }
            catch { }
        }

        private static void ApplyMutedForeground(FrameworkElement element, System.Reflection.Assembly serifAssembly)
        {
            try
            {
                var schemeType = serifAssembly?.GetType("Serif.Affinity.Resources.Schemes.SchemeManager");
                var brushField = schemeType?.GetProperty("Brush_LabelForeground", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var resourceKey = brushField?.GetValue(null);
                if (resourceKey != null)
                {
                    if (element is TextBlock tb)
                        tb.SetResourceReference(TextBlock.ForegroundProperty, resourceKey);
                    else if (element is Control ctrl)
                        ctrl.SetResourceReference(Control.ForegroundProperty, resourceKey);
                    return;
                }
            }
            catch { }

            // Fallback
            if (element is TextBlock tb2)
                tb2.Foreground = System.Windows.Media.Brushes.Gray;
            else if (element is Control ctrl2)
                ctrl2.Foreground = System.Windows.Media.Brushes.Gray;
        }

        private static ResourceDictionary _cachedStyles;
        private static bool _stylesCached;

        private static ResourceDictionary LoadPreferencesStyles(System.Reflection.Assembly serifAssembly)
        {
            if (_stylesCached) return _cachedStyles;
            _stylesCached = true;

            try
            {
                // Load from Serif.Affinity's embedded resources
                var uri = new Uri("pack://application:,,,/Serif.Affinity;component/ui/dialogs/preferences/styles/preferencesstyles.xaml", UriKind.Absolute);
                _cachedStyles = new ResourceDictionary { Source = uri };
            }
            catch
            {
                _cachedStyles = null;
            }

            return _cachedStyles;
        }
    }

    /// <summary>
    /// Wraps a SettingsStore to provide WPF data binding via INotifyPropertyChanged.
    /// Properties are accessed by setting key via an indexer.
    /// </summary>
    internal class SettingsBindingSource : INotifyPropertyChanged
    {
        public SettingsStore Store { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public SettingsBindingSource(SettingsStore store)
        {
            Store = store;
        }

        public object this[string key]
        {
            get => Store.GetValue<object>(key);
            set
            {
                Store.SetValue(key, value);
                // Fire Item[] so all indexer bindings re-evaluate
                OnPropertyChanged("Item[]");
            }
        }

        public Binding CreateBinding(string key)
        {
            return new Binding($"[{key}]")
            {
                Source = this,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
