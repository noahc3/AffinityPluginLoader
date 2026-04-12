using System;
using System.Runtime.InteropServices;
using AffinityPluginLoader.Core;

namespace AffinityPluginLoader.Native
{
    /// <summary>
    /// Utilities for patching native DLLs in memory at runtime.
    /// </summary>
    public static class NativePatch
    {
        /// <summary>
        /// Get the start address and size of a named section in a loaded module.
        /// </summary>
        /// <param name="moduleName">DLL name (e.g. "d2d1")</param>
        /// <param name="sectionName">Section name (e.g. ".text")</param>
        /// <param name="start">Start address of the section in memory</param>
        /// <param name="size">Size of the section in bytes</param>
        /// <returns>True if the module and section were found</returns>
        public static unsafe bool TryGetSection(string moduleName, string sectionName,
            out IntPtr start, out int size)
        {
            start = IntPtr.Zero;
            size = 0;

            IntPtr moduleBase = GetModuleHandle(moduleName);
            if (moduleBase == IntPtr.Zero)
                return false;

            byte* basePtr = (byte*)moduleBase;
            int peOffset = *(int*)(basePtr + 0x3C);
            short numSections = *(short*)(basePtr + peOffset + 6);
            short optHeaderSize = *(short*)(basePtr + peOffset + 20);
            byte* sectionTable = basePtr + peOffset + 24 + optHeaderSize;

            for (int i = 0; i < numSections; i++)
            {
                byte* sec = sectionTable + i * 40;
                bool match = true;
                for (int j = 0; j < sectionName.Length && j < 8; j++)
                {
                    if (sec[j] != (byte)sectionName[j]) { match = false; break; }
                }
                if (match && (sectionName.Length >= 8 || sec[sectionName.Length] == 0))
                {
                    size = *(int*)(sec + 8);
                    int rva = *(int*)(sec + 12);
                    start = (IntPtr)(basePtr + rva);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Scan a memory region for a byte pattern with optional wildcard mask,
        /// and replace the first match with the given bytes.
        /// </summary>
        /// <param name="moduleName">DLL name</param>
        /// <param name="sectionName">Section to scan</param>
        /// <param name="pattern">Byte pattern to find</param>
        /// <param name="replacement">Bytes to write at the match site</param>
        /// <param name="mask">
        /// Optional mask the same length as pattern. 0xFF = must match, 0x00 = wildcard.
        /// Null means all bytes must match exactly.
        /// </param>
        /// <returns>The address where the patch was applied, or IntPtr.Zero if not found</returns>
        public static unsafe IntPtr Patch(string moduleName, string sectionName,
            byte[] pattern, byte[] replacement, byte[] mask = null)
        {
            if (!TryGetSection(moduleName, sectionName, out IntPtr start, out int size))
            {
                Logger.Warning($"NativePatch: section {sectionName} not found in {moduleName}");
                return IntPtr.Zero;
            }

            byte* ptr = (byte*)start;
            int scanLimit = size - pattern.Length;

            for (int i = 0; i <= scanLimit; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    byte m = mask != null ? mask[j] : (byte)0xFF;
                    if ((ptr[i + j] & m) != (pattern[j] & m)) { found = false; break; }
                }

                if (found)
                {
                    IntPtr site = (IntPtr)(ptr + i);
                    VirtualProtect(site, (UIntPtr)replacement.Length,
                        PAGE_EXECUTE_READWRITE, out uint oldProtect);
                    Marshal.Copy(replacement, 0, site, replacement.Length);
                    VirtualProtect(site, (UIntPtr)replacement.Length,
                        oldProtect, out _);

                    IntPtr moduleBase = GetModuleHandle(moduleName);
                    Logger.Debug($"NativePatch: patched {moduleName}+0x{(ptr + i - (byte*)moduleBase):X}");
                    return site;
                }
            }

            return IntPtr.Zero;
        }

        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize,
            uint flNewProtect, out uint lpflOldProtect);
    }
}
