using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace LoginFix.Patches
{
    /// <summary>
    /// Serif.Affinity.Application.ProcessCommandLineArguments references
    /// Windows.ApplicationModel.DataTransfer.SharedStorageAccessManager (only reached for
    /// "affinity-open-file:" arguments). Wine has no WinRT implementation for that type, and the
    /// CLR resolves every type referenced in a method body when it JITs the method - so any call
    /// into ProcessCommandLineArguments throws System.TypeLoadException, including the unrelated
    /// "affinity://" OAuth callback path the sign-in flow depends on.
    ///
    /// Harmony itself can't patch ProcessCommandLineArguments directly either: patching requires
    /// decompiling the target method's IL (even for a plain prefix, to build the merged
    /// dispatcher), which means resolving every operand in its body - including the poisoned
    /// SharedStorageAccessManager call - and that resolution throws the same way the JIT does.
    ///
    /// Both of ProcessCommandLineArguments' callers only reference it by signature (safe to
    /// resolve), not by body, so patching *them* instead works: ProcessArguments() handles the
    /// app's own startup command line, and SingleInstanceThread() receives arguments forwarded
    /// over a named pipe by a second launched instance (this is the path the Canva sign-in
    /// callback actually takes). Both patches fully replace their target with a safe
    /// reimplementation that never touches the real ProcessCommandLineArguments.
    /// </summary>
    public static class ProcessCommandLineArgumentsPatch
    {
        static readonly HashSet<string> KnownFlags = new HashSet<string>
        {
            "--gpu-telemetry", "--no-dwm-warning", "--hw-ui", "--no-hw-ui", "--no-ocl",
            "--click-through", "--no-click-through", "--input-logging", "--printmode1",
            "--disable-cltest", "--disable-font-preview-cache",
            "--disable-parallel-font-enumeration", "--font-cache-logging",
            "--full-crash-dumps", "--disable-wintab", "--legacy-wintab",
        };

        static Type _applicationType;

        public static void ApplyPatches(Harmony harmony)
        {
            Logger.Info("Applying command-line argument patches (Wine SharedStorageAccessManager fix)...");

            var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");
            if (serifAssembly == null)
            {
                Logger.Error("Serif.Affinity assembly not found");
                return;
            }

            _applicationType = serifAssembly.GetType("Serif.Affinity.Application");
            if (_applicationType == null)
            {
                Logger.Error("Serif.Affinity.Application type not found");
                return;
            }

            var processArguments = AccessTools.Method(_applicationType, "ProcessArguments", Type.EmptyTypes);
            if (processArguments == null)
            {
                Logger.Error("ProcessArguments() not found");
            }
            else
            {
                harmony.Patch(processArguments,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ProcessCommandLineArgumentsPatch), nameof(ProcessArgumentsPrefix))));
                Logger.Info("Patched ProcessArguments (startup command line)");
            }

            var singleInstanceThread = AccessTools.Method(_applicationType, "SingleInstanceThread", Type.EmptyTypes);
            if (singleInstanceThread == null)
            {
                Logger.Error("SingleInstanceThread() not found");
            }
            else
            {
                harmony.Patch(singleInstanceThread,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ProcessCommandLineArgumentsPatch), nameof(SingleInstanceThreadPrefix))));
                Logger.Info("Patched SingleInstanceThread (activation via named pipe - the sign-in callback path)");
            }
        }

        // Replaces Application.ProcessArguments(). Mirrors the original's fallback chain
        // (live command line, then per-user arguments.cfg, then per-machine arguments.cfg)
        // but routes everything through SafeProcessCommandLineArguments instead of the
        // real (poisoned) ProcessCommandLineArguments.
        static bool ProcessArgumentsPrefix(object __instance)
        {
            try
            {
                var setCommandLineArguments = AccessTools.Method(_applicationType, "SetCommandLineArguments", new[] { typeof(string[]) });
                setCommandLineArguments?.Invoke(__instance, new object[] { Environment.GetCommandLineArgs() });

                SafeProcessCommandLineArguments(__instance, Environment.GetCommandLineArgs().Skip(1));

                bool handledFromFile = false;
                var appDataPathForCurrentUser = (string)AccessTools.Property(_applicationType.BaseType, "AppDataPathForCurrentUser")?.GetValue(__instance);
                if (appDataPathForCurrentUser != null)
                {
                    try
                    {
                        var lines = File.ReadAllText(Path.Combine(appDataPathForCurrentUser, "arguments.cfg"))
                            .Split(new[] { "\r\n", "\n", "\t", " " }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length != 0)
                        {
                            SafeProcessCommandLineArguments(__instance, lines);
                            handledFromFile = true;
                        }
                    }
                    catch { }
                }

                if (!handledFromFile)
                {
                    var appDataPathForAllUsers = (string)AccessTools.Property(_applicationType.BaseType, "AppDataPathForAllUsers")?.GetValue(__instance);
                    if (appDataPathForAllUsers != null)
                    {
                        try
                        {
                            var lines = File.ReadAllText(Path.Combine(appDataPathForAllUsers, "arguments.cfg"))
                                .Split(new[] { "\r\n", "\n", "\t", " " }, StringSplitOptions.RemoveEmptyEntries);
                            if (lines.Length != 0)
                            {
                                SafeProcessCommandLineArguments(__instance, lines);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ProcessArguments replacement failed: " + ex);
            }

            return false;
        }

        // Replaces the static Application.SingleInstanceThread(). Mirrors the original's named
        // pipe server loop exactly, except the received arguments are handed to
        // SafeProcessCommandLineArguments on the dispatcher instead of the real
        // ProcessCommandLineArguments.
        static bool SingleInstanceThreadPrefix()
        {
            try
            {
                var application = System.Windows.Application.Current;
                var isClosingProp = AccessTools.Property(_applicationType.BaseType, "IsClosing")
                                     ?? AccessTools.Property(_applicationType, "IsClosing");
                var singleInstanceIdProp = AccessTools.Property(_applicationType, "SingleInstanceId");
                var delayDocumentOpenField = AccessTools.Field(_applicationType, "m_delayDocumentOpen");

                Func<bool> isClosing = () => (bool)isClosingProp.GetValue(application);
                string singleInstanceId = (string)singleInstanceIdProp.GetValue(application);

                while (!isClosing())
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(singleInstanceId))
                        {
                            server.WaitForConnection();
                            while ((bool)delayDocumentOpenField.GetValue(application))
                            {
                                Thread.Sleep(500);
                            }
                            if (isClosing()) continue;

                            try
                            {
                                using (var reader = new BinaryReader(server))
                                {
                                    string text = reader.ReadString();
                                    var arguments = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                    var argsToProcess = arguments.Skip(1).ToArray();
                                    application.Dispatcher.BeginInvoke((Action)(() =>
                                    {
                                        SafeProcessCommandLineArguments(application, argsToProcess);
                                    }));
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("SingleInstanceThread replacement failed: " + ex);
            }

            return false;
        }

        // Safe stand-in for Application.ProcessCommandLineArguments(IEnumerable<string>).
        // Drops "affinity-open-file:" support (needs SharedStorageAccessManager, unsupported
        // under Wine either way); everything else matches the original's behavior.
        static void SafeProcessCommandLineArguments(object instance, IEnumerable<string> arguments)
        {
            string callbackUrl = null;
            var paths = new List<string>();

            foreach (var argument in arguments)
            {
                var lower = argument.ToLowerInvariant();
                if (KnownFlags.Contains(lower)) continue;
                if (lower.StartsWith("affinity-open-file:"))
                {
                    Logger.Warning("Ignoring affinity-open-file: argument (needs SharedStorageAccessManager, unsupported under Wine)");
                    continue;
                }
                if (lower.StartsWith("affinity://"))
                {
                    callbackUrl = argument;
                    continue;
                }
                paths.Add(argument);
            }

            bool mainWindowLoaded = (bool)AccessTools.Field(_applicationType, "m_mainWindowLoaded").GetValue(instance);
            var activateMainWindow = AccessTools.Method(_applicationType, "ActivateMainWindow");
            var loadFiles = AccessTools.Method(_applicationType, "LoadFiles", new[] { typeof(List<string>) });

            if (mainWindowLoaded)
            {
                activateMainWindow.Invoke(instance, null);
                loadFiles.Invoke(instance, new object[] { paths });
            }
            else
            {
                var pathsToLoad = (List<string>)AccessTools.Field(_applicationType, "m_pathsToLoad").GetValue(instance);
                foreach (var p in paths)
                {
                    if (!pathsToLoad.Contains(p)) pathsToLoad.Add(p);
                }
                activateMainWindow.Invoke(instance, null);
            }

            if (!string.IsNullOrEmpty(callbackUrl))
            {
                var openUrl = AccessTools.Method(_applicationType, "OpenUrl", new[] { typeof(string) });
                openUrl.Invoke(instance, new object[] { callbackUrl });
            }
        }
    }
}
