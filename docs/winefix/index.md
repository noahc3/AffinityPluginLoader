# WineFix

WineFix is an APL plugin that patches Wine-specific bugs in Affinity using runtime code patches. It applies [Harmony](https://github.com/pardeike/Harmony) IL transpilers and prefixes to fix issues at the .NET level, and ships a patched `d2d1.dll` to fix native rendering bugs.

## Fixes

- **Preferences fail to save on exit** — Transpiler replaces `HasPreviousPackageInstalled()` with `false`, which otherwise blocks the preferences save path under Wine.
- **Vector path preview lines drawn incorrectly** — Fixed via a patched `d2d1.dll` built from Wine 10.18 source with a cubic bezier subdivision algorithm that approximates cubic beziers using multiple quadratic beziers.
- **Color picker zoom preview shows a black image on Wayland** — Replaces `CopyFromScreen` (which returns black under Wayland) with a `BitBlt` from the canvas window. Auto-detected by default; [configurable](configuration.md).
- **Intermittent startup crash from parallel font enumeration** — Forces synchronous font loading to avoid an access violation in `libkernel.dll` during startup. Enabled by default; [configurable](configuration.md).

!!! warning
    WineFix currently patches out the Canva sign-in dialog prompt. This is temporary and will be restored once there is a consistent fix for the sign-in browser redirect and Affinity protocol handler.

## Color Picker Sampling Modes

The color picker has two sampling modes, configurable in [settings](configuration.md):

- **Native** (default) — Uses Affinity's built-in color sampling pipeline. Colors are sampled in the document's native color space (sRGB, CMYK, wide-gamut, etc.). The highlighted pixel in the zoom preview may differ slightly from the actual sampled color value due to differences in how the zoom preview and the native picker resolve coordinates.
- **Exact** — Picks the literal pixel color shown in the zoom preview center. Samples from a screen capture in sRGB. The picked color always matches what you see in the zoom, but does not use the document's native color space.

Use Native for color-accurate work (especially CMYK or wide-gamut documents). Use Exact if you want the picked color to always match the zoom preview.

<!-- TODO: Add screenshot comparison between Native and Exact modes
![Native mode](../assets/winefix-colorpicker-native.png)
*Native mode: the zoom preview highlight and the sampled color value may differ slightly.*

![Exact mode](../assets/winefix-colorpicker-exact.png)
*Exact mode: the sampled color always matches the highlighted pixel in the zoom preview.*
-->

## Known Open Bugs

These are under investigation and not yet patched:

- Accepting crash reporting causes a permanent crash until preferences are cleared
- Embedded SVG document editor crashes after being open for some time

We are open to resolving any Wine-specific bugs. Feel free to [open an issue](https://github.com/noahc3/AffinityPluginLoader/issues) requesting a patch — just keep in mind these bugs take time to research and develop patches for, especially when native code is involved.

## Licensing

WineFix is licensed under **GPLv2**. The bundled `d2d1.dll` source (under `WineFix/lib/d2d1/`) is licensed under **LGPLv2.1** per upstream Wine licensing.

This is a different license from the rest of APL (MIT). See the [LICENSE](https://github.com/noahc3/AffinityPluginLoader/blob/main/WineFix/LICENSE) file for details.
