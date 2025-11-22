using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace WineFix.Patches
{
    /// <summary>
    /// Fixes color picker zoom preview on Wayland by capturing the DocumentView/canvas instead of the full screen.
    /// On Wayland, CopyFromScreen doesn't work properly, resulting in a black preview.
    /// This patch captures the RenderControl's native window (HwndHost) using Win32 BitBlt.
    /// </summary>
    public static class ColorPickerWaylandPatch
    {
        private static Type _documentViewServiceType;
        private static Type _screenHelperType;
        private static Type _win32MethodsType;
        private static Type _rectType;
        private static MethodInfo _getDCMethod;
        private static MethodInfo _releaseDCMethod;
        private static MethodInfo _bitBltMethod;
        private static MethodInfo _getWindowRectMethod;
        private static bool _useExactPixelColor = false;

        public static void ApplyPatches(Harmony harmony)
        {
            try
            {
                Logger.Info($"Applying ColorPickerWayland patch...");

                // Check environment variable for color picker mode
                string pickerMode = Environment.GetEnvironmentVariable("APL_PICKER_VALUE");
                if (!string.IsNullOrEmpty(pickerMode))
                {
                    _useExactPixelColor = pickerMode.Equals("EXACT", StringComparison.OrdinalIgnoreCase);
                    Logger.Info($"APL_PICKER_VALUE={pickerMode}, using {(_useExactPixelColor ? "EXACT" : "NATIVE")} color mode");
                }
                else
                {
                    Logger.Info($"APL_PICKER_VALUE not set, using NATIVE color mode (default)");
                }

                // Find the Serif.Affinity assembly
                var serifAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

                if (serifAssembly == null)
                {
                    Logger.Error($"ERROR: Serif.Affinity assembly not found");
                    return;
                }

                // Get the ScreenHelper type
                _screenHelperType = serifAssembly.GetType("Serif.Affinity.UI.Controls.ScreenHelper");
                if (_screenHelperType == null)
                {
                    Logger.Error($"ERROR: ScreenHelper type not found");
                    return;
                }

                // Get Win32Methods type for Win32 API calls (in Serif.Windows assembly)
                var serifWindowsAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Windows");
                if (serifWindowsAssembly == null)
                {
                    Logger.Error($"ERROR: Serif.Windows assembly not found");
                    return;
                }

                _win32MethodsType = serifWindowsAssembly.GetType("Serif.Windows.Win32Methods");
                if (_win32MethodsType == null)
                {
                    Logger.Error($"ERROR: Win32Methods type not found");
                    return;
                }

                _getDCMethod = _win32MethodsType.GetMethod("GetDC", BindingFlags.Public | BindingFlags.Static);
                _releaseDCMethod = _win32MethodsType.GetMethod("ReleaseDC", BindingFlags.Public | BindingFlags.Static);
                _bitBltMethod = _win32MethodsType.GetMethod("BitBlt", BindingFlags.Public | BindingFlags.Static);
                _getWindowRectMethod = _win32MethodsType.GetMethod("GetWindowRect", BindingFlags.Public | BindingFlags.Static);

                if (_getDCMethod == null || _releaseDCMethod == null || _bitBltMethod == null || _getWindowRectMethod == null)
                {
                    Logger.Error($"ERROR: Win32Methods API methods not found");
                    return;
                }

                // Get RECT type
                _rectType = serifWindowsAssembly.GetType("Serif.Windows.RECT");
                if (_rectType == null)
                {
                    Logger.Error($"ERROR: RECT type not found");
                    return;
                }

                // Get IDocumentViewService type
                var personaAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Serif.Interop.Persona");
                if (personaAssembly != null)
                {
                    _documentViewServiceType = personaAssembly.GetType("Serif.Interop.Persona.Services.IDocumentViewService");
                }

                // Patch SaveAllScreens(int, int, int, int) method
                var saveAllScreensMethod = _screenHelperType.GetMethod("SaveAllScreens",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) },
                    null);

                if (saveAllScreensMethod != null)
                {
                    var prefix = typeof(ColorPickerWaylandPatch).GetMethod(nameof(SaveAllScreens_Prefix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(saveAllScreensMethod, prefix: new HarmonyMethod(prefix));
                    Logger.Info($"Patched ScreenHelper.SaveAllScreens to capture canvas instead of screen");
                }
                else
                {
                    Logger.Error($"ERROR: SaveAllScreens method not found");
                }

                // Patch CreateZoomImage to override color in EXACT mode
                var createZoomImageMethod = _screenHelperType.GetMethod("CreateZoomImage",
                    BindingFlags.Public | BindingFlags.Static);

                if (createZoomImageMethod != null)
                {
                    var postfix = typeof(ColorPickerWaylandPatch).GetMethod(nameof(CreateZoomImage_Postfix),
                        BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(createZoomImageMethod, postfix: new HarmonyMethod(postfix));
                    Logger.Info($"Patched ScreenHelper.CreateZoomImage for EXACT color mode support");
                }
                else
                {
                    Logger.Warning($"WARNING: CreateZoomImage method not found");
                }

                // Patch ColourPickerMagnifier.UpdateWindowLocation to force bitmap refresh
                var colourPickerMagnifierType = serifAssembly.GetType("Serif.Affinity.UI.Controls.ColourPickerMagnifier");
                if (colourPickerMagnifierType != null)
                {
                    var updateWindowLocationMethod = colourPickerMagnifierType.GetMethod("UpdateWindowLocation",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (updateWindowLocationMethod != null)
                    {
                        var prefix = typeof(ColorPickerWaylandPatch).GetMethod(nameof(UpdateWindowLocation_Prefix),
                            BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(updateWindowLocationMethod, prefix: new HarmonyMethod(prefix));
                        Logger.Info($"Patched ColourPickerMagnifier.UpdateWindowLocation to refresh bitmap");
                    }
                    else
                    {
                        Logger.Warning($"WARNING: UpdateWindowLocation method not found");
                    }
                }
                else
                {
                    Logger.Warning($"WARNING: ColourPickerMagnifier type not found");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply ColorPickerWayland patch", ex);
            }
        }

        private static int _frameCounter = 0;
        private const int REFRESH_INTERVAL = 10;

        // Postfix to override color with exact pixel value in EXACT mode
        public static void CreateZoomImage_Postfix(Bitmap bmpSrc, System.Windows.Point posIn, System.Windows.Size size, ref object col)
        {
            if (!_useExactPixelColor || bmpSrc == null)
            {
                return; // NATIVE mode or no bitmap - use original behavior
            }

            try
            {
                // Calculate the center pixel position in the bitmap
                // posIn is in screen coordinates, need to convert to bitmap coordinates
                var screenLeft = SystemParameters.VirtualScreenLeft;
                var screenTop = SystemParameters.VirtualScreenTop;

                // Offset by virtual screen position
                int bitmapX = (int)(posIn.X - screenLeft);
                int bitmapY = (int)(posIn.Y - screenTop);

                // Bounds check
                if (bitmapX >= 0 && bitmapX < bmpSrc.Width && bitmapY >= 0 && bitmapY < bmpSrc.Height)
                {
                    // Get the exact pixel color from the bitmap
                    System.Drawing.Color pixelColor = bmpSrc.GetPixel(bitmapX, bitmapY);

                    // Convert to Colour type
                    // Need to find the ColourRGB type and create an instance
                    var personaAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "Serif.Interop.Persona");

                    if (personaAssembly != null)
                    {
                        var colourRGBType = personaAssembly.GetType("Serif.Interop.Persona.Colours.ColourRGB");
                        if (colourRGBType != null)
                        {
                            // Create ColourRGB(double r, double g, double b, double a)
                            var constructor = colourRGBType.GetConstructor(new Type[] { typeof(double), typeof(double), typeof(double), typeof(double) });
                            if (constructor != null)
                            {
                                col = constructor.Invoke(new object[] {
                                    pixelColor.R / 255.0,
                                    pixelColor.G / 255.0,
                                    pixelColor.B / 255.0,
                                    pixelColor.A / 255.0
                                });

                                Logger.Debug($"EXACT mode: Picked color from pixel ({bitmapX}, {bitmapY}): R={pixelColor.R}, G={pixelColor.G}, B={pixelColor.B}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error in EXACT color mode: {ex.Message}");
            }
        }

        // Prefix to periodically refresh bitmap (not every frame for performance)
        public static void UpdateWindowLocation_Prefix(object __instance)
        {
            try
            {
                _frameCounter++;

                // Only recapture every N frames to improve performance
                if (_frameCounter >= REFRESH_INTERVAL)
                {
                    _frameCounter = 0;

                    // Clear the cached _screenBmp so it gets recaptured
                    var field = __instance.GetType().GetField("_screenBmp", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(__instance, null);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error clearing cached bitmap: {ex.Message}");
            }
        }

        private static bool _loggedCaptureInfo = false;

        // Prefix that replaces the screen capture with canvas/DocumentView capture
        public static bool SaveAllScreens_Prefix(int screenLeft, int screenTop, int screenWidth, int screenHeight, ref Bitmap __result)
        {
            try
            {
                // Only log capture info once to reduce spam
                if (!_loggedCaptureInfo)
                {
                    Logger.Debug($"SaveAllScreens_Prefix: Capturing canvas (virtual screen: {screenLeft}, {screenTop}, {screenWidth}x{screenHeight})");
                }

                // Get the DocumentView from the application
                var affinityApp = System.Windows.Application.Current;
                if (affinityApp == null)
                {
                    Logger.Warning("Application.Current is null");
                    return true;
                }

                // Get the DocumentViewService and current view
                object documentView = null;

                try
                {
                    // Get the service using reflection
                    // Look for the generic GetService<T>() method (no parameters)
                    var getServiceMethod = affinityApp.GetType().GetMethods()
                        .FirstOrDefault(m => m.Name == "GetService"
                            && m.IsGenericMethod
                            && m.GetParameters().Length == 0);

                    if (getServiceMethod != null && _documentViewServiceType != null)
                    {
                        var genericMethod = getServiceMethod.MakeGenericMethod(_documentViewServiceType);
                        var documentViewService = genericMethod.Invoke(affinityApp, null);

                        if (documentViewService != null)
                        {
                            var viewsProperty = _documentViewServiceType.GetProperty("CurrentView");
                            documentView = viewsProperty?.GetValue(documentViewService);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!_loggedCaptureInfo)
                    {
                        Logger.Debug($"Could not get DocumentView: {ex.Message}");
                    }
                }

                // Try to get the RenderControl's window handle
                IntPtr renderControlHwnd = IntPtr.Zero;
                System.Windows.Point controlPosition = new System.Windows.Point(0, 0);
                double controlWidth = 0;
                double controlHeight = 0;

                if (documentView != null)
                {
                    // Try to get the RenderControl from DocumentView
                    var renderControlProp = documentView.GetType().GetProperty("RenderControl");

                    if (renderControlProp != null)
                    {
                        var renderControl = renderControlProp.GetValue(documentView);

                        // RenderControl is an HwndHost, so we can get its Handle
                        if (renderControl is HwndHost hwndHost)
                        {
                            renderControlHwnd = hwndHost.Handle;

                            // Get position and size using Win32 GetWindowRect for accurate positioning
                            try
                            {
                                // Use GetWindowRect to get the actual window rectangle
                                object rect = Activator.CreateInstance(_rectType);
                                object[] args = new object[] { renderControlHwnd, rect };
                                _getWindowRectMethod.Invoke(null, args);
                                rect = args[1]; // GetWindowRect uses out parameter

                                // Extract Left, Top, Right, Bottom from RECT
                                var leftField = _rectType.GetField("Left");
                                var topField = _rectType.GetField("Top");
                                var rightField = _rectType.GetField("Right");
                                var bottomField = _rectType.GetField("Bottom");

                                int left = (int)leftField.GetValue(rect);
                                int top = (int)topField.GetValue(rect);
                                int right = (int)rightField.GetValue(rect);
                                int bottom = (int)bottomField.GetValue(rect);

                                controlPosition = new System.Windows.Point(left, top);
                                controlWidth = right - left;
                                controlHeight = bottom - top;

                                if (!_loggedCaptureInfo)
                                {
                                    Logger.Debug($"RenderControl HWND: {renderControlHwnd}");
                                    Logger.Debug($"RenderControl RECT: left={left}, top={top}, right={right}, bottom={bottom}");
                                    Logger.Debug($"RenderControl position: {controlPosition}, size: {controlWidth}x{controlHeight}");
                                }
                            }
                            catch (Exception ex)
                            {
                                if (!_loggedCaptureInfo)
                                {
                                    Logger.Debug($"Error getting control position: {ex.Message}");
                                }
                            }
                        }
                    }
                }

                // If we didn't get a RenderControl, fall back to original implementation
                if (renderControlHwnd == IntPtr.Zero)
                {
                    Logger.Warning("Could not get RenderControl HWND, falling back");
                    return true;
                }

                // Capture the RenderControl window using BitBlt
                Bitmap bitmap = new Bitmap(screenWidth, screenHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    // Fill with black background
                    graphics.Clear(System.Drawing.Color.Black);

                    IntPtr hdcDest = graphics.GetHdc();
                    try
                    {
                        // Get the device context for the RenderControl window
                        IntPtr hdcSrc = (IntPtr)_getDCMethod.Invoke(null, new object[] { renderControlHwnd });

                        // Calculate where to draw the control in the virtual screen bitmap
                        int destX = (int)(controlPosition.X - screenLeft);
                        int destY = (int)(controlPosition.Y - screenTop);

                        if (!_loggedCaptureInfo)
                        {
                            Logger.Debug($"Capturing from HWND {renderControlHwnd} to position ({destX}, {destY})");
                            Logger.Debug($"Virtual screen: left={screenLeft}, top={screenTop}, size={screenWidth}x{screenHeight}");
                            Logger.Debug($"Control in bitmap: from ({destX},{destY}) to ({destX + (int)controlWidth},{destY + (int)controlHeight})");
                        }

                        // Copy window contents to bitmap at the correct position using BitBlt
                        // SRCCOPY = 0x00CC0020
                        bool success = (bool)_bitBltMethod.Invoke(null, new object[] {
                            hdcDest, destX, destY, (int)controlWidth, (int)controlHeight,
                            hdcSrc, 0, 0, 0x00CC0020
                        });

                        if (!_loggedCaptureInfo)
                        {
                            Logger.Debug($"BitBlt result: {success}");
                            _loggedCaptureInfo = true; // Only log detailed info once
                        }

                        // Release the window DC
                        _releaseDCMethod.Invoke(null, new object[] { renderControlHwnd, hdcSrc });
                    }
                    finally
                    {
                        graphics.ReleaseHdc(hdcDest);
                    }
                }

                __result = bitmap;
                return false; // Skip original method
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in SaveAllScreens_Prefix: {ex.Message}", ex);
                return true;
            }
        }

    }
}
