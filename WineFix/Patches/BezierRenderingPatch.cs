using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using AffinityPluginLoader.Core;
using AffinityPluginLoader.Native;

namespace WineFix.Patches
{
    /// <summary>
    /// Fixes incorrect cubic Bézier rendering under Wine by hooking the
    /// ID2D1GeometrySink COM vtable. Replaces AddBezier(s) with adaptive
    /// cubic-to-quadratic subdivision so Wine's quadratic-only renderer
    /// produces correct output.
    ///
    /// Algorithm from:
    /// patches/0008-d2d1-implement-cubic-to-quadratic-Bézier-subdivision.patch
    /// </summary>
    public static class BezierRenderingPatch
    {
        // ── D2D1 structures ──

        [StructLayout(LayoutKind.Sequential)]
        private struct Point2F { public float X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct BezierSegment { public Point2F Point1, Point2, Point3; }

        [StructLayout(LayoutKind.Sequential)]
        private struct QuadBezierSegment { public Point2F Point1, Point2; }

        // ── COM vtable indices (fixed by interface ABI, stable across all Wine/Windows versions) ──

        // ID2D1Factory : IUnknown(3) + ReloadSystemMetrics, GetDesktopDpi,
        //   CreateRectangleGeometry, CreateRoundedRectangleGeometry, CreateEllipseGeometry,
        //   CreateGeometryGroup, CreateTransformedGeometry, CreatePathGeometry[10], ...
        private const int Factory_CreatePathGeometry = 10;

        // ID2D1PathGeometry : ID2D1Geometry(17) + Open[17], Stream, GetSegmentCount, GetFigureCount
        //   ID2D1Geometry : ID2D1Resource(4) + GetBounds, GetWidenedBounds, StrokeContainsPoint,
        //     FillContainsPoint, CompareWithGeometry, Simplify, Tessellate, CombineWithGeometry,
        //     Outline, ComputeArea, ComputeLength, ComputePointAtLength, Widen
        private const int PathGeometry_Open = 17;

        // ID2D1GeometrySink : ID2D1SimplifiedGeometrySink(10) + AddLine[10], AddBezier[11],
        //   AddQuadraticBezier[12], AddQuadraticBeziers[13], AddArc[14]
        //   ID2D1SimplifiedGeometrySink : IUnknown(3) + SetFillMode, SetSegmentFlags,
        //     BeginFigure[5], AddLines[6], AddBeziers[7], EndFigure, Close
        private const int Sink_BeginFigure = 5;
        private const int Sink_AddLines = 6;
        private const int Sink_AddBeziers = 7;
        private const int Sink_AddLine = 10;
        private const int Sink_AddBezier = 11;
        private const int Sink_AddQuadBezier = 12;
        private const int Sink_AddQuadBeziers = 13;
        private const int Sink_AddArc = 14;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void BeginFigureFn(IntPtr self, Point2F startPoint, int figureBegin);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void AddLinesFn(IntPtr self, IntPtr points, uint count);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void AddLineFn(IntPtr self, Point2F point);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void AddBeziersFn(IntPtr self, IntPtr beziers, uint count);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void AddBezierFn(IntPtr self, ref BezierSegment bezier);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void AddQuadBezierFn(IntPtr self, ref QuadBezierSegment bezier);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void AddQuadBeziersFn(IntPtr self, IntPtr beziers, uint count);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void AddArcFn(IntPtr self, IntPtr arc);

        // ── Saved original methods ──

        private static BeginFigureFn _origBeginFigure;
        private static AddLinesFn _origAddLines;
        private static AddLineFn _origAddLine;
        private static AddBeziersFn _origAddBeziers;
        private static AddBezierFn _origAddBezier;
        private static AddQuadBezierFn _origAddQuadBezier;
        private static AddQuadBeziersFn _origAddQuadBeziers;
        private static AddArcFn _origAddArc;

        // ── Current point tracking ──
        // The subdivision algorithm needs p0 (the current figure point).
        // We track it by hooking all methods that move the current point.

        [ThreadStatic] private static Point2F _currentPoint;

        // ── Constants ──

        /// <summary>
        /// Squared tolerance for cubic-to-quadratic conversion.
        /// 0.25 = 0.5² — half a device-independent pixel.
        /// </summary>
        private const float ToleranceSq = 0.25f;
        private const int MaxDepth = 16;
        private const int QuadBufferSize = 64;

        // ── D2D1 factory P/Invoke ──

        [DllImport("d2d1.dll")]
        private static extern int D2D1CreateFactory(
            int factoryType, [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            IntPtr factoryOptions, out IntPtr factory);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreatePathGeometryFn(IntPtr factory, out IntPtr pathGeometry);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int OpenFn(IntPtr pathGeometry, out IntPtr geometrySink);

        private static readonly Guid IID_ID2D1Factory = new Guid("06152247-6f50-465a-9245-118bfd3b6007");

        // ── Public entry point ──

        [HandleProcessCorruptedStateExceptions]
        public static void Apply()
        {
            Logger.Info("Applying bezier rendering fix (COM vtable hook)...");

            IntPtr sink = IntPtr.Zero;
            IntPtr pathGeometry = IntPtr.Zero;
            IntPtr factory = IntPtr.Zero;

            try
            {
                Logger.Debug("Calling D2D1CreateFactory...");
                int hr = D2D1CreateFactory(0, IID_ID2D1Factory, IntPtr.Zero, out factory);
                if (hr < 0 || factory == IntPtr.Zero)
                {
                    Logger.Error($"D2D1CreateFactory failed: hr=0x{hr:X8} factory=0x{factory.ToInt64():X}");
                    return;
                }
                Logger.Debug($"D2D1CreateFactory succeeded: factory=0x{factory.ToInt64():X}");

                var createPathGeometry = ComHook.GetMethod<CreatePathGeometryFn>(factory, Factory_CreatePathGeometry);
                Logger.Debug("Calling CreatePathGeometry...");
                try
                {
                    hr = createPathGeometry(factory, out pathGeometry);
                }
                catch (Exception cpgEx)
                {
                    Logger.Error($"CreatePathGeometry threw: {cpgEx.GetType().Name}: {cpgEx.Message}");
                    return;
                }
                Logger.Debug($"CreatePathGeometry returned: hr=0x{hr:X8} pathGeometry=0x{pathGeometry.ToInt64():X}");
                if (hr < 0 || pathGeometry == IntPtr.Zero)
                {
                    Logger.Error($"CreatePathGeometry failed: hr=0x{hr:X8}");
                    return;
                }
                Logger.Debug($"CreatePathGeometry succeeded: pathGeometry=0x{pathGeometry.ToInt64():X}");

                var open = ComHook.GetMethod<OpenFn>(pathGeometry, PathGeometry_Open);
                Logger.Debug("Calling PathGeometry::Open...");
                hr = open(pathGeometry, out sink);
                if (hr < 0 || sink == IntPtr.Zero)
                {
                    Logger.Error($"PathGeometry::Open failed: hr=0x{hr:X8}");
                    return;
                }
                Logger.Debug($"PathGeometry::Open succeeded: sink=0x{sink.ToInt64():X}");

                // Read the original AddQuadraticBeziers BEFORE hooking anything
                _origAddQuadBeziers = ComHook.GetMethod<AddQuadBeziersFn>(sink, Sink_AddQuadBeziers);

                // Hook current-point tracking methods
                _origBeginFigure = ComHook.Hook<BeginFigureFn>(sink, Sink_BeginFigure, new BeginFigureFn(OnBeginFigure));
                _origAddLines = ComHook.Hook<AddLinesFn>(sink, Sink_AddLines, new AddLinesFn(OnAddLines));
                _origAddLine = ComHook.Hook<AddLineFn>(sink, Sink_AddLine, new AddLineFn(OnAddLine));
                _origAddQuadBezier = ComHook.Hook<AddQuadBezierFn>(sink, Sink_AddQuadBezier, new AddQuadBezierFn(OnAddQuadBezier));
                _origAddArc = ComHook.Hook<AddArcFn>(sink, Sink_AddArc, new AddArcFn(OnAddArc));

                // Hook the cubic bezier methods (the actual fix)
                _origAddBeziers = ComHook.Hook<AddBeziersFn>(sink, Sink_AddBeziers, new AddBeziersFn(OnAddBeziers));
                _origAddBezier = ComHook.Hook<AddBezierFn>(sink, Sink_AddBezier, new AddBezierFn(OnAddBezier));

                Logger.Info("Bezier rendering fix installed (ID2D1GeometrySink vtable patched)");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to install bezier rendering fix", ex);
            }
            finally
            {
                if (sink != IntPtr.Zero) Marshal.Release(sink);
                if (pathGeometry != IntPtr.Zero) Marshal.Release(pathGeometry);
                if (factory != IntPtr.Zero) Marshal.Release(factory);
            }
        }

        // ── Current-point tracking hooks ──

        private static void OnBeginFigure(IntPtr self, Point2F startPoint, int figureBegin)
        {
            _currentPoint = startPoint;
            _origBeginFigure(self, startPoint, figureBegin);
        }

        private static void OnAddLine(IntPtr self, Point2F point)
        {
            _currentPoint = point;
            _origAddLine(self, point);
        }

        private static unsafe void OnAddLines(IntPtr self, IntPtr points, uint count)
        {
            if (count > 0)
                _currentPoint = ((Point2F*)points)[count - 1];
            _origAddLines(self, points, count);
        }

        private static void OnAddQuadBezier(IntPtr self, ref QuadBezierSegment bezier)
        {
            _currentPoint = bezier.Point2;
            _origAddQuadBezier(self, ref bezier);
        }

        private static unsafe void OnAddArc(IntPtr self, IntPtr arc)
        {
            // D2D1_ARC_SEGMENT starts with D2D1_POINT_2F (the endpoint)
            _currentPoint = *(Point2F*)arc;
            _origAddArc(self, arc);
        }

        // ── Cubic bezier hooks (the actual fix) ──

        private static void OnAddBezier(IntPtr self, ref BezierSegment bezier)
        {
            EmitSubdividedBezier(self, ref bezier);
        }

        private static unsafe void OnAddBeziers(IntPtr self, IntPtr beziers, uint count)
        {
            var ptr = (BezierSegment*)beziers;
            for (uint i = 0; i < count; i++)
                EmitSubdividedBezier(self, ref ptr[i]);
        }

        private static unsafe void EmitSubdividedBezier(IntPtr self, ref BezierSegment cubic)
        {
            var quads = stackalloc QuadBezierSegment[QuadBufferSize];
            int quadCount = 0;

            Subdivide(_currentPoint, cubic, quads, ref quadCount, 0);

            _currentPoint = cubic.Point3;

            // Emit all quadratics via the original (unhooked) AddQuadraticBeziers
            _origAddQuadBeziers(self, (IntPtr)quads, (uint)quadCount);
        }

        // ── Subdivision algorithm ──
        // Error metric: for Q = (3·P1 + 3·P2 − P0 − P3)/4 the max squared error
        // is |D|²/432 where D = P3 − 3·P2 + 3·P1 − P0.

        private static unsafe void Subdivide(Point2F p0, BezierSegment cubic,
            QuadBezierSegment* quads, ref int count, int depth)
        {
            if (depth < MaxDepth)
            {
                float dx = cubic.Point3.X - 3f * cubic.Point2.X + 3f * cubic.Point1.X - p0.X;
                float dy = cubic.Point3.Y - 3f * cubic.Point2.Y + 3f * cubic.Point1.Y - p0.Y;
                float dSq = dx * dx + dy * dy;

                if (dSq > ToleranceSq * 432f)
                {
                    CubicSubdivide(p0, cubic, out var left, out var right, out var mid);
                    Subdivide(p0, left, quads, ref count, depth + 1);
                    Subdivide(mid, right, quads, ref count, depth + 1);
                    return;
                }
            }

            // Error acceptable or max depth — emit one quadratic.
            // Q = (3*P1 + 3*P2 - P0 - P3) / 4
            if (count < QuadBufferSize)
            {
                quads[count].Point1.X = (cubic.Point1.X + cubic.Point2.X) * 0.75f
                    - (p0.X + cubic.Point3.X) * 0.25f;
                quads[count].Point1.Y = (cubic.Point1.Y + cubic.Point2.Y) * 0.75f
                    - (p0.Y + cubic.Point3.Y) * 0.25f;
                quads[count].Point2 = cubic.Point3;
                count++;
            }
        }

        /// <summary>
        /// De Casteljau subdivision at t=0.5.
        /// </summary>
        private static void CubicSubdivide(Point2F p0, BezierSegment b,
            out BezierSegment left, out BezierSegment right, out Point2F mid)
        {
            Point2F q0, q1, q2, r0, r1;

            q0.X = (p0.X + b.Point1.X) * 0.5f;
            q0.Y = (p0.Y + b.Point1.Y) * 0.5f;
            q1.X = (b.Point1.X + b.Point2.X) * 0.5f;
            q1.Y = (b.Point1.Y + b.Point2.Y) * 0.5f;
            q2.X = (b.Point2.X + b.Point3.X) * 0.5f;
            q2.Y = (b.Point2.Y + b.Point3.Y) * 0.5f;

            r0.X = (q0.X + q1.X) * 0.5f;
            r0.Y = (q0.Y + q1.Y) * 0.5f;
            r1.X = (q1.X + q2.X) * 0.5f;
            r1.Y = (q1.Y + q2.Y) * 0.5f;

            mid.X = (r0.X + r1.X) * 0.5f;
            mid.Y = (r0.Y + r1.Y) * 0.5f;

            left.Point1 = q0;
            left.Point2 = r0;
            left.Point3 = mid;

            right.Point1 = r1;
            right.Point2 = q2;
            right.Point3 = b.Point3;
        }
    }
}
