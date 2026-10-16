using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Windows;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace WineFix.Patches
{
    /// <summary>
    /// Keeps the sub-tool panel's column count across a studio switch.
    ///
    /// The sub-tool panel is a ToolTray like the main tool pane. SubToolPanel.ToggleVisibility
    /// gives it two columns when the current tool has more than ten sub-tools (the shape tools)
    /// and one otherwise, and runs whenever the tool changes. A studio switch also raises
    /// CurrentWorkspaceChanged, and every ToolTray handles that by setting Columns to
    /// ToolSettings.ToolPaneColumns -- the main tool pane's setting. With a one-column tool pane
    /// the shape tools came back as a single column running the height of the window, until
    /// the next tool change put two columns back.
    ///
    /// After that handler, the sub-tool panel's tray gets its own rule back. The main tool pane
    /// is left alone.
    /// </summary>
    public static class SubToolColumnsPatch
    {
        private static PropertyInfo _toolhostProperty;
        private static PropertyInfo _subToolRootProperty;
        private static PropertyInfo _columnsProperty;
        private static PropertyInfo _currentToolProperty;

        public static void ApplyPatches(Harmony harmony)
        {
            Logger.Info("Applying SubToolColumns patch...");

            var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");
            if (serifAssembly == null)
            {
                Logger.Error("Serif.Affinity assembly not found");
                return;
            }

            var trayType = serifAssembly.GetType("Serif.Affinity.UI.Controls.Tools.ToolTray");
            var hostType = serifAssembly.GetType("Serif.Affinity.UI.Controls.Tools.ToolHost");
            var windowType = serifAssembly.GetType("Serif.Affinity.UI.AffinityWindow");
            var handler = trayType == null ? null
                : AccessTools.Method(trayType, "WorkspaceService_CurrentWorkspaceChanged");

            _toolhostProperty = windowType == null ? null : AccessTools.Property(windowType, "Toolhost");
            _subToolRootProperty = hostType == null ? null : AccessTools.Property(hostType, "SubToolRoot");
            _columnsProperty = trayType == null ? null : AccessTools.Property(trayType, "Columns");
            _currentToolProperty = trayType == null ? null : AccessTools.Property(trayType, "CurrentTool");

            if (handler == null || _toolhostProperty == null || _subToolRootProperty == null
                || _columnsProperty == null || _currentToolProperty == null)
            {
                Logger.Error("ToolTray/ToolHost members not found; sub-tool columns left as they are");
                return;
            }

            harmony.Patch(handler, postfix: new HarmonyMethod(typeof(SubToolColumnsPatch), nameof(CurrentWorkspaceChanged_Postfix)));
            Logger.Info("Patched ToolTray.WorkspaceService_CurrentWorkspaceChanged");
        }

        public static void CurrentWorkspaceChanged_Postfix(object __instance)
        {
            try
            {
                var window = Application.Current?.MainWindow;
                if (window == null || !_toolhostProperty.DeclaringType.IsInstanceOfType(window))
                    return;
                var toolhost = _toolhostProperty.GetValue(window);
                if (toolhost == null || !ReferenceEquals(_subToolRootProperty.GetValue(toolhost), __instance))
                    return;

                // SubToolPanel.ToggleVisibility's rule.
                var tool = _currentToolProperty.GetValue(__instance);
                if (tool == null)
                    return;
                var children = AccessTools.Property(tool.GetType(), "Children")?.GetValue(tool) as ICollection;
                if (children == null)
                    return;
                object columns = children.Count > 10 ? 2 : 1;
                if (_columnsProperty.PropertyType == typeof(int?))
                    columns = (int?)(int)columns;
                _columnsProperty.SetValue(__instance, columns);
            }
            catch (Exception ex)
            {
                Logger.Error("SubToolColumns: " + ex.Message);
            }
        }
    }
}
