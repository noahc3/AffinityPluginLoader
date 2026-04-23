using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace WineFix.Patches
{
    /// <summary>
    /// Fixes command-line file opening on Wine.
    /// 
    /// Affinity's ProcessCommandLineArguments() references the WinRT type
    /// SharedStorageAccessManager, which doesn't exist in Wine. The JIT throws a
    /// TypeLoadException when compiling the method — even for code paths that don't
    /// use it.
    ///
    /// Two code paths are affected:
    /// 1. Fresh launch: ProcessArguments() catches the exception silently, so CLI
    ///    file paths are never queued. Fixed by hooking OnMainWindowLoaded.
    /// 2. Single-instance IPC: When a second instance sends file args via named pipe,
    ///    the first instance dispatches ProcessCommandLineArguments on the UI thread,
    ///    crashing the app. Fixed by replacing SingleInstanceThread.
    /// </summary>
    public static class CommandLineFileOpenPatch
    {
        private static MethodInfo _getService;
        private static Type _iDocViewType;

        public static void ApplyPatches(Harmony harmony)
        {
            Assembly serifAssembly = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name == "Serif.Affinity") { serifAssembly = a; break; }
            if (serifAssembly == null) return;

            var appType = serifAssembly.GetType("Serif.Affinity.Application");
            if (appType == null) return;

            // Cache reflection lookups
            Assembly serifInterop = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name == "Serif.Interop.Persona") { serifInterop = a; break; }
            _iDocViewType = serifInterop?.GetType("Serif.Interop.Persona.Services.IDocumentViewService");

            foreach (var m in appType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                if (m.Name == "GetService" && m.IsGenericMethod && m.GetParameters().Length == 0)
                { _getService = m; break; }

            // Patch 1: Fresh launch — open files after main window loads
            if (Environment.GetCommandLineArgs().Length >= 2)
            {
                var onLoaded = appType.GetMethod("OnMainWindowLoaded",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (onLoaded != null)
                {
                    harmony.Patch(onLoaded,
                        postfix: new HarmonyMethod(typeof(CommandLineFileOpenPatch), nameof(OnMainWindowLoaded_Postfix)));
                    Logger.Info("Patched OnMainWindowLoaded for CLI file opening");
                }
            }

            // Patch 2: Single-instance IPC — replace thread to avoid ProcessCommandLineArguments crash
            var singleInstanceThread = appType.GetMethod("SingleInstanceThread",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (singleInstanceThread != null)
            {
                harmony.Patch(singleInstanceThread,
                    prefix: new HarmonyMethod(typeof(CommandLineFileOpenPatch), nameof(SingleInstanceThread_Prefix)));
                Logger.Info("Patched SingleInstanceThread for IPC file opening");
            }
        }

        public static void OnMainWindowLoaded_Postfix(object __instance)
        {
            try
            {
                var filePaths = Environment.GetCommandLineArgs().Skip(1)
                    .Where(a => !a.StartsWith("--") && !a.StartsWith("affinity-open-file:"))
                    .ToList();

                if (filePaths.Count == 0) return;

                Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() => OpenFiles(__instance, filePaths.ToArray())));
            }
            catch (Exception ex)
            {
                Logger.Error("CommandLineFileOpenPatch OnMainWindowLoaded error", ex);
            }
        }

        /// <summary>
        /// Replaces SingleInstanceThread to avoid calling ProcessCommandLineArguments.
        /// Reimplements the named pipe listener, parsing file args ourselves.
        /// </summary>
        public static bool SingleInstanceThread_Prefix()
        {
            // Get the Application instance and required fields
            var appType = AppDomain.CurrentDomain.GetAssemblies()
                .First(a => a.GetName().Name == "Serif.Affinity")
                .GetType("Serif.Affinity.Application");

            var currentProp = appType.GetProperty("Current",
                BindingFlags.Public | BindingFlags.Static);
            var closingField = appType.GetField("m_closing",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var delayField = appType.GetField("m_delayDocumentOpen",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var singleIdProp = appType.GetProperty("SingleInstanceId",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var app = currentProp.GetValue(null);
            var singleInstanceId = (string)singleIdProp.GetValue(app);

            var thread = new Thread(() =>
            {
                while (!(bool)closingField.GetValue(app))
                {
                    try
                    {
                        using (var pipe = new NamedPipeServerStream(singleInstanceId))
                        {
                            pipe.WaitForConnection();

                            while ((bool)delayField.GetValue(app))
                                Thread.Sleep(500);

                            if ((bool)closingField.GetValue(app))
                                continue;

                            try
                            {
                                using (var reader = new BinaryReader(pipe))
                                {
                                    var text = reader.ReadString();
                                    var arguments = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                                    // Skip first arg (exe path), filter flags
                                    var filePaths = arguments.Skip(1)
                                        .Where(a => !a.StartsWith("--") && !a.StartsWith("affinity-open-file:"))
                                        .ToArray();

                                    if (filePaths.Length > 0)
                                    {
                                        ((DispatcherObject)app).Dispatcher.BeginInvoke(
                                            new Action(() => OpenFiles(app, filePaths)));
                                    }
                                }
                            }
                            catch (Exception) { }
                        }
                    }
                    catch (Exception) { }
                }
            });
            thread.IsBackground = true;
            thread.Start();

            return false; // skip original
        }

        private static void OpenFiles(object appInstance, string[] paths)
        {
            try
            {
                if (_getService == null || _iDocViewType == null) return;

                var svc = _getService.MakeGenericMethod(_iDocViewType).Invoke(appInstance, null);
                var loadDoc = svc.GetType().GetMethod("LoadDocument",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool) }, null);
                if (loadDoc == null) return;

                // Activate main window
                var activateMethod = appInstance.GetType().GetMethod("ActivateMainWindow",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                activateMethod?.Invoke(appInstance, null);

                foreach (var path in paths)
                {
                    loadDoc.Invoke(svc, new object[] { path, true, false, false });
                    Logger.Info($"Opened file: {path}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open file", ex);
            }
        }
    }
}
