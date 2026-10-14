using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace WineFix.Patches
{
    /// <summary>
    /// Fixes the empty Leading dropdown in the text context toolbar (Affinity 3.3).
    ///
    /// LeadingComboItemStyle and CheckedComboBoxItemStyle in ContextBarItemTemplates.xaml set
    /// no template of their own; they are BasedOn the implicit CheckedComboBoxItem style and
    /// expect Core.xaml's. Affinity 3.3 merged ReflectedLayoutItemTemplates.xaml into the same
    /// dictionary, and its implicit CheckedComboBoxItem style has an empty ControlTemplate (a
    /// base its own derived styles replace). The merged dictionary is searched first, so the
    /// dropdown items get no visual tree and the list opens with zero height.
    ///
    /// Replacing the dictionary entries does not help: the context bar's DataTemplates keep the
    /// style objects their StaticResource references were bound to. Instead, when a
    /// CheckedComboBox applies its template, an ItemContainerStyle whose inherited template is
    /// empty is swapped for a copy BasedOn Core.xaml's style. Styles with a real template,
    /// including Reflected's own, are left alone, as is everything once Serif fixes the XAML.
    /// </summary>
    public static class ContextBarItemStylePatch
    {
        private static readonly Dictionary<Style, Style> Rebased = new Dictionary<Style, Style>();

        private static Type _itemType;
        private static Style _coreStyle;
        private static bool _coreLookedUp;

        public static void ApplyPatches(Harmony harmony)
        {
            Logger.Info("Applying ContextBarItemStyle patch...");

            var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

            if (serifAssembly == null)
            {
                Logger.Error("Serif.Affinity assembly not found");
                return;
            }

            _itemType = serifAssembly.GetType("Serif.Affinity.UI.Controls.CheckedComboBoxItem");
            var comboType = serifAssembly.GetType("Serif.Affinity.UI.Controls.CheckedComboBox");
            var onApplyTemplate = comboType == null ? null : AccessTools.Method(comboType, "OnApplyTemplate");

            if (_itemType == null || onApplyTemplate == null)
            {
                Logger.Error("CheckedComboBoxItem or CheckedComboBox.OnApplyTemplate not found");
                return;
            }

            harmony.Patch(onApplyTemplate, postfix: new HarmonyMethod(typeof(ContextBarItemStylePatch), nameof(OnApplyTemplate_Postfix)));
            Logger.Info("Patched CheckedComboBox.OnApplyTemplate");
        }

        public static void OnApplyTemplate_Postfix(ItemsControl __instance)
        {
            try
            {
                var style = __instance.ItemContainerStyle;
                if (style == null || style.TargetType != _itemType)
                    return;

                if (!Rebased.TryGetValue(style, out var replacement))
                {
                    var template = FindTemplate(style);
                    var core = template == null || HasContent(template) ? null : CoreStyle();
                    replacement = core == null ? null : Rebase(style, core);
                    Rebased[style] = replacement;
                    if (replacement != null)
                        Logger.Info("ContextBarItemStyle: dropdown item style with an empty template now based on Core.xaml's");
                }

                if (replacement != null)
                    __instance.ItemContainerStyle = replacement;
            }
            catch (Exception ex)
            {
                Logger.Error("ContextBarItemStyle: " + ex);
            }
        }

        /// <summary>Core.xaml's implicit CheckedComboBoxItem style from the application resources, if it has a template.</summary>
        private static Style CoreStyle()
        {
            if (_coreLookedUp)
                return _coreStyle;
            _coreLookedUp = true;

            var core = System.Windows.Application.Current?.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null &&
                d.Source.OriginalString.EndsWith("/Core.xaml", StringComparison.OrdinalIgnoreCase));
            if (core != null && core.Contains(_itemType) && core[_itemType] is Style style && HasContent(FindTemplate(style)))
                _coreStyle = style;
            else
                Logger.Info("ContextBarItemStyle: Core.xaml's CheckedComboBoxItem style not found, nothing changed");
            return _coreStyle;
        }

        private static Style Rebase(Style style, Style basedOn)
        {
            if (style.Triggers.Count != 0 || style.Resources.Count != 0)
                return null;

            var rebased = new Style(style.TargetType, basedOn);
            foreach (var setterBase in style.Setters)
            {
                if (setterBase is Setter setter)
                    rebased.Setters.Add(new Setter(setter.Property, setter.Value, setter.TargetName));
                else if (setterBase is EventSetter eventSetter)
                    rebased.Setters.Add(new EventSetter(eventSetter.Event, eventSetter.Handler) { HandledEventsToo = eventSetter.HandledEventsToo });
                else
                    return null;
            }
            return rebased;
        }

        /// <summary>The template the style applies: its own Template setter, else the nearest base's.</summary>
        private static ControlTemplate FindTemplate(Style style)
        {
            for (var s = style; s != null; s = s.BasedOn)
            {
                foreach (var setterBase in s.Setters)
                {
                    if (setterBase is Setter setter && setter.Property == Control.TemplateProperty)
                        return setter.Value as ControlTemplate;
                }
            }
            return null;
        }

        private static bool HasContent(FrameworkTemplate template)
        {
            if (template == null)
                return false;
            if (template.VisualTree != null)
                return true;
            var hasContent = typeof(FrameworkTemplate).GetProperty("HasContent", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return hasContent != null && (bool)hasContent.GetValue(template);
        }
    }
}
