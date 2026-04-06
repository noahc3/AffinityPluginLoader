namespace AffinityPluginLoader
{
    /// <summary>
    /// Stages of the APL plugin loading pipeline.
    /// </summary>
    public enum LoadStage
    {
        /// <summary>
        /// Plugin discovery and settings init. No Affinity types available.
        /// </summary>
        Load = 0,

        /// <summary>
        /// Serif assemblies loaded. Apply Harmony patches here.
        /// APL settings are available via context.Settings.
        /// </summary>
        Patch = 1,

        /// <summary>
        /// Affinity's InitialiseServices() complete. All Affinity services and settings available.
        /// </summary>
        ServicesReady = 2,

        /// <summary>
        /// Affinity's OnServicesInitialised() complete. Full runtime including native engine.
        /// </summary>
        Ready = 3,

        /// <summary>
        /// Main window loaded and visible. Full UI tree available.
        /// </summary>
        UiReady = 4,

        /// <summary>
        /// Startup fully complete. Splash hidden, app idle, m_startupComplete = true.
        /// </summary>
        StartupComplete = 5
    }
}
