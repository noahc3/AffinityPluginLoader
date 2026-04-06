using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Interop;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace WineFix.Patches
{
    /// <summary>
    /// Fixes color picker zoom preview on Wayland by replacing CopyFromScreen
    /// (which returns black on Wayland) with a BitBlt from the RenderControl's
    /// native window. Uses a Harmony transpiler so the original SaveAllScreens
    /// flow is preserved — same bitmap lifecycle, same timing, zero per-frame overhead.
    /// </summary>
    public static class ColorPickerWaylandPatch
    {
        // Win32 methods (from Serif.Windows.Win32Methods)
        private static MethodInfo _getDCMethod;
        private static MethodInfo _releaseDCMethod;
        private static MethodInfo _bitBltMethod;
        private static MethodInfo _getWindowRectMethod;

        // Cached reflection
        private static Type _rectType;
        private static FieldInfo _rectLeftField, _rectTopField, _rectRightField, _rectBottomField;
        private static MethodInfo _getServiceGenericMethod;
        private static PropertyInfo _currentViewProperty;
        private static PropertyInfo _renderControlProperty; // lazily cached (needs runtime type)
        private static ConstructorInfo _colourRGBConstructor;

        // State
        private static IntPtr _hwnd = IntPtr.Zero;
        private static bool _pickerActive = false;
        private static bool _useExactPixelColor = false;
        private static double _monitorScale = 1.0;

        public static void ApplyPatches(Harmony harmony)
        {
            Logger.Info("Applying ColorPickerWayland patch...");

            var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");
                var serifWindowsAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Windows");
                var personaAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Interop.Persona");

                if (serifAssembly == null || serifWindowsAssembly == null)
                {
                    Logger.Error("Required assemblies not found");
                    return;
                }

                // Cache Win32Methods
                var win32 = serifWindowsAssembly.GetType("Serif.Windows.Win32Methods");
                _getDCMethod = win32?.GetMethod("GetDC", BindingFlags.Public | BindingFlags.Static);
                _releaseDCMethod = win32?.GetMethod("ReleaseDC", BindingFlags.Public | BindingFlags.Static);
                _bitBltMethod = win32?.GetMethod("BitBlt", BindingFlags.Public | BindingFlags.Static);
                _getWindowRectMethod = win32?.GetMethod("GetWindowRect", BindingFlags.Public | BindingFlags.Static);

                if (_getDCMethod == null || _releaseDCMethod == null || _bitBltMethod == null || _getWindowRectMethod == null)
                {
                    Logger.Error("Win32Methods API methods not found");
                    return;
                }

                // Cache RECT type and fields
                _rectType = serifWindowsAssembly.GetType("Serif.Windows.RECT");
                if (_rectType != null)
                {
                    _rectLeftField = _rectType.GetField("Left");
                    _rectTopField = _rectType.GetField("Top");
                    _rectRightField = _rectType.GetField("Right");
                    _rectBottomField = _rectType.GetField("Bottom");
                }

                // Cache service lookup for RenderControl discovery
                if (personaAssembly != null)
                {
                    var docViewServiceType = personaAssembly.GetType("Serif.Interop.Persona.Services.IDocumentViewService");
                    if (docViewServiceType != null)
                        _currentViewProperty = docViewServiceType.GetProperty("CurrentView");

                    var colourRGBType = personaAssembly.GetType("Serif.Interop.Persona.Colours.ColourRGB");
                    if (colourRGBType != null)
                        _colourRGBConstructor = colourRGBType.GetConstructor(new[] { typeof(double), typeof(double), typeof(double), typeof(double) });

                    var app = System.Windows.Application.Current;
                    if (app != null && docViewServiceType != null)
                    {
                        var gsm = app.GetType().GetMethods()
                            .FirstOrDefault(m => m.Name == "GetService" && m.IsGenericMethod && m.GetParameters().Length == 0);
                        if (gsm != null)
                            _getServiceGenericMethod = gsm.MakeGenericMethod(docViewServiceType);
                    }
                }

                // Transpile SaveAllScreens: replace CopyFromScreen with our CaptureCanvas
                var screenHelperType = serifAssembly.GetType("Serif.Affinity.UI.Controls.ScreenHelper");
                var saveAllScreens = screenHelperType?.GetMethod("SaveAllScreens",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(int), typeof(int), typeof(int), typeof(int) }, null);
                if (saveAllScreens != null)
                {
                    harmony.Patch(saveAllScreens,
                        transpiler: new HarmonyMethod(typeof(ColorPickerWaylandPatch), nameof(SaveAllScreens_Transpiler)));
                    Logger.Info("Transpiled ScreenHelper.SaveAllScreens");
                }

                // Postfix on CreateZoomImage for Exact color mode
                var createZoomImage = screenHelperType?.GetMethod("CreateZoomImage", BindingFlags.Public | BindingFlags.Static);
                if (createZoomImage != null)
                {
                    harmony.Patch(createZoomImage,
                        postfix: new HarmonyMethod(typeof(ColorPickerWaylandPatch), nameof(CreateZoomImage_Postfix)));
                    Logger.Info("Patched ScreenHelper.CreateZoomImage (Exact mode postfix)");
                }

                // Patch StartDragging/FinishDragging to track picker lifecycle and cache HWND
                var magnifierType = serifAssembly.GetType("Serif.Affinity.UI.Controls.ColourPickerMagnifier");
                if (magnifierType != null)
                {
                    var startDragging = magnifierType.GetMethod("StartDragging", BindingFlags.Public | BindingFlags.Instance);
                    if (startDragging != null)
                        harmony.Patch(startDragging, prefix: new HarmonyMethod(typeof(ColorPickerWaylandPatch), nameof(StartDragging_Prefix)));

                    var finishDragging = magnifierType.GetMethod("FinishDragging", BindingFlags.Public | BindingFlags.Instance);
                    if (finishDragging != null)
                        harmony.Patch(finishDragging, prefix: new HarmonyMethod(typeof(ColorPickerWaylandPatch), nameof(FinishDragging_Prefix)));

                    Logger.Info("Patched ColourPickerMagnifier StartDragging/FinishDragging");
                }

                Logger.Info("ColorPickerWayland patch applied successfully");
        }

        // ── Picker lifecycle ──

        public static void StartDragging_Prefix()
        {
            _pickerActive = true;
            _hwnd = IntPtr.Zero;

            try
            {
                var app = System.Windows.Application.Current;
                if (app != null && _getServiceGenericMethod != null)
                {
                    var svc = _getServiceGenericMethod.Invoke(app, null);
                    var view = _currentViewProperty?.GetValue(svc);
                    if (view != null)
                    {
                        if (_renderControlProperty == null)
                            _renderControlProperty = view.GetType().GetProperty("RenderControl");
                        if (_renderControlProperty?.GetValue(view) is HwndHost host)
                            _hwnd = host.Handle;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to cache HWND: {ex.Message}");
            }

            // Cache color picker mode setting
            var store = AffinityPluginLoader.Core.PluginManager.GetSettingsStore(WineFixPlugin.PluginId);
            if (store != null)
            {
                var mode = store.GetEffectiveValue<string>(WineFixPlugin.ColorPickerModeKey);
                _useExactPixelColor = "exact".Equals(mode, StringComparison.OrdinalIgnoreCase);
            }

            // Cache monitor scale
            try
            {
                var pAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Interop.Persona");
                var mcType = pAsm?.GetType("Serif.Interop.Persona.MonitorCollection");
                var inst = mcType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                var gms = inst?.GetType().GetMethod("GetMaximumScale");
                if (gms != null) _monitorScale = (double)gms.Invoke(inst, null);
            }
            catch { }
        }

        public static void FinishDragging_Prefix()
        {
            _pickerActive = false;
        }

        // ── Exact mode: override picked colour with the zoom preview center pixel ──

        /// <summary>
        /// In Exact mode, replaces the picked colour with the actual pixel value at the
        /// center of the zoom bitmap, so the picked colour always matches the preview.
        /// </summary>
        public static void CreateZoomImage_Postfix(Bitmap bmpSrc, System.Windows.Point posIn,
            System.Windows.Size size, ref object col)
        {
            if (!_useExactPixelColor || bmpSrc == null || _colourRGBConstructor == null)
                return;

            try
            {
                int bmpX = (int)(posIn.X - System.Windows.SystemParameters.VirtualScreenLeft * _monitorScale);
                int bmpY = (int)(posIn.Y - System.Windows.SystemParameters.VirtualScreenTop * _monitorScale);

                if (bmpX >= 0 && bmpX < bmpSrc.Width && bmpY >= 0 && bmpY < bmpSrc.Height)
                {
                    System.Drawing.Color px = bmpSrc.GetPixel(bmpX, bmpY);
                    col = _colourRGBConstructor.Invoke(new object[] {
                        px.R / 255.0, px.G / 255.0, px.B / 255.0, px.A / 255.0
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Exact color mode error: {ex.Message}");
            }
        }

        // ── Transpiler ──

        /// <summary>
        /// Replaces the CopyFromScreen call in SaveAllScreens with our CaptureCanvas,
        /// which has the same signature so the IL stack is consumed correctly.
        /// </summary>
        public static IEnumerable<CodeInstruction> SaveAllScreens_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var copyFromScreen = typeof(Graphics).GetMethod("CopyFromScreen",
                new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(System.Drawing.Size) });

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(copyFromScreen))
                {
                    codes[i] = new CodeInstruction(OpCodes.Call,
                        typeof(ColorPickerWaylandPatch).GetMethod(nameof(CaptureCanvas), BindingFlags.Public | BindingFlags.Static));
                    Logger.Info("Transpiler: replaced CopyFromScreen with CaptureCanvas");
                    break;
                }
            }

            return codes;
        }

        // ── Capture ──

        /// <summary>
        /// Drop-in replacement for Graphics.CopyFromScreen (same signature).
        /// When the picker is active, BitBlts the RenderControl canvas.
        /// Otherwise falls back to the original CopyFromScreen.
        /// </summary>
        public static void CaptureCanvas(Graphics graphics, int screenLeft, int screenTop,
            int dstX, int dstY, System.Drawing.Size size)
        {
            if (!_pickerActive || _hwnd == IntPtr.Zero)
            {
                graphics.CopyFromScreen(screenLeft, screenTop, dstX, dstY, size);
                return;
            }

            try
            {
                object rect = Activator.CreateInstance(_rectType);
                object[] rectArgs = { _hwnd, rect };
                _getWindowRectMethod.Invoke(null, rectArgs);
                rect = rectArgs[1];
                int winLeft = (int)_rectLeftField.GetValue(rect);
                int winTop = (int)_rectTopField.GetValue(rect);
                int winW = (int)_rectRightField.GetValue(rect) - winLeft;
                int winH = (int)_rectBottomField.GetValue(rect) - winTop;

                int canvasDestX = winLeft - screenLeft;
                int canvasDestY = winTop - screenTop;

                IntPtr hdcDst = graphics.GetHdc();
                try
                {
                    IntPtr hdcSrc = (IntPtr)_getDCMethod.Invoke(null, new object[] { _hwnd });
                    _bitBltMethod.Invoke(null, new object[] {
                        hdcDst, canvasDestX, canvasDestY, winW, winH,
                        hdcSrc, 0, 0, 0x00CC0020
                    });
                    _releaseDCMethod.Invoke(null, new object[] { _hwnd, hdcSrc });
                }
                finally
                {
                    graphics.ReleaseHdc(hdcDst);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"CaptureCanvas error: {ex.Message}");
            }
        }
    }
}
