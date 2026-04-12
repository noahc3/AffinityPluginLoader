# WineFix

WineFix is an APL plugin that patches Wine-specific bugs in Affinity using runtime code patches. It applies [Harmony](https://github.com/pardeike/Harmony) IL transpilers and prefixes to fix issues at the .NET level, and ships a patched `d2d1.dll` to fix native rendering bugs.

## Fixes

- **Canva sign-in under Wine** — The `affinity://` protocol handler doesn't work under Wine without extra configuration on the host, so the browser auth redirect never reaches Affinity. WineFix adds a paste-URL panel to the sign-in dialog: after signing in via the browser, users copy the redirect URL from the "Launching Affinity" page and paste it into the dialog to complete authentication.
- **Preferences fail to save on exit** — Transpiler replaces `HasPreviousPackageInstalled()` with `false`, which otherwise blocks the preferences save path under Wine.
- **Vector path preview lines drawn incorrectly** — Fixed via a patched `d2d1.dll` built from Wine 10.18 source with a cubic bezier subdivision algorithm that approximates cubic beziers using multiple quadratic beziers.
- **Color picker zoom preview shows a black image on Wayland** — Replaces `CopyFromScreen` (which returns black under Wayland) with a `BitBlt` from the canvas window. Auto-detected by default; [configurable](configuration.md).
- **Intermittent startup crash from parallel font enumeration** — Forces synchronous font loading to avoid an access violation in `libkernel.dll` during startup. Enabled by default; [configurable](configuration.md).

## Canva Sign-In

Under Wine, the `affinity://` protocol handler used by Canva's OAuth flow doesn't work — after signing in via the browser, the redirect back to Affinity never arrives. WineFix adds a panel to the right side of the sign-in dialog with instructions and a textbox. After signing in, the browser shows a "Launching Affinity" page. Copy the full URL from the browser's address bar and paste it into the textbox to complete sign-in.

Both URL formats are accepted:

- The full redirect page URL: `https://page.service.serif.com/canva-auth-redirect/?url=affinity%3A%2F%2F...`
- The raw protocol URL: `affinity://canva/authorize?code=...&exchangeId=...`

## Color Picker Sampling Modes

The color picker has two sampling modes, configurable in [settings](configuration.md):

- **Native** (default) — Uses Affinity's built-in color sampling pipeline. Colors are sampled in the document's native color space (sRGB, CMYK, wide-gamut, etc.). The highlighted pixel in the zoom preview may differ slightly from the actual sampled color value due to differences in how the zoom preview and the native picker resolve coordinates.
- **Exact** — Picks the literal pixel color shown in the zoom preview center. Samples from a screen capture in sRGB. The picked color always matches what you see in the zoom, but does not use the document's native color space.

Use Native for color-accurate work (especially CMYK or wide-gamut documents). Use Exact if you want the picked color to always match the zoom preview.

| Native | Exact |
|--------|-------|
| ![Native mode](img/native.png) | ![Exact mode](img/exact.png) |
| The highlighted pixel reads `R:255 G:255 B:255` — the sampled color differs from what's visible in the zoom preview. | The highlighted pixel reads `R:255 G:148 B:148` — the sampled color matches the actual pixel shown in the zoom preview. |

## Known Open Bugs

These are under investigation and not yet patched:

- Accepting crash reporting causes a permanent crash until preferences are cleared
- Embedded SVG document editor crashes after being open for some time

We are open to resolving any Wine-specific bugs. Feel free to [open an issue](https://github.com/noahc3/AffinityPluginLoader/issues) requesting a patch — just keep in mind these bugs take time to research and develop patches for, especially when native code is involved.

## Licensing

WineFix is licensed under **GPLv2**. The bundled `d2d1.dll` source (under `WineFix/lib/d2d1/`) is licensed under **LGPLv2.1** per upstream Wine licensing.

This is a different license from the rest of APL (MIT). See the [LICENSE](https://github.com/noahc3/AffinityPluginLoader/blob/main/WineFix/LICENSE) file for details.
