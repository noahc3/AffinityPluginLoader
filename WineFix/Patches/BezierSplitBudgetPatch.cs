using System;
using System.Runtime.InteropServices;
using AffinityPluginLoader.Core;
using AffinityPluginLoader.Native;

namespace WineFix.Patches
{
    /// <summary>
    /// Caps the number of bezier control triangle splits in Wine's geometry
    /// processing to prevent unbounded segment growth and application freezes.
    ///
    /// Wine's d2d_geometry_resolve_beziers (inlined in d2d_geometry_sink_Close)
    /// calls d2d_geometry_split_bezier in an unbounded loop. Pathological
    /// geometries (e.g. overlapping beziers in embedded SVGs) cause the loop
    /// to run forever, hanging Affinity.
    ///
    /// This patch hooks d2d_geometry_split_bezier with a thread-local call
    /// counter that returns failure after 512 calls, causing the caller to
    /// stop splitting.
    ///
    /// Based on:
    /// 0005-d2d1-prevent-runaway-bezier-splitting-and-recursion-.patch
    /// by Arecsu (https://github.com/Arecsu/wine-affinity)
    /// </summary>
    public static class BezierSplitBudgetPatch
    {
        // d2d_geometry_split_bezier.isra.0(figures_ptr, segment_idx_ptr) -> BOOL
        // .isra = GCC interprocedural SRA; takes two pointer args after optimization
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SplitBezierFn(IntPtr figures, IntPtr idx);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SinkCloseFn(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreatePathGeometryFn(IntPtr factory, out IntPtr pathGeometry);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int OpenFn(IntPtr pathGeometry, out IntPtr geometrySink);

        private static SplitBezierFn _original;
        private static SinkCloseFn _origClose;

        // Prologue: push rsi (56) + push rbx (53) + sub rsp,0x48 (48 83 ec 48)
        // + first 4 bytes of movq xmm2,[rip+disp] (f3 0f 7e 15) for pattern uniqueness
        // Only the first 6 bytes are relocated (before the RIP-relative instruction)
        private static readonly byte[] SplitBezierPattern = {
            0x56, 0x53, 0x48, 0x83, 0xEC, 0x48, 0xF3, 0x0F, 0x7E, 0x15
        };

        private const int PrologueSize = 6; // push+push+sub = 6 bytes (>= 5 for rel32 jmp)
        private const int MaxSplitsPerOperation = 512;

        // ID2D1SimplifiedGeometrySink::Close = vtable index 9
        private const int Sink_Close = 9;
        // ID2D1Factory::CreatePathGeometry = vtable index 10
        private const int Factory_CreatePathGeometry = 10;
        // ID2D1PathGeometry::Open = vtable index 17
        private const int PathGeometry_Open = 17;

        [ThreadStatic] private static int _splitCount;

        [DllImport("d2d1.dll")]
        private static extern int D2D1CreateFactory(
            int factoryType, [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            IntPtr factoryOptions, out IntPtr factory);

        private static readonly Guid IID_ID2D1Factory =
            new Guid("06152247-6f50-465a-9245-118bfd3b6007");

        public static void Apply()
        {
            Logger.Info("Applying bezier split budget (NativeHook + ComHook)...");

            try
            {
                // 1. Hook d2d_geometry_split_bezier via NativeHook
                if (!NativePatch.TryGetSection("d2d1", ".text", out IntPtr textStart, out int textSize))
                {
                    Logger.Warning("Bezier split budget: .text section not found in d2d1.dll");
                    return;
                }

                IntPtr funcAddr = ScanForPattern(textStart, textSize, SplitBezierPattern);
                if (funcAddr == IntPtr.Zero)
                {
                    Logger.Warning("Bezier split budget: split_bezier pattern not found in d2d1.dll");
                    return;
                }

                _original = NativeHook.Hook<SplitBezierFn>(
                    funcAddr, PrologueSize, new SplitBezierFn(OnSplitBezier));

                Logger.Info($"Bezier split budget: split_bezier hooked at d2d1+0x{(funcAddr.ToInt64() - GetModuleHandle("d2d1").ToInt64()):X}");

                // 2. Hook ID2D1GeometrySink::Close via ComHook to reset counter
                IntPtr factory = IntPtr.Zero, pathGeometry = IntPtr.Zero, sink = IntPtr.Zero;
                try
                {
                    int hr = D2D1CreateFactory(0, IID_ID2D1Factory, IntPtr.Zero, out factory);
                    if (hr < 0) return;

                    var createPG = ComHook.GetMethod<CreatePathGeometryFn>(factory, Factory_CreatePathGeometry);
                    hr = createPG(factory, out pathGeometry);
                    if (hr < 0) return;

                    var open = ComHook.GetMethod<OpenFn>(pathGeometry, PathGeometry_Open);
                    hr = open(pathGeometry, out sink);
                    if (hr < 0) return;

                    _origClose = ComHook.Hook<SinkCloseFn>(sink, Sink_Close, new SinkCloseFn(OnClose));
                    Logger.Info("Bezier split budget: sink Close hooked for counter reset");
                }
                finally
                {
                    if (sink != IntPtr.Zero) Marshal.Release(sink);
                    if (pathGeometry != IntPtr.Zero) Marshal.Release(pathGeometry);
                    if (factory != IntPtr.Zero) Marshal.Release(factory);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to install bezier split budget", ex);
            }
        }

        private static int OnSplitBezier(IntPtr figures, IntPtr idx)
        {
            if (++_splitCount > MaxSplitsPerOperation)
            {
                if (_splitCount == MaxSplitsPerOperation + 1)
                    Logger.Debug($"Bezier split budget: capped at {MaxSplitsPerOperation} splits");
                return 0; // FALSE — tell caller to stop splitting
            }

            return _original(figures, idx);
        }

        private static int OnClose(IntPtr self)
        {
            _splitCount = 0;
            return _origClose(self);
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
