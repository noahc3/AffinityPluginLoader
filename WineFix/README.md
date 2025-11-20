# WineFix

WineFix is an APL plugin which aims to patch bugs encountered when running Affinity on Linux under Wine using
runtime code patches. 

## Install

To install WineFix, download `apl-winefix-vX.X.X.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases) and extract the `plugins` directory to your Affinity install directory. That is, `WineFix.dll` should be inside the `plugins` folder in your Affinity directory after extraction.

## Included Patches

As of now WineFix fixes the following bugs:

- Fixed: Preferences fail to save on application exit
- Fixed: Vector path preview lines are drawn incorrectly and don't match the underlying stroke path

> [!WARNING]
> WineFix currently patches out the Canva sign-in dialog prompt when launching Affinity. This is temporary and the sign-in dialog prompt will be restored as soon as we have a known consistent way to fix the sign-in browser redirect and Affinity protocol handler.

More fixes are planned. We are currently researching potential solutions for the following bugs:

- Color picker doesn't display zoom image on Wayland
- Accepting crash reporting causes permanent crash until prefs are cleared
- Embedded SVG document editor causes crash after being open for some amount of time

We are open to resolving any Wine-specific bugs. Feel free to open an issue requesting a patch for any
particular bug you encounter. Just please keep in mind these bugs take time to research and develop patches for,
especially if the bug needs to be patched in native code.

## Licensing

WineFix is licensed under the terms of the GPLv2 except for the exclusion and exemption noted below. You can find a copy of the license in the LICENSE file.

### License Exclusion

WineFix includes source code from the Wine project for d2d1.dll under `/WineFix/lib/d2d1`. In accordance with the original project, the code in this directory are instead licensed under the LGPLv2.1. A copy of this license can be found at `/WineFix/lib/d2d1/LICENSE`. Changes have been applied to the d2d1 source code to implement a recursive cubic bezier subdivision algorithm to correct cubic bezier rendering in Affinity, and to allow building d2d1.dll standalone from the full Wine source code repository.

### License Exemption

[Canva](https://github.com/canva) and it's subsidiaries are exempt from GPLv2 licensing and may (at its option) instead license any source code authored for the WineFix project under the Zero-Clause BSD license. 
- Due to requirements of the upstream Wine licensing, this exemption **does not apply** to the d2d1.dll implementation source code, ie. all code under `WineFix/lib/d2d1/` is excluded from this exemption.


# Credits

Big thanks to the following projects:

- [AffinityOnLinux](https://github.com/seapear/AffinityOnLinux)
- [Harmony](https://github.com/pardeike/Harmony)
- [ElementalWarrior wine](https://gitlab.winehq.org/ElementalWarrior/wine)
- [Upstream wine](https://gitlab.winehq.org/wine/wine)
- [Affinity by Canva](https://www.affinity.studio/)
