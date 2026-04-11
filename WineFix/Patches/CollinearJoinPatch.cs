using System;
using System.Runtime.InteropServices;
using AffinityPluginLoader.Core;

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
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize,
            uint flNewProtect, out uint lpflOldProtect);

        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        /// <summary>
        /// Scans d2d1.dll's .text section for a `movss xmm0, [rip+disp]` instruction
        /// that loads 25.0f (0x41c80000), and replaces it with `xorps xmm0, xmm0` (0.0f).
        /// </summary>
        public static unsafe void Apply()
        {
            try
            {
                IntPtr d2d1Base = GetModuleHandle("d2d1");
                if (d2d1Base == IntPtr.Zero)
                {
                    Logger.Warning("Collinear join fix: d2d1.dll not loaded, skipping");
                    return;
                }

                // Parse PE headers to find .text section bounds
                byte* basePtr = (byte*)d2d1Base;
                int peOffset = *(int*)(basePtr + 0x3C);
                short numSections = *(short*)(basePtr + peOffset + 6);
                short optHeaderSize = *(short*)(basePtr + peOffset + 20);
                byte* sectionTable = basePtr + peOffset + 24 + optHeaderSize;

                byte* textStart = null;
                int textSize = 0;

                for (int i = 0; i < numSections; i++)
                {
                    byte* sec = sectionTable + i * 40;
                    if (sec[0] == '.' && sec[1] == 't' && sec[2] == 'e' && sec[3] == 'x' && sec[4] == 't')
                    {
                        textSize = *(int*)(sec + 8);
                        int textRva = *(int*)(sec + 12);
                        textStart = basePtr + textRva;
                        break;
                    }
                }

                if (textStart == null)
                {
                    Logger.Warning("Collinear join fix: .text section not found in d2d1.dll");
                    return;
                }

                // Scan for: f3 0f 10 05 [4-byte disp] where target dword == 0x41c80000 (25.0f)
                const uint FLOAT_25 = 0x41c80000;
                byte* patchSite = null;

                for (int i = 0; i < textSize - 8; i++)
                {
                    if (textStart[i] == 0xf3 && textStart[i + 1] == 0x0f &&
                        textStart[i + 2] == 0x10 && textStart[i + 3] == 0x05)
                    {
                        int disp = *(int*)(textStart + i + 4);
                        byte* target = textStart + i + 8 + disp;
                        if (*(uint*)target == FLOAT_25)
                        {
                            patchSite = textStart + i;
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

                // Replace `movss xmm0, [25.0f]` (8 bytes) with `xorps xmm0, xmm0` (3 bytes) + 5 NOPs
                VirtualProtect((IntPtr)patchSite, (UIntPtr)8, PAGE_EXECUTE_READWRITE, out uint oldProtect);
                patchSite[0] = 0x0f; // xorps xmm0, xmm0
                patchSite[1] = 0x57;
                patchSite[2] = 0xc0;
                patchSite[3] = 0x90; // nop
                patchSite[4] = 0x90;
                patchSite[5] = 0x90;
                patchSite[6] = 0x90;
                patchSite[7] = 0x90;
                VirtualProtect((IntPtr)patchSite, (UIntPtr)8, oldProtect, out _);

                Logger.Info($"Collinear join fix applied at d2d1+0x{(patchSite - basePtr):X}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply collinear join fix", ex);
            }
        }
    }
}
