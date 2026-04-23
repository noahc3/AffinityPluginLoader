using System;
using System.Runtime.InteropServices;
using AffinityPluginLoader.Core;
using AffinityPluginLoader.Native;

namespace WineFix.Patches
{
    /// <summary>
    /// Prevents runaway recursion in d2d_geometry_intersect_bezier_bezier by
    /// returning early when bezier parameter ranges shrink below 1e-6.
    ///
    /// Wine's geometry intersection code recurses without bound on overlapping
    /// or collinear beziers, causing Affinity to hang on complex vector paths
    /// (e.g. editing embedded SVGs).
    ///
    /// Based on:
    /// 0005-d2d1-prevent-runaway-bezier-splitting-and-recursion-.patch
    /// by Arecsu (https://github.com/Arecsu/wine-affinity)
    /// </summary>
    public static class BezierSplitGuardPatch
    {
        // Function signature (from Wine dlls/d2d1/geometry.c):
        // static BOOL d2d_geometry_intersect_bezier_bezier(
        //     struct d2d_geometry *geometry,        // rcx
        //     struct d2d_geometry_intersections *intersections, // rdx
        //     const struct d2d_segment_idx *idx_p,  // r8
        //     float start_p,                        // xmm3
        //     float end_p,                          // [rsp+0x28]
        //     const struct d2d_segment_idx *idx_q,  // [rsp+0x30]
        //     float start_q,                        // [rsp+0x38]
        //     float end_q)                          // [rsp+0x40]

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int IntersectBezierBezierFn(
            IntPtr geometry, IntPtr intersections, IntPtr idx_p,
            float start_p, float end_p, IntPtr idx_q, float start_q, float end_q);

        private static IntersectBezierBezierFn _original;

        // Prologue pattern: 8 pushes + sub rsp,0xe8 — unique across EW 7.9, TKG 11.6, Staging 11.5
        private static readonly byte[] ProloguePattern = {
            0x41, 0x57, 0x41, 0x56, 0x41, 0x55, 0x41, 0x54,
            0x55, 0x57, 0x56, 0x53,
            0x48, 0x81, 0xEC, 0xE8, 0x00, 0x00, 0x00
        };

        private const int PrologueSize = 12; // 8 pushes = 12 bytes, instruction boundary
        private const float MinRange = 1e-6f;

        public static void Apply()
        {
            Logger.Info("Applying bezier split recursion guard (NativeHook)...");

            try
            {
                if (!NativePatch.TryGetSection("d2d1", ".text", out IntPtr textStart, out int textSize))
                {
                    Logger.Warning("Bezier split guard: .text section not found in d2d1.dll");
                    return;
                }

                IntPtr funcAddr = ScanForPattern(textStart, textSize, ProloguePattern);
                if (funcAddr == IntPtr.Zero)
                {
                    Logger.Warning("Bezier split guard: function pattern not found in d2d1.dll " +
                                   "(may already be fixed in this Wine version)");
                    return;
                }

                _original = NativeHook.Hook<IntersectBezierBezierFn>(
                    funcAddr, PrologueSize, new IntersectBezierBezierFn(OnIntersectBezierBezier));

                Logger.Info($"Bezier split recursion guard installed at d2d1+0x{(funcAddr.ToInt64() - GetModuleHandle("d2d1").ToInt64()):X}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to install bezier split recursion guard", ex);
            }
        }

        private static int OnIntersectBezierBezier(
            IntPtr geometry, IntPtr intersections, IntPtr idx_p,
            float start_p, float end_p, IntPtr idx_q, float start_q, float end_q)
        {
            if (end_p - start_p < MinRange || end_q - start_q < MinRange)
            {
                Logger.Debug($"Bezier split guard: early abort (range_p={end_p - start_p:E2}, range_q={end_q - start_q:E2})");
                return 1; // TRUE — bail out
            }

            return _original(geometry, intersections, idx_p,
                start_p, end_p, idx_q, start_q, end_q);
        }

        private static unsafe IntPtr ScanForPattern(IntPtr start, int size, byte[] pattern)
        {
            byte* ptr = (byte*)start;
            int limit = size - pattern.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (ptr[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return (IntPtr)(ptr + i);
            }
            return IntPtr.Zero;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
