using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace AffinityPluginLoader
{
    public class EntryPoint
    {
        private static bool _initialized = false;
        private static readonly object _initLock = new object();
        
        /// <summary>
        /// Static entry point for native bootstrap
        /// Called by AffinityBootstrap.dll via CLR hosting: ExecuteInDefaultAppDomain
        /// </summary>
        public static int Initialize(string args)
        {
            try
            {
                var instance = new EntryPoint();
                instance.InitializeInternal();
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Static Initialize error", ex);
                return 1;
            }
        }
        
        /// <summary>
        /// Initialize the plugin loader
        /// This is the main entry point called by the native bootstrap
        /// </summary>
        private void InitializeInternal()
        {
            lock (_initLock)
            {
                if (_initialized)
                    return;

                _initialized = true;
            }

            // Initialize logger first
            Logger.Initialize();

            Logger.Info($"APL initializing... {DateTime.UtcNow}");
            Logger.Debug($"Current AppDomain: {AppDomain.CurrentDomain.FriendlyName}");
            
            try
            {
                // Get the default AppDomain (where Affinity's code runs)
                var defaultDomain = GetDefaultAppDomain();
                
                if (defaultDomain != null && defaultDomain != AppDomain.CurrentDomain)
                {
                    Logger.Info($"Switching to default AppDomain: {defaultDomain.FriendlyName}");

                    // Since AffinityPluginLoader.dll is now in Affinity's folder,
                    // the default domain can find it naturally
                    var patcherType = typeof(DefaultDomainPatcher);
                    var patcher = (DefaultDomainPatcher)defaultDomain.CreateInstanceAndUnwrap(
                        patcherType.Assembly.FullName,
                        patcherType.FullName);

                    patcher.Initialize();
                    Logger.Info($"APL initialized in default AppDomain");
                }
                else
                {
                    // Fallback: run in current domain
                    Logger.Info($"Running in current AppDomain");
                    var harmony = new Harmony("dev.ncuroe.affinitypluginloader");
                    Core.PluginManager.Initialize(harmony);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during initialization", ex);
            }
        }
        
        private AppDomain GetDefaultAppDomain()
        {
            try
            {
                var type = Type.GetType("System.AppDomain");
                var method = type?.GetMethod("GetDefaultDomain", BindingFlags.Static | BindingFlags.NonPublic);
                if (method != null)
                {
                    return (AppDomain)method.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting default AppDomain: {ex.Message}");
            }
            
            return null;
        }
    }
    
    // This class executes in the default AppDomain where Affinity's code runs
    public class DefaultDomainPatcher : MarshalByRefObject
    {
        public void Initialize()
        {
            try
            {
                Logger.Debug($"DefaultDomainPatcher running in AppDomain: {AppDomain.CurrentDomain.FriendlyName}");

                var harmony = new Harmony("dev.ncuroe.affinitypluginloader");
                Core.PluginManager.Initialize(harmony);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in DefaultDomainPatcher", ex);
            }
        }
    }
}
