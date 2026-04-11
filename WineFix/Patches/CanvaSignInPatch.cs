using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HarmonyLib;
using AffinityPluginLoader.Core;

namespace WineFix.Patches
{
    /// <summary>
    /// Adds a paste-URL textbox to the Canva sign-in dialog so users can manually
    /// paste the authentication redirect URL from their browser, bypassing the need
    /// for the affinity:// protocol handler which doesn't work under Wine.
    /// </summary>
    public static class CanvaSignInPatch
    {
        private static FieldInfo _authHookField;
        private static object _cloudServicesService;

        public static void ApplyPatches(Harmony harmony)
        {
            Logger.Info("Applying CanvaSignIn patch...");

            var serifAffinity = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");
            var serifInterop = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Serif.Interop.Persona");

            if (serifAffinity == null || serifInterop == null)
            {
                Logger.Error("Required assemblies not found");
                return;
            }

            // Resolve the AuthHookReceived backing field on CloudServicesService
            var cssType = serifInterop.GetType("Serif.Interop.Persona.Services.CloudServicesService");
            _authHookField = cssType?.GetField("<backing_store>AuthHookReceived",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (_authHookField == null)
            {
                // Try the mangled name from decompilation
                _authHookField = cssType?.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(f => f.Name.Contains("AuthHookReceived") && f.FieldType == typeof(Action<Uri>));
            }

            if (_authHookField == null)
            {
                Logger.Error("Could not find AuthHookReceived backing field");
                return;
            }

            // Patch ProductKeyDialog_Loaded to inject our UI
            var dialogType = serifAffinity.GetType("Serif.Affinity.UI.ProductKeyDialog");
            var loadedMethod = dialogType?.GetMethod("ProductKeyDialog_Loaded",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (loadedMethod == null)
            {
                Logger.Error("ProductKeyDialog_Loaded not found");
                return;
            }

            harmony.Patch(loadedMethod,
                postfix: new HarmonyMethod(typeof(CanvaSignInPatch), nameof(OnDialogLoaded)));
            Logger.Info("Patched ProductKeyDialog_Loaded");
        }

        public static void OnDialogLoaded(object __instance)
        {
            try
            {
                var window = __instance as Window;
                if (window == null) return;

                // Get CloudServicesService via Application.Current.GetService<T>()
                var app = Application.Current;
                var getServiceMethod = app.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "GetService" && m.IsGenericMethod)
                    .FirstOrDefault();

                if (getServiceMethod != null)
                {
                    var cssType = _authHookField.DeclaringType;
                    var bound = getServiceMethod.MakeGenericMethod(cssType);
                    _cloudServicesService = bound.Invoke(app, null);
                }

                // Listen for DataContext property changes to detect state transitions
                var model = ((FrameworkElement)window).DataContext;
                if (model is System.ComponentModel.INotifyPropertyChanged npc)
                {
                    npc.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == "State")
                            InjectPasteUI(window);
                    };
                }

                // Also try immediately in case we're already in SigningIn
                InjectPasteUI(window);
            }
            catch (Exception ex)
            {
                Logger.Error($"OnDialogLoaded failed: {ex.Message}");
            }
        }

        private static void InjectPasteUI(Window window)
        {
            try
            {
                // Check if current state is SigningIn
                var model = ((FrameworkElement)window).DataContext;
                var stateProp = model.GetType().GetProperty("State");
                if (stateProp == null) return;

                var state = stateProp.GetValue(model);
                if (state.ToString() == "SigningIn")
                {
                    // Defer to allow the template to render
                    window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                        new Action(() => InjectPasteUIImpl(window)));
                }
                else
                {
                    HideOverlay();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"InjectPasteUI failed: {ex.Message}");
            }
        }

        private static Border _overlayPanel;

        private static void InjectPasteUIImpl(Window window)
        {
            try
            {
                // Find the root decorator/grid of the window to overlay on top of everything
                var rootGrid = FindChild<Grid>(window);
                if (rootGrid == null) return;

                // If we already injected the overlay, just make sure it's visible
                if (_overlayPanel != null)
                {
                    _overlayPanel.Visibility = Visibility.Visible;
                    return;
                }

                // Wrap existing window content in a new Grid so we can overlay on top
                var existingContent = window.Content as UIElement;
                if (existingContent == null) return;

                var wrapperGrid = new Grid();
                window.Content = wrapperGrid;
                wrapperGrid.Children.Add(existingContent);

                // Build an overlay panel pinned to the right half of the window
                var panel = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(30)
                };

                var heading = new TextBlock
                {
                    Text = "Running under Wine?",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var instructions = new TextBlock
                {
                    Text = "After signing in, your browser will show a " +
                           "\"Launching Affinity\" page. Copy the full URL " +
                           "from the address bar and paste it below:",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x65, 0x65, 0x65)),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                // Embedded screenshot
                var screenshot = LoadEmbeddedImage("WineFix.Resources.signin.png");
                Image screenshotImage = null;
                if (screenshot != null)
                {
                    screenshotImage = new Image
                    {
                        Source = screenshot,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Margin = new Thickness(0, 0, 0, 12)
                    };
                }

                var textBox = new TextBox
                {
                    Height = 28,
                    FontSize = 12,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                // Placeholder text overlay
                var placeholder = new TextBlock
                {
                    Text = "https://page.service.serif.com/canva-auth-redirect/...",
                    Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                    FontSize = 12,
                    IsHitTestVisible = false,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0)
                };
                var textBoxContainer = new Grid { Height = 28, HorizontalAlignment = HorizontalAlignment.Stretch };
                textBoxContainer.Children.Add(textBox);
                textBoxContainer.Children.Add(placeholder);
                textBox.TextChanged += (s, e) => placeholder.Visibility =
                    string.IsNullOrEmpty(textBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                textBox.GotFocus += (s, e) => placeholder.Visibility = Visibility.Collapsed;
                textBox.LostFocus += (s, e) => placeholder.Visibility =
                    string.IsNullOrEmpty(textBox.Text) ? Visibility.Visible : Visibility.Collapsed;

                var button = new Button
                {
                    Content = "Submit",
                    Margin = new Thickness(0, 8, 0, 0),
                    Padding = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var errorText = new TextBlock
                {
                    Foreground = Brushes.Red,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Visibility = Visibility.Collapsed,
                    Margin = new Thickness(0, 4, 0, 0)
                };

                button.Click += (s, e) => OnSubmitUrl(textBox.Text, errorText);

                panel.Children.Add(heading);
                panel.Children.Add(instructions);
                if (screenshotImage != null)
                    panel.Children.Add(screenshotImage);
                panel.Children.Add(textBoxContainer);
                panel.Children.Add(button);
                panel.Children.Add(errorText);

                // Overlay container: right-aligned, 50% width, full height, white background
                _overlayPanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Child = panel
                };

                // Bind width to 50% of the window
                _overlayPanel.SetBinding(FrameworkElement.WidthProperty,
                    new System.Windows.Data.Binding("ActualWidth")
                    {
                        Source = window,
                        Converter = new HalfWidthConverter()
                    });

                // Add on top of everything
                wrapperGrid.Children.Add(_overlayPanel);

                // Nudge the "Back" button left so it's not covered by the overlay
                var contentControl = FindChild<ContentControl>(existingContent,
                    c => c.ContentTemplateSelector != null);
                if (contentControl != null)
                {
                    var backButton = FindDescendant<Button>(contentControl, b =>
                        Grid.GetRow(b) == 4 && Grid.GetColumnSpan(b) == 3
                        && b.HorizontalAlignment == HorizontalAlignment.Center);
                    if (backButton != null)
                        backButton.HorizontalAlignment = HorizontalAlignment.Left;
                }

                Logger.Info("Injected paste-URL overlay into SigningIn dialog");
            }
            catch (Exception ex)
            {
                Logger.Error($"InjectPasteUIImpl failed: {ex.Message}");
            }
        }

        private static void HideOverlay()
        {
            if (_overlayPanel != null)
                _overlayPanel.Visibility = Visibility.Collapsed;
        }

        private static void OnSubmitUrl(string input, TextBlock errorText)
        {
            try
            {
                errorText.Visibility = Visibility.Collapsed;

                if (string.IsNullOrWhiteSpace(input))
                {
                    ShowError(errorText, "Please paste a URL.");
                    return;
                }

                var affinityUri = ExtractAffinityUri(input.Trim());
                if (affinityUri == null)
                {
                    ShowError(errorText, "Could not find an authentication URL. Please paste the full URL from your browser's address bar after signing in.");
                    return;
                }

                // Fire AuthHookReceived on CloudServicesService
                if (_cloudServicesService == null || _authHookField == null)
                {
                    ShowError(errorText, "Internal error: cloud services not available.");
                    return;
                }

                var handler = _authHookField.GetValue(_cloudServicesService) as Action<Uri>;
                if (handler == null)
                {
                    ShowError(errorText, "Internal error: auth hook not registered.");
                    return;
                }

                Logger.Info($"Invoking AuthHookReceived with: {affinityUri}");
                handler.Invoke(affinityUri);
            }
            catch (Exception ex)
            {
                Logger.Error($"OnSubmitUrl failed: {ex}");
                ShowError(errorText, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts the affinity:// URI from either:
        /// - A direct affinity://canva/authorize?... URL
        /// - A redirect page URL like https://page.service.serif.com/canva-auth-redirect/?url=affinity%3A%2F%2F...
        /// </summary>
        private static Uri ExtractAffinityUri(string input)
        {
            // Direct affinity:// URL
            if (input.StartsWith("affinity://", StringComparison.OrdinalIgnoreCase))
                return new Uri(input);

            // Extract from redirect page URL's "url" query parameter
            try
            {
                var uri = new Uri(input);
                var query = uri.Query;
                if (string.IsNullOrEmpty(query)) return null;

                // Parse query string manually (no System.Web)
                var decoded = ParseQueryParam(query, "url");
                if (decoded != null && decoded.StartsWith("affinity://", StringComparison.OrdinalIgnoreCase))
                    return new Uri(decoded);
            }
            catch { }

            return null;
        }

        private static string ParseQueryParam(string query, string name)
        {
            // Remove leading '?'
            if (query.StartsWith("?")) query = query.Substring(1);

            foreach (var part in query.Split('&'))
            {
                var eq = part.IndexOf('=');
                if (eq < 0) continue;
                var key = Uri.UnescapeDataString(part.Substring(0, eq));
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(part.Substring(eq + 1));
            }
            return null;
        }

        private static void ShowError(TextBlock tb, string msg)
        {
            tb.Text = msg;
            tb.Visibility = Visibility.Visible;
        }

        private static BitmapImage LoadEmbeddedImage(string resourceName)
        {
            try
            {
                var assembly = typeof(CanvaSignInPatch).Assembly;
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load embedded image: {ex.Message}");
                return null;
            }
        }

        private static T FindChild<T>(DependencyObject parent, Func<T, bool> predicate = null)
            where T : DependencyObject
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t && (predicate == null || predicate(t)))
                    return t;
                var result = FindChild(child, predicate);
                if (result != null) return result;
            }
            return null;
        }

        private static T FindAncestor<T>(DependencyObject child, Func<T, bool> predicate)
            where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T t && predicate(t))
                    return t;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private static T FindDescendant<T>(DependencyObject parent, Func<T, bool> predicate)
            where T : DependencyObject
        {
            return FindChild(parent, predicate);
        }
    }

    internal class HalfWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d) return d * 0.5;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
