# WineFix

WineFix is an APL plugin that patches Wine-specific bugs in Affinity using runtime code patches. It applies [Harmony](https://github.com/pardeike/Harmony) IL transpilers for .NET-level fixes, and uses APL's [native code APIs](../dev/native-apis.md) (COM vtable hooking and in-memory patching) for native rendering fixes.

## Fixes

## Canva Sign-In Helper

Under Wine, the `affinity://` protocol handler used by Canva's OAuth flow doesn't work without extra configuration on the host, meaning after signing in via the browser the redirect back to Affinity never arrives. WineFix adds a panel to the right side of the sign-in dialog with instructions and a URL inpiut. After signing in, the browser shows a "Launching Affinity" page. Copy the full URL from the browser's address bar and paste it into the textbox to complete sign-in.

Both URL formats are accepted:

- The full redirect page URL: `https://page.service.serif.com/canva-auth-redirect/?url=affinity%3A%2F%2F...`
- The raw protocol URL: `affinity://canva/authorize?code=...&exchangeId=...`

### Bezier rendering fix

Wine's Direct2D implementation approximates every cubic Bézier with a single quadratic, which produces visible distortion on curves with high curvature or inflection points. WineFix hooks the `ID2D1GeometrySink` COM vtable at runtime to intercept cubic Bézier calls (`AddBezier`, `AddBeziers`) and replaces them with adaptive cubic-to-quadratic subdivision using De Casteljau's algorithm. The resulting quadratic segments are emitted via `AddQuadraticBeziers`, which Wine renders correctly.

This approach works across all Wine versions (7.9 through 11.6+ tested) because COM vtable layout is defined by the interface ABI and never changes between implementations.

### Collinear outline join fix

When two adjacent outline segments are collinear, Wine's `d2d_geometry_outline_add_join` unconditionally places join vertices 25 units away from the join point. This is correct for hairpin reversals but produces visible spike artifacts on smooth curve continuations from Bézier subdivision. WineFix patches d2d1.dll in memory to zero this offset, making collinear joins flat.

The patch is applied by scanning d2d1.dll's `.text` section for the `movss xmm0, [25.0f]` instruction and replacing it with `xorps xmm0, xmm0` (0.0f). Based on a [Wine patch by Arecsu](https://github.com/Arecsu/wine-affinity).

### Widen stub fix

Wine's `ID2D1PathGeometry::Widen` returns `E_NOTIMPL`, which can cause Affinity to hang indefinitely when interacting with stroked path geometries (e.g. stroked SVG vectors). WineFix hooks the `Widen` vtable entry via `ComHook` to return `S_OK` with an empty closed geometry sink instead. Stroke rendering will be absent but the application remains usable. Based on a [Wine patch by Arecsu](https://github.com/Arecsu/wine-affinity).

### Bezier split recursion and budget fix

Wine's geometry processing code can enter unbounded recursion or unbounded splitting loops on complex or pathological vector paths (e.g. overlapping Bézier curves in embedded SVGs), causing Affinity to hang. WineFix applies two guards using APL's `NativeHook` API:

- **Recursion guard:** Detours `d2d_geometry_intersect_bezier_bezier` to return early when Bézier parameter ranges shrink below 1e-6, preventing infinite recursion on overlapping or collinear Béziers.
- **Split budget:** Detours `d2d_geometry_split_bezier` with a thread-local call counter that caps splits at 512 per geometry sink `Close` operation, preventing unbounded segment growth.

Both functions are found by scanning d2d1.dll's `.text` section for their unique prologue byte patterns (verified across ElementalWarrior Wine 7.9, Wine Staging 11.5, and TKG Staging 11.6). Based on a [Wine patch by Arecsu](https://github.com/Arecsu/wine-affinity).

### Preferences save fix

Preferences fail to save on application exit under Wine. A Harmony transpiler replaces the call to `HasPreviousPackageInstalled()` with `false`, which otherwise throws an exception that blocks the preferences save path.

### Color picker Wayland fix

The color picker zoom preview displays a black image on Wayland because `CopyFromScreen` returns black. WineFix replaces it with a `BitBlt` from the canvas window. Auto-detected by default; [configurable](configuration.md).

## Color Picker Sampling Modes

The color picker has two sampling modes, configurable in [settings](configuration.md):

- **Native** (default) — Uses Affinity's built-in color sampling pipeline. Colors are sampled in the document's native color space (sRGB, CMYK, wide-gamut, etc.). The highlighted pixel in the zoom preview may differ slightly from the actual sampled color value due to differences in how the zoom preview and the native picker resolve coordinates.
- **Exact** — Picks the literal pixel color shown in the zoom preview center. Samples from a screen capture in sRGB. The picked color always matches what you see in the zoom, but does not use the document's native color space.

Use Native for color-accurate work (especially CMYK or wide-gamut documents). Use Exact if you want the picked color to always match the zoom preview.

| Native | Exact |
|--------|-------|
| ![Native mode](img/native.png) | ![Exact mode](img/exact.png) |
| The highlighted pixel reads `R:255 G:255 B:255` — the sampled color differs from what's visible in the zoom preview. | The highlighted pixel reads `R:255 G:148 B:148` — the sampled color matches the actual pixel shown in the zoom preview. |

### Font enumeration fix

Intermittent startup crash from parallel font enumeration in `libkernel.dll`. Forces synchronous font loading. Enabled by default; [configurable](configuration.md).

## Known Open Bugs

These are under investigation and not yet patched:

- Crash reporting acceptance causes permanent crash until prefs cleared

We are open to resolving any Wine-specific bugs. Feel free to [open an issue](https://github.com/noahc3/AffinityPluginLoader/issues) requesting a patch — just keep in mind these bugs take time to research and develop patches for, especially when native code is involved.

## Licensing

WineFix is licensed under **GPLv2**. See the [LICENSE](https://github.com/noahc3/AffinityPluginLoader/blob/main/WineFix/LICENSE) file for details.
