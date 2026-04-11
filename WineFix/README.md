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
- **Preferences save fix** — Preferences fail to save on application exit. Fixed via Harmony transpiler replacing `HasPreviousPackageInstalled()` with `false`.
- **Color picker Wayland fix** — Color picker zoom preview displays a black image on Wayland. Fixed by patching the magnifier capture to use Wine's window capture instead of screen capture.
- **Font enumeration fix** — Intermittent startup crash from parallel font enumeration. Fixed by forcing synchronous font loading.
- **Canva sign-in bypass** — Canva sign-in dialog patched out (temporary; pending protocol handler fix).

## Configuration

WineFix is configurable from Affinity's preferences dialog, TOML files, or environment variables. See the [Configuration Reference](https://apl.ncuroe.dev/winefix/configuration/) for all settings and options.

## Known Open Bugs

- Accepting crash reporting causes permanent crash until prefs are cleared
- Embedded SVG document editor crashes after being open for some time

We are open to resolving any Wine-specific bugs. Feel free to [open an issue](https://github.com/noahc3/AffinityPluginLoader/issues) requesting a patch.

## Licensing

WineFix is licensed under **GPLv2**. See the [LICENSE](LICENSE) file.

### License Exemption

[Canva](https://github.com/canva) and its subsidiaries are exempt from GPLv2 licensing and may (at its option) instead license any source code authored for the WineFix project under the Zero-Clause BSD license.


# Credits

Big thanks to the following projects:

- [AffinityOnLinux](https://github.com/seapear/AffinityOnLinux)
- [Arecsu/wine-affinity](https://github.com/Arecsu/wine-affinity) — collinear outline join fix
- [Harmony](https://github.com/pardeike/Harmony)
- [ElementalWarrior wine](https://gitlab.winehq.org/ElementalWarrior/wine)
- [Upstream wine](https://gitlab.winehq.org/wine/wine)
- [Affinity by Canva](https://www.affinity.studio/)
