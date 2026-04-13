using System;
using System.Runtime.InteropServices;
using AffinityPluginLoader.Core;
using AffinityPluginLoader.Native;

namespace WineFix.Patches
{
    /// <summary>
    /// Stubs ID2D1PathGeometry1::Widen to return S_OK with an empty closed sink
    /// instead of E_NOTIMPL. Prevents Affinity from hanging indefinitely when
    /// clicking stroked SVG vectors.
    ///
    /// Stroke rendering will be absent but the application remains usable.
    ///
    /// Based on:
    /// 0002-d2d1-stub-Widen-with-empty-geometry-to-prevent-calle.patch
    /// by Arecsu (https://github.com/Arecsu/wine-affinity)
    /// </summary>
    public static class WidenStubPatch
    {
        // ID2D1Geometry vtable layout (from d2d1.h):
        // IUnknown(3) + ID2D1Resource(1) + GetBounds(4) + GetWidenedBounds(5) +
        // StrokeContainsPoint(6) + FillContainsPoint(7) + CompareWithGeometry(8) +
        // Simplify(9) + Tessellate(10) + CombineWithGeometry(11) + Outline(12) +
        // ComputeArea(13) + ComputeLength(14) + ComputePointAtLength(15) + Widen(16)
        private const int Geometry_Widen = 16;

        // ID2D1SimplifiedGeometrySink vtable:
        // IUnknown(3) + SetFillMode(3) + SetSegmentFlags(4) + BeginFigure(5) +
        // AddLines(6) + AddBeziers(7) + EndFigure(8) + Close(9)
        private const int Sink_SetFillMode = 3;
        private const int Sink_Close = 9;

        private const int D2D1_FILL_MODE_WINDING = 1;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int WidenFn(IntPtr self, float strokeWidth, IntPtr strokeStyle,
            IntPtr transform, float tolerance, IntPtr sink);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetFillModeFn(IntPtr self, int fillMode);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CloseFn(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreatePathGeometryFn(IntPtr factory, out IntPtr pathGeometry);

        [DllImport("d2d1.dll")]
        private static extern int D2D1CreateFactory(
            int factoryType, [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            IntPtr factoryOptions, out IntPtr factory);

        private static readonly Guid IID_ID2D1Factory =
            new Guid("06152247-6f50-465a-9245-118bfd3b6007");

        private const int Factory_CreatePathGeometry = 10;

        public static void Apply()
        {
            Logger.Info("Applying Widen stub fix (COM vtable hook)...");

            IntPtr factory = IntPtr.Zero;
            IntPtr pathGeometry = IntPtr.Zero;

            try
            {
                int hr = D2D1CreateFactory(0, IID_ID2D1Factory, IntPtr.Zero, out factory);
                if (hr < 0 || factory == IntPtr.Zero)
                {
                    Logger.Error($"Widen stub: D2D1CreateFactory failed: hr=0x{hr:X8}");
                    return;
                }

                var createPathGeometry = ComHook.GetMethod<CreatePathGeometryFn>(
                    factory, Factory_CreatePathGeometry);
                hr = createPathGeometry(factory, out pathGeometry);
                if (hr < 0 || pathGeometry == IntPtr.Zero)
                {
                    Logger.Error($"Widen stub: CreatePathGeometry failed: hr=0x{hr:X8}");
                    return;
                }

                ComHook.Hook<WidenFn>(pathGeometry, Geometry_Widen, new WidenFn(OnWiden));

                Logger.Info("Widen stub fix installed (ID2D1PathGeometry vtable patched)");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to install Widen stub fix", ex);
            }
            finally
            {
                if (pathGeometry != IntPtr.Zero) Marshal.Release(pathGeometry);
                if (factory != IntPtr.Zero) Marshal.Release(factory);
            }
        }

        private static int OnWiden(IntPtr self, float strokeWidth, IntPtr strokeStyle,
            IntPtr transform, float tolerance, IntPtr sink)
        {
            if (sink == IntPtr.Zero)
                return 0; // S_OK

            try
            {
                var setFillMode = ComHook.GetMethod<SetFillModeFn>(sink, Sink_SetFillMode);
                var close = ComHook.GetMethod<CloseFn>(sink, Sink_Close);

                setFillMode(sink, D2D1_FILL_MODE_WINDING);
                close(sink);
            }
            catch
            {
                // Swallow — better to return S_OK with a possibly-unclosed sink
                // than to let the app hang on E_NOTIMPL
            }

            return 0; // S_OK
        }
    }
}
