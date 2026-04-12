using System;
using System.Runtime.InteropServices;
using AffinityPluginLoader.Core;
using AffinityPluginLoader.Native;

namespace WineFix.Patches
{
    /// <summary>
    /// Fixes collinear outline join placing vertices 25 units away on smooth
    /// continuations from bezier subdivision.
    ///
    /// Wine's d2d_geometry_outline_add_join unconditionally places join vertices
    /// 25 units away when two adjacent segments are collinear (ccw == 0). This is
    /// correct for hairpin reversals but produces visible spikes on smooth curve
    /// continuations. Fix: patch the 25.0f constant load to 0.0f in d2d1.dll memory.
    ///
    /// Validated on 7.9 EW & 11.6 TKG Staging
    ///
    /// Based on:
    /// 0009-d2d1-Fix-collinear-outline-join-placing-vertices-25-.patch
    /// by Arecsu (https://github.com/Arecsu/wine-affinity)
    /// </summary>
    public static class CollinearJoinPatch
    {
        /// <summary>
        /// Scans d2d1.dll's .text section for a `movss xmm0, [rip+disp]` instruction
        /// that loads 25.0f (0x41c80000), and replaces it with `xorps xmm0, xmm0` (0.0f).
        /// </summary>
        public static unsafe void Apply()
        {
            try
            {
                if (!NativePatch.TryGetSection("d2d1", ".text", out IntPtr start, out int size))
                {
                    Logger.Warning("Collinear join fix: .text section not found in d2d1.dll");
                    return;
                }

                // Scan for: f3 0f 10 05 [4-byte disp] where the RIP-relative target == 25.0f
                const uint FLOAT_25 = 0x41c80000;
                byte* ptr = (byte*)start;
                byte* patchSite = null;

                for (int i = 0; i < size - 8; i++)
                {
                    if (ptr[i] == 0xf3 && ptr[i + 1] == 0x0f &&
                        ptr[i + 2] == 0x10 && ptr[i + 3] == 0x05)
                    {
                        int disp = *(int*)(ptr + i + 4);
                        byte* target = ptr + i + 8 + disp;
                        if (*(uint*)target == FLOAT_25)
                        {
                            patchSite = ptr + i;
                            break;
                        }
                    }
                }

                if (patchSite == null)
                {
                    Logger.Warning("Collinear join fix: 25.0f load pattern not found in d2d1.dll " +
                                   "(may already be fixed in this Wine version)");
                    return;
                }

                // Replace `movss xmm0, [25.0f]` (8 bytes) with `xorps xmm0, xmm0` (3) + NOPs (5)
                byte[] replacement = { 0x0f, 0x57, 0xc0, 0x90, 0x90, 0x90, 0x90, 0x90 };

                VirtualProtect((IntPtr)patchSite, (UIntPtr)8, 0x40, out uint oldProtect);
                Marshal.Copy(replacement, 0, (IntPtr)patchSite, 8);
                VirtualProtect((IntPtr)patchSite, (UIntPtr)8, oldProtect, out _);

                IntPtr d2d1Base = GetModuleHandle("d2d1");
                Logger.Info($"Collinear join fix applied at d2d1+0x{(patchSite - (byte*)d2d1Base):X}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply collinear join fix", ex);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize,
            uint flNewProtect, out uint lpflOldProtect);
    }
}
