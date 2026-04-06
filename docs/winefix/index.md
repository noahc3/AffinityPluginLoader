# WineFix

WineFix is an APL plugin that fixes Wine-specific bugs in Affinity using Harmony IL transpilers.

## Current Fixes

- **Preferences fail to save on exit** — Transpiler replaces `HasPreviousPackageInstalled()` call with `false`
- **Vector path preview lines drawn incorrectly** — Fixed via a patched `d2d1.dll` (standalone Wine build with cubic bezier subdivision)
- **Canva sign-in dialog** — Patched out temporarily (pending fix for browser redirect/protocol handler)

## Known Open Bugs

These are under investigation and not yet patched:

- Color picker zoom image broken on Wayland
- Crash reporting acceptance causes permanent crash until prefs cleared
- Embedded SVG document editor crashes after being open for some time

## Licensing

WineFix is licensed under **GPLv2**. The bundled `d2d1.dll` is licensed under **LGPLv2.1** (per upstream Wine licensing).

This is a different license from the rest of APL (MIT). See the [LICENSE](https://github.com/noahc3/AffinityPluginLoader/blob/main/WineFix/LICENSE) file for details.
