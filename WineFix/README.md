# WineFix

WineFix is an APL plugin which patches bugs encountered when running Affinity on Linux under Wine using runtime code patches.

> [!TIP]
> 📖 **Full documentation at [apl.ncuroe.dev/winefix](https://apl.ncuroe.dev/winefix/)**

## Install

1. [Install APL](https://apl.ncuroe.dev/guide/installation/) first.
2. Download `winefix-vX.X.X.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases) and extract into your Affinity install directory.

For detailed instructions, see the [WineFix Installation Guide](https://apl.ncuroe.dev/winefix/installation/).

## Included Patches

- **Bezier rendering fix** — Cubic Bézier curves render incorrectly under Wine. Fixed by hooking the `ID2D1GeometrySink` COM vtable to subdivide cubic Béziers into quadratics at runtime using adaptive De Casteljau subdivision. Works across all Wine versions.
- **Collinear join fix** — Spike artifacts appear at smooth curve joins after Bézier subdivision. Fixed by patching d2d1.dll in memory to zero the erroneous 25-unit vertex offset for collinear outline joins.
- **Widen stub fix** — Affinity can hang when interacting with stroked SVG vectors because Wine's `ID2D1PathGeometry::Widen` returns `E_NOTIMPL`. Fixed by hooking the vtable to return an empty geometry instead.
- **Bezier split recursion/budget fix** — Affinity hangs on complex vector paths (e.g. embedded SVGs with overlapping Bézier curves) due to unbounded recursion and splitting in Wine's geometry processing. Fixed by detouring the recursion and split functions with guards that cap recursion depth and total splits.
- **Preferences save fix** — Preferences fail to save on application exit. Fixed via Harmony transpiler replacing `HasPreviousPackageInstalled()` with `false`.
- **Color picker Wayland fix** — Color picker zoom preview displays a black image on Wayland. Fixed by patching the magnifier capture to use Wine's window capture instead of screen capture.
- **Font enumeration fix** — Intermittent startup crash from parallel font enumeration. Fixed by forcing synchronous font loading.
- **Canva sign-in helper** — Canva sign-in helper to allow copy/paste of the authorization URL to complete sign-in, no protocol handler required.
- **Command-line file opening fix** — Opening files from the desktop or command line crashes or silently fails due to a missing WinRT type. Fixed by bypassing `ProcessCommandLineArguments` and opening files directly via `IDocumentViewService`.

## Configuration

WineFix is configurable from Affinity's preferences dialog, TOML files, or environment variables. See the [Configuration Reference](https://apl.ncuroe.dev/winefix/configuration/) for all settings and options.

## Known Open Bugs

- Crash reporting acceptance causes permanent crash until prefs cleared

We are open to resolving any Wine-specific bugs. Feel free to [open an issue](https://github.com/noahc3/AffinityPluginLoader/issues) requesting a patch.

## Licensing

WineFix is licensed under **GPLv2**. See the [LICENSE](LICENSE) file.

### License Exemption

[Canva](https://github.com/canva) and its subsidiaries are exempt from GPLv2 licensing and may (at its option) instead license any source code authored for the WineFix project under the Zero-Clause BSD license.


# Credits

Big thanks to the following projects:

- [AffinityOnLinux](https://github.com/seapear/AffinityOnLinux)
- [Arecsu/wine-affinity](https://github.com/Arecsu/wine-affinity) — collinear outline join fix, Widen stub fix, bezier split recursion/budget fix
- [Harmony](https://github.com/pardeike/Harmony)
- [ElementalWarrior wine](https://gitlab.winehq.org/ElementalWarrior/wine)
- [Upstream wine](https://gitlab.winehq.org/wine/wine)
- [Affinity by Canva](https://www.affinity.studio/)
