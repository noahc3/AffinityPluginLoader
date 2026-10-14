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
        private static FieldInfo _screenBmpField;
        private static FieldInfo _dragdropWindowField;
        private static MethodInfo _showColourPickerMethod;
        private static MethodInfo _onFinishDraggingMethod;

        // State
        private static IntPtr _hwnd = IntPtr.Zero;
        private static bool _pickerActive = false;
        private static bool _useExactPixelColor = false;
        private static double _monitorScale = 1.0;
        private static int _dragStartTicks;
        private static int _recaptureIndex;
        private static object _pickerService;   // the UserInterfaceService showing the picker, if any
        private static object _activeMagnifier; // the ColourPickerMagnifier between StartDragging and FinishDragging
        private static bool _appInactive;

        // When to take the canvas capture again, in ms after StartDragging; see UpdateWindowLocation_Prefix.
        private static readonly int[] RecaptureAfterMs = { 50, 150, 300, 600 };

        private const int SRCCOPY = 0x00CC0020;

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

                _onFinishDraggingMethod = magnifierType.GetMethod("OnFinishDragging", BindingFlags.Public | BindingFlags.Instance);

                var finishDragging = magnifierType.GetMethod("FinishDragging", BindingFlags.Public | BindingFlags.Instance);
                if (finishDragging != null)
                    harmony.Patch(finishDragging, prefix: new HarmonyMethod(typeof(ColorPickerWaylandPatch), nameof(FinishDragging_Prefix)));

                Logger.Info("Patched ColourPickerMagnifier StartDragging/FinishDragging");

                // Stop a timer that outlives its window instead of letting it throw
                var timerTick = magnifierType.GetMethod("timer_Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                _dragdropWindowField = magnifierType.GetField("_dragdropWindow", BindingFlags.NonPublic | BindingFlags.Instance);
                if (timerTick != null && _dragdropWindowField != null)
                {
                    harmony.Patch(timerTick, prefix: new HarmonyMethod(typeof(ColorPickerWaylandPatch), nameof(TimerTick_Prefix)));
                    Logger.Info("Patched ColourPickerMagnifier timer_Tick (stop a timer whose window is gone)");
                }

                var updateWindowLocation = magnifierType.GetMethod("UpdateWindowLocation", BindingFlags.NonPublic | BindingFlags.Instance);
                _screenBmpField = magnifierType.GetField("_screenBmp", BindingFlags.NonPublic | BindingFlags.Instance);
                if (updateWindowLocation != null && _screenBmpField != null)
                {
                    harmony.Patch(updateWindowLocation, prefix: new HarmonyMethod(typeof(ColorPickerWaylandPatch), nameof(UpdateWindowLocation_Prefix)));
                    Logger.Info("Patched ColourPickerMagnifier UpdateWindowLocation (recapture after the overlay is hidden)");
                }
            }

            // End the pick when Affinity loses focus
            var uiServiceType = serifAssembly.GetType("Serif.Affinity.Services.UserInterfaceService");
            _showColourPickerMethod = uiServiceType?.GetMethod("ShowColourPicker", BindingFlags.Public | BindingFlags.Instance);
            var application = System.Windows.Application.Current;
            if (_showColourPickerMethod != null && application != null)
            {
                harmony.Patch(_showColourPickerMethod,
                    prefix: new HarmonyMethod(typeof(ColorPickerWaylandPatch), nameof(ShowColourPicker_Prefix)));
                application.Deactivated += OnApplicationDeactivated;
                application.Activated += OnApplicationActivated;
                Logger.Info("Patched UserInterfaceService.ShowColourPicker (end the pick when Affinity loses focus)");
            }

            Logger.Info("ColorPickerWayland patch applied successfully");
        }

        // ── Picker lifecycle ──

        public static void StartDragging_Prefix(object __instance)
        {
            _pickerActive = true;
            _activeMagnifier = __instance;
            _hwnd = IntPtr.Zero;
            _dragStartTicks = Environment.TickCount;
            _recaptureIndex = 0;

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
            catch (Exception ex) { Logger.Debug($"Failed to cache monitor scale: {ex.Message}"); }
        }

        public static void FinishDragging_Prefix()
        {
            _pickerActive = false;
            _activeMagnifier = null;
        }

        // ── Ending the pick when Affinity loses focus ──

        /// <summary>
        /// The magnifier is a topmost window, and under Wine switching to another application
        /// does not end the pick, so it stayed above that application's windows. End it as a
        /// canceled pick when Affinity is deactivated. The tool ends a pick by calling
        /// ShowColourPicker(false); do the same. While Affinity is inactive the tool still sees the
        /// mouse over the canvas and would show the magnifier again, so requests to show it are
        /// ignored until Affinity is active again; the tool shows a new one when it is next used.
        /// A drag from a picker button (the Colors panel) ends on mouse up or lost capture, and
        /// both happen elsewhere; end it through the magnifier's OnFinishDragging, which tells the
        /// button to release its capture.
        ///
        /// The end is deferred: WM_ACTIVATEAPP is a sent message and can arrive inside the
        /// magnifier's own timer tick (its SetWindowPos call), and ending the pick there destroys
        /// the window the rest of that tick still uses.
        /// </summary>
        public static bool ShowColourPicker_Prefix(object __instance, bool isShowing, ref bool __result)
        {
            if (isShowing && _appInactive)
            {
                __result = true;
                return false;
            }
            _pickerService = isShowing ? __instance : null;
            return true;
        }

        private static void OnApplicationDeactivated(object sender, EventArgs e)
        {
            _appInactive = true;
            var service = _pickerService;
            var magnifier = _activeMagnifier;
            if (service == null && magnifier == null)
                return;
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal, new Action(() => EndPick(service, magnifier)));
        }

        private static void EndPick(object service, object magnifier)
        {
            if (!_appInactive)
                return; // Affinity is active again
            try
            {
                if (service != null && service == _pickerService)
                    _showColourPickerMethod.Invoke(service, new object[] { false, false, false, 0, true, false });
                else if (magnifier != null && magnifier == _activeMagnifier && _onFinishDraggingMethod != null)
                    _onFinishDraggingMethod.Invoke(magnifier, new object[] { true });
                else
                    return;
                Logger.Info("Color picker ended: Affinity lost focus");
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to end the color picker: {ex.Message}");
            }
        }

        private static void OnApplicationActivated(object sender, EventArgs e)
        {
            _appInactive = false;
        }

        /// <summary>
        /// timer_Tick moves the magnifier window and then reads it; once the pick has ended and the
        /// window is gone, a tick still delivered throws a NullReferenceException that takes
        /// Affinity down. Stop such a timer and skip the tick.
        /// </summary>
        public static bool TimerTick_Prefix(object __instance, object sender)
        {
            if (_dragdropWindowField.GetValue(__instance) != null)
                return true;
            (sender as System.Windows.Threading.DispatcherTimer)?.Stop();
            Logger.Debug("Stopped a color picker timer whose window is gone");
            return false;
        }

        /// <summary>
        /// StartDragging hides the tool overlay (selection handles, outlines) and the first tick
        /// captures the canvas, which is then kept for the whole drag. Under Wine the canvas is
        /// usually not yet redrawn without the overlay at that point, so the zoom preview showed
        /// the handles while the picked color came from the document under them. Drop the capture
        /// a few times early in the drag, so the next tick takes it again from the redrawn canvas.
        /// </summary>
        public static void UpdateWindowLocation_Prefix(object __instance)
        {
            if (!_pickerActive || _recaptureIndex >= RecaptureAfterMs.Length)
                return;

            int elapsed = Environment.TickCount - _dragStartTicks;
            if (elapsed < RecaptureAfterMs[_recaptureIndex])
                return;
            while (_recaptureIndex < RecaptureAfterMs.Length && elapsed >= RecaptureAfterMs[_recaptureIndex])
                _recaptureIndex++;

            if (_screenBmpField.GetValue(__instance) is Bitmap old)
            {
                _screenBmpField.SetValue(__instance, null);
                old.Dispose();
            }
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
                        hdcSrc, 0, 0, SRCCOPY
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
