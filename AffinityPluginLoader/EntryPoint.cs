using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AffinityPluginLoader.Core;

namespace AffinityPluginLoader
{
    public class EntryPoint
    {
        private static bool _initialized = false;
        private static readonly object _initLock = new object();

        private static bool _serifAffinityLoaded = false;
        private static bool _serifInteropLoaded = false;
        private static bool _patchStageRun = false;

        private static readonly List<DeferredPatch> _deferredPatches = new List<DeferredPatch>();
        private static int _retryCount = 0;
        private const int MaxRetries = 50;

        public static int Initialize(string args)
        {
            try
            {
                new EntryPoint().InitializeInternal();
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Static Initialize error", ex);
                return 1;
            }
        }

        private void InitializeInternal()
        {
            lock (_initLock)
            {
                if (_initialized) return;
                _initialized = true;
            }

            Logger.Initialize();
            Logger.Info($"APL initializing... {DateTime.UtcNow}");
            Logger.Debug($"Current AppDomain: {AppDomain.CurrentDomain.FriendlyName}");

            try
            {
                var defaultDomain = GetDefaultAppDomain();
                if (defaultDomain != null && defaultDomain != AppDomain.CurrentDomain)
                {
                    Logger.Info($"Switching to default AppDomain: {defaultDomain.FriendlyName}");
                    var patcher = (DefaultDomainPatcher)defaultDomain.CreateInstanceAndUnwrap(
                        typeof(DefaultDomainPatcher).Assembly.FullName,
                        typeof(DefaultDomainPatcher).FullName);
                    patcher.Initialize();
                }
                else
                {
                    Logger.Info("Running in current AppDomain");
                    StartPipeline();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during initialization", ex);
            }
        }

        internal static void StartPipeline()
        {
            PluginManager.RunStageLoad();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                CheckAssembly(asm);

            // Always subscribe — needed for both Stage 1 trigger and deferred patch retries
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

            if (!_patchStageRun)
                Logger.Info("Waiting for Serif assemblies to load...");
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            CheckAssembly(args.LoadedAssembly);

            if (_patchStageRun && _deferredPatches.Count > 0)
                RetryDeferredPatches();
        }

        private static void CheckAssembly(Assembly assembly)
        {
            if (_patchStageRun) return;

            var name = assembly.GetName().Name;
            if (name == "Serif.Affinity")
            {
                _serifAffinityLoaded = true;
                Logger.Debug("Detected Serif.Affinity assembly load");
            }
            else if (name == "Serif.Interop.Persona")
            {
                _serifInteropLoaded = true;
                Logger.Debug("Detected Serif.Interop.Persona assembly load");
            }

            if (_serifAffinityLoaded && _serifInteropLoaded)
                TryRunPatchStage();
        }

        private static void TryRunPatchStage()
        {
            try
            {
                var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .First(a => a.GetName().Name == "Serif.Affinity");
                serifAssembly.GetType("Serif.Affinity.Application", throwOnError: true);

                _patchStageRun = true;
                Logger.Info("Serif types resolvable, running Stage 1");
                PluginManager.RunStagePatch();
            }
            catch
            {
                Logger.Debug("Serif types not yet resolvable, waiting...");
            }
        }

        /// <summary>
        /// Called by PluginManager when a patch fails with TypeLoadException.
        /// Automatically queues it for retry on subsequent assembly loads.
        /// </summary>
        internal static void AddDeferredPatch(string description, Action patchAction)
        {
            _deferredPatches.Add(new DeferredPatch { Description = description, Action = patchAction });
            Logger.Info($"Auto-deferred patch: {description} (will retry on next assembly load)");
        }

        private static void RetryDeferredPatches()
        {
            if (_retryCount >= MaxRetries)
            {
                Logger.Error($"Deferred patch retry limit ({MaxRetries}) reached, giving up on {_deferredPatches.Count} patches:");
                foreach (var p in _deferredPatches)
                    Logger.Error($"  - {p.Description}");
                _deferredPatches.Clear();
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                return;
            }

            _retryCount++;
            var remaining = new List<DeferredPatch>();

            foreach (var deferred in _deferredPatches)
            {
                try
                {
                    deferred.Action();
                    Logger.Info($"Deferred patch succeeded: {deferred.Description}");
                }
                catch (Exception ex) when (IsTypeLoadException(ex))
                {
                    remaining.Add(deferred);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Deferred patch failed permanently: {deferred.Description}", ex);
                }
            }

            _deferredPatches.Clear();
            _deferredPatches.AddRange(remaining);

            if (_deferredPatches.Count == 0)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                Logger.Debug("All deferred patches resolved, unsubscribed from AssemblyLoad");
            }
        }

        /// <summary>
        /// Check if an exception (or its inner) is a TypeLoadException — the signature
        /// of a transitive dependency not yet available.
        /// </summary>
        internal static bool IsTypeLoadException(Exception ex)
        {
            return ex is TypeLoadException
                || ex.InnerException is TypeLoadException
                || ex is ReflectionTypeLoadException;
        }

        private AppDomain GetDefaultAppDomain()
        {
            try
            {
                var method = typeof(AppDomain).GetMethod("GetDefaultDomain",
                    BindingFlags.Static | BindingFlags.NonPublic);
                return (AppDomain)method?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting default AppDomain: {ex.Message}");
                return null;
            }
        }

        private class DeferredPatch
        {
            public string Description;
            public Action Action;
        }
    }

    public class DefaultDomainPatcher : MarshalByRefObject
    {
        public void Initialize()
        {
            Logger.Debug($"DefaultDomainPatcher running in AppDomain: {AppDomain.CurrentDomain.FriendlyName}");
            EntryPoint.StartPipeline();
        }
    }
}
