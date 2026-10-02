using System;
using System.IO;
using System.Runtime.InteropServices;
using AffinityPluginLoader.Core;

namespace WineFix.Patches
{
    /// <summary>
    /// Keeps libplugins.dll loaded for the lifetime of the process.
    ///
    /// Affinity loads and frees its plugin host more than once per session, and
    /// something keeps calling into it after the last reference is dropped. A
    /// crash dump taken on a JPEG import showed the main thread taking an
    /// access violation trying to *execute* 0x6fffc524d6c0 -- an address inside
    /// libplugins.dll, which by then was in the dump's unloaded-module list with
    /// the memory marked FREE/NOACCESS. The call arrived from libscripting.dll
    /// via Serif.Interop.Persona.dll, so the scripting engine was still holding
    /// an entry point into a module the loader had already released. The module
    /// appeared in that list twice at the same base address, so this is a
    /// load/free cycle rather than a one-off teardown.
    ///
    /// Taking one reference that is never released makes the refcount unable to
    /// reach zero, so the pages stay mapped and the stale pointer stays valid.
    /// It does not fix whatever drops the last reference -- it removes the
    /// consequence. Presumably something on Windows holds this module up in a
    /// way Wine's loader does not, since the same build does not crash there.
    ///
    /// This is not a Harmony patch: the caller is native code with no managed
    /// frame to intercept.
    /// </summary>
    public static class PluginHostPin
    {
        private const string ModuleName = "libplugins.dll";

        // Held for the lifetime of the process. Never freed -- that is the point.
        private static IntPtr _handle = IntPtr.Zero;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string lpLibFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandleW(string lpModuleName);

        public static void Apply()
        {
            if (_handle != IntPtr.Zero)
            {
                Logger.Info("Plugin host already held, nothing to do");
                return;
            }

            // Full path rather than the bare name: the bare name would be resolved
            // against the search path, and the point is to reference the copy
            // sitting beside Affinity.exe -- the one that gets unloaded.
            string path = ModuleName;
            try
            {
                var dir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(dir))
                {
                    var candidate = Path.Combine(dir, ModuleName);
                    if (File.Exists(candidate)) path = candidate;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("Could not resolve the application directory, falling back to the module name: " + ex.Message);
            }

            bool alreadyLoaded = GetModuleHandleW(ModuleName) != IntPtr.Zero;

            _handle = LoadLibraryW(path);
            if (_handle == IntPtr.Zero)
            {
                Logger.Error("Failed to hold " + ModuleName + " (error " + Marshal.GetLastWin32Error() + ")");
                return;
            }

            // When it was already loaded this only raises the refcount and no
            // DllMain runs; when it was not, we have just loaded it earlier than
            // Affinity would have.
            Logger.Info("Holding a reference to " + ModuleName + " (" +
                        (alreadyLoaded ? "was already loaded" : "loaded early") + ")");
        }
    }
}
