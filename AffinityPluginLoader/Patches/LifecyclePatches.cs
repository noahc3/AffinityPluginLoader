using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace AffinityPluginLoader.Patches
{
    /// <summary>
    /// Harmony postfixes on Affinity lifecycle methods that trigger Stages 2-4.
    /// Applied during Stage 1 by PluginManager.
    /// </summary>
    public static class LifecyclePatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

            if (serifAssembly == null)
            {
                Logger.Error("Serif.Affinity not found for lifecycle patches");
                return;
            }

            var appType = serifAssembly.GetType("Serif.Affinity.Application");
            if (appType == null)
            {
                Logger.Error("Application type not found for lifecycle patches");
                return;
            }

            // Stage 2: after InitialiseServices()
            var initServices = appType.GetMethod("InitialiseServices",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (initServices != null)
            {
                harmony.Patch(initServices,
                    postfix: new HarmonyMethod(typeof(LifecyclePatches), nameof(InitialiseServices_Postfix)));
                Logger.Info("Hooked InitialiseServices for Stage 2");
            }

            // Stage 3: after OnServicesInitialised()
            var onServicesInit = appType.GetMethod("OnServicesInitialised",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (onServicesInit != null)
            {
                harmony.Patch(onServicesInit,
                    postfix: new HarmonyMethod(typeof(LifecyclePatches), nameof(OnServicesInitialised_Postfix)));
                Logger.Info("Hooked OnServicesInitialised for Stage 3");
            }

            // Stage 4: after OnMainWindowLoaded()
            var onMainWindowLoaded = appType.GetMethod("OnMainWindowLoaded",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (onMainWindowLoaded != null)
            {
                harmony.Patch(onMainWindowLoaded,
                    postfix: new HarmonyMethod(typeof(LifecyclePatches), nameof(OnMainWindowLoaded_Postfix)));
                Logger.Info("Hooked OnMainWindowLoaded for Stage 4");
            }
        }

        public static void InitialiseServices_Postfix()
        {
            PluginManager.RunStage(LoadStage.ServicesReady);
        }

        public static void OnServicesInitialised_Postfix()
        {
            PluginManager.RunStage(LoadStage.Ready);
        }

        public static void OnMainWindowLoaded_Postfix()
        {
            PluginManager.RunStage(LoadStage.UiReady);
        }
    }
}
