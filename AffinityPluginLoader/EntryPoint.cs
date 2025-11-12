using System;
using System.IO;
using System.Reflection;
using HarmonyLib;

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
                FileLog.Log($"Static Initialize error: {ex.Message}\n{ex.StackTrace}\n");
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
            
            FileLog.Log($"AffinityPluginLoader initializing... {DateTime.UtcNow}\n");
            FileLog.Log($"Current AppDomain: {AppDomain.CurrentDomain.FriendlyName}\n");
            
            try
            {
                // Get the default AppDomain (where Affinity's code runs)
                var defaultDomain = GetDefaultAppDomain();
                
                if (defaultDomain != null && defaultDomain != AppDomain.CurrentDomain)
                {
                    FileLog.Log($"Switching to default AppDomain: {defaultDomain.FriendlyName}\n");
                    
                    // Since AffinityPluginLoader.dll is now in Affinity's folder,
                    // the default domain can find it naturally
                    var patcherType = typeof(DefaultDomainPatcher);
                    var patcher = (DefaultDomainPatcher)defaultDomain.CreateInstanceAndUnwrap(
                        patcherType.Assembly.FullName,
                        patcherType.FullName);
                    
                    patcher.Initialize();
                    FileLog.Log($"AffinityPluginLoader initialized in default AppDomain\n");
                }
                else
                {
                    // Fallback: run in current domain
                    FileLog.Log($"Running in current AppDomain\n");
                    var harmony = new Harmony("dev.ncuroe.affinitypluginloader");
                    Core.PluginManager.Initialize(harmony);
                }
            }
            catch (Exception ex)
            {
                FileLog.Log($"Error: {ex.Message}\n{ex.StackTrace}\n");
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
                FileLog.Log($"Error getting default AppDomain: {ex.Message}\n");
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
                HarmonyLib.FileLog.Log($"DefaultDomainPatcher running in AppDomain: {AppDomain.CurrentDomain.FriendlyName}\n");
                
                var harmony = new Harmony("dev.ncuroe.affinitypluginloader");
                Core.PluginManager.Initialize(harmony);
            }
            catch (Exception ex)
            {
                HarmonyLib.FileLog.Log($"Error in DefaultDomainPatcher: {ex.Message}\n{ex.StackTrace}\n");
            }
        }
    }
}
