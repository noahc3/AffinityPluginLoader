using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using AffinityPluginLoader.Core;

namespace AffinityPluginLoader.UI
{
    /// <summary>
    /// Creates Serif UI controls via reflection with fallback to plain WPF controls.
    /// </summary>
    internal static class SerifControlHelper
    {
        private static Type _toggleSwitchType;
        private static Type _rangeSliderType;
        private static Type _unitEditorType;
        private static Type _unitValueType;
        private static Type _unitTypeEnum;
        private static bool _resolved;
        private static bool _available;

        public static bool ForceWpfFallback
        {
            get
            {
                var store = PluginManager.GetSettingsStore(AplSettings.PluginId);
                return store?.GetEffectiveValue<bool>(AplSettings.ForceWpfControls) ?? false;
            }
        }

        private static bool ResolveTypes()
        {
            if (_resolved) return _available;
            _resolved = true;

            try
            {
                var serifWindows = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Windows");
                var serifAffinity = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");
                var serifPersona = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Interop.Persona");

                _toggleSwitchType = serifWindows?.GetType("Serif.Windows.UI.Controls.ToggleSwitch");
                _rangeSliderType = serifAffinity?.GetType("Serif.Affinity.UI.Controls.RangeSlider");
                _unitEditorType = serifAffinity?.GetType("Serif.Affinity.UI.Units.UnitEditor");
                _unitValueType = serifPersona?.GetType("Serif.Interop.Persona.Units.UnitValue");
                _unitTypeEnum = serifPersona?.GetType("Serif.Interop.Persona.Units.UnitType");

                _available = _toggleSwitchType != null;
                Logger.Debug($"Serif controls resolved: ToggleSwitch={_toggleSwitchType != null}, RangeSlider={_rangeSliderType != null}, UnitEditor={_unitEditorType != null}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to resolve Serif control types: {ex.Message}");
                _available = false;
            }

            return _available;
        }

        /// <summary>
        /// Creates a toggle switch. Uses Serif ToggleSwitch if available, otherwise CheckBox.
        /// </summary>
        public static ToggleButton CreateToggleSwitch()
        {
            if (!ForceWpfFallback && ResolveTypes() && _toggleSwitchType != null)
            {
                try
                {
                    var toggle = (ToggleButton)Activator.CreateInstance(_toggleSwitchType);
                    // Set CheckedText="" and UncheckedText="" like Affinity does
                    var checkedTextProp = _toggleSwitchType.GetProperty("CheckedText");
                    var uncheckedTextProp = _toggleSwitchType.GetProperty("UncheckedText");
                    checkedTextProp?.SetValue(toggle, "");
                    uncheckedTextProp?.SetValue(toggle, "");
                    return toggle;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to create Serif ToggleSwitch, falling back to CheckBox: {ex.Message}");
                }
            }

            return new CheckBox();
        }

        /// <summary>
        /// Creates an inline slider with numeric readout.
        /// Uses Serif RangeSlider + UnitEditor if available, otherwise WPF Slider + TextBlock.
        /// Returns a panel containing both controls.
        /// </summary>
        public static FrameworkElement CreateSlider(double min, double max, int precision, Binding valueBinding)
        {
            if (!ForceWpfFallback && ResolveTypes() && _rangeSliderType != null && _unitValueType != null && _unitEditorType != null)
            {
                try
                {
                    return CreateSerifSlider(min, max, precision, valueBinding, popupMode: false);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to create Serif slider, falling back to WPF: {ex.Message}");
                }
            }

            return CreateWpfSlider(min, max, precision, valueBinding);
        }

        /// <summary>
        /// Creates a dropdown slider (text field with popup slider).
        /// Uses Serif UnitEditor with EditableSlider popup if available, otherwise WPF Slider + TextBox.
        /// </summary>
        public static FrameworkElement CreateDropdownSlider(double min, double max, int precision, Binding valueBinding)
        {
            if (!ForceWpfFallback && ResolveTypes() && _unitEditorType != null && _unitValueType != null)
            {
                try
                {
                    return CreateSerifSlider(min, max, precision, valueBinding, popupMode: true);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to create Serif dropdown slider, falling back to WPF: {ex.Message}");
                }
            }

            return CreateWpfSlider(min, max, precision, valueBinding);
        }

        private static FrameworkElement CreateSerifSlider(double min, double max, int precision, Binding valueBinding, bool popupMode)
        {
            // Get UnitType.Number enum value
            var unitTypeNumber = Enum.Parse(_unitTypeEnum, "Number");

            if (popupMode)
            {
                // UnitEditor with PopupMode = EditableSlider
                var editor = (Control)Activator.CreateInstance(_unitEditorType);

                // Set properties via reflection
                SetDependencyProperty(_unitEditorType, editor, "Minimum", CreateUnitValue(min, unitTypeNumber));
                SetDependencyProperty(_unitEditorType, editor, "Maximum", CreateUnitValue(max, unitTypeNumber));
                SetDependencyProperty(_unitEditorType, editor, "Precision", (int?)precision);

                // PopupMode enum
                var popupModeType = _unitEditorType.GetProperty("PopupMode")?.PropertyType;
                if (popupModeType != null)
                {
                    var editableSlider = Enum.Parse(popupModeType, "EditableSlider");
                    SetDependencyProperty(_unitEditorType, editor, "PopupMode", editableSlider);
                }

                // OutputUnitType = Number
                var outputUnitTypeProp = _unitEditorType.GetProperty("OutputUnitType");
                if (outputUnitTypeProp != null)
                    outputUnitTypeProp.SetValue(editor, unitTypeNumber);

                editor.Width = 80;

                // Bind UnitValue - we need a converter from double to UnitValue
                BindUnitValue(editor, _unitEditorType, valueBinding, unitTypeNumber);

                return editor;
            }
            else
            {
                // RangeSlider + UnitEditor pair
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 8, 2, 0) };

                var slider = (Slider)Activator.CreateInstance(_rangeSliderType);
                slider.Minimum = min;
                slider.Maximum = max;
                slider.Width = 250;

                // Set tick properties
                slider.TickPlacement = TickPlacement.BottomRight;
                slider.IsSnapToTickEnabled = precision == 0;

                // Bind Value (plain double) directly
                slider.SetBinding(RangeBase.ValueProperty, valueBinding);

                var editor = (Control)Activator.CreateInstance(_unitEditorType);
                SetDependencyProperty(_unitEditorType, editor, "Precision", (int?)precision);

                var outputUnitTypeProp = _unitEditorType.GetProperty("OutputUnitType");
                if (outputUnitTypeProp != null)
                    outputUnitTypeProp.SetValue(editor, unitTypeNumber);

                editor.Width = 65;
                editor.Margin = new Thickness(2, 0, 0, 0);

                BindUnitValue(editor, _unitEditorType, valueBinding, unitTypeNumber);

                panel.Children.Add(slider);
                panel.Children.Add(editor);
                return panel;
            }
        }

        private static object CreateUnitValue(double value, object unitType)
        {
            // new UnitValue(double, UnitType)
            var ctor = _unitValueType.GetConstructor(new[] { typeof(double), _unitTypeEnum });
            return ctor?.Invoke(new[] { value, unitType });
        }

        private static void SetDependencyProperty(Type controlType, DependencyObject obj, string propName, object value)
        {
            var dpField = controlType.GetField(propName + "Property", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (dpField != null && value != null)
            {
                var dp = (DependencyProperty)dpField.GetValue(null);
                obj.SetValue(dp, value);
            }
        }

        private static void BindUnitValue(Control control, Type controlType, Binding sourceBinding, object unitType)
        {
            // For Serif controls, UnitValue is the binding property but our store uses double.
            // We bind the plain Slider.Value (double) and let the control sync internally.
            // For UnitEditor standalone (dropdown), we bind via the underlying slider's Value.
            // Simplest approach: bind the UnitValue DP with a converter.

            var uvDpField = controlType.GetField("UnitValueProperty", BindingFlags.Public | BindingFlags.Static);
            if (uvDpField == null) return;

            var dp = (DependencyProperty)uvDpField.GetValue(null);

            // Create a binding that uses our DoubleToUnitValueConverter
            var binding = new Binding(sourceBinding.Path.Path)
            {
                Source = sourceBinding.Source,
                Mode = BindingMode.TwoWay,
                Converter = new DoubleToUnitValueConverter(_unitValueType, _unitTypeEnum, unitType),
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            BindingOperations.SetBinding(control, dp, binding);
        }

        private static FrameworkElement CreateWpfSlider(double min, double max, int precision, Binding valueBinding)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 8, 2, 0) };

            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Width = 250,
                TickPlacement = TickPlacement.BottomRight,
                IsSnapToTickEnabled = precision == 0,
                TickFrequency = precision == 0 ? 1 : (max - min) / 20
            };
            slider.SetBinding(RangeBase.ValueProperty, valueBinding);

            var textBlock = new TextBlock
            {
                Width = 55,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var textBinding = new Binding(valueBinding.Path.Path)
            {
                Source = valueBinding.Source,
                StringFormat = precision > 0 ? $"F{precision}" : "F0"
            };
            textBlock.SetBinding(TextBlock.TextProperty, textBinding);

            panel.Children.Add(slider);
            panel.Children.Add(textBlock);
            return panel;
        }
    }

    /// <summary>
    /// Converts between double and Serif UnitValue via reflection.
    /// </summary>
    internal class DoubleToUnitValueConverter : IValueConverter
    {
        private readonly Type _unitValueType;
        private readonly Type _unitTypeEnum;
        private readonly object _unitType;
        private readonly ConstructorInfo _ctor;
        private readonly PropertyInfo _valueProp;

        public DoubleToUnitValueConverter(Type unitValueType, Type unitTypeEnum, object unitType)
        {
            _unitValueType = unitValueType;
            _unitTypeEnum = unitTypeEnum;
            _unitType = unitType;
            _ctor = unitValueType.GetConstructor(new[] { typeof(double), unitTypeEnum });
            _valueProp = unitValueType.GetProperty("Value");
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double d && _ctor != null)
                return _ctor.Invoke(new object[] { d, _unitType });
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value != null && _valueProp != null && _unitValueType.IsInstanceOfType(value))
                return (double)_valueProp.GetValue(value);
            return 0.0;
        }
    }
}
