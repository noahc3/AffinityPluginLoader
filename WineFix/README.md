# WineFix

WineFix is an APL plugin which aims to patch bugs encountered when running Affinity on Linux under Wine using
runtime code patches. 

## Install

To install WineFix, download `apl-winefix-vX.X.X.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases) and extract the `plugins` directory to your Affinity install directory. That is, `WineFix.dll` should be inside the `plugins` folder in your Affinity directory after extraction.

## Included Patches

As of now WineFix fixes the following bugs:

- Fixed: Preferences not saving on Linux

> [!WARNING]
> Warning: WineFix currently patches out the Canva sign-in dialog prompt when launching Affinity. This is temporary and the sign-in dialog prompt will be restored as soon as we have a known consistent way to fix the sign-in browser redirect and Affinity protocol handler.

More fixes are planned. We are currently researching potential solutions for the following bugs:

- Pen preview line doesn't match actual stroke path
- Color picker doesn't display zoom image

We are open to resolving any Wine-specific bugs. Feel free to open an issue requesting a patch for any
particular bug you encounter. Just please keep in mind these bugs take time to research and develop patches for,
especially if the bug needs to be patched in native code.

## License

WineFix is licensed under the terms of the GPLv2 except for the exemption noted below. You can find a copy of the license in the LICENSE file.

### License Exemption

[Canva](https://github.com/canva) and it's subsidiaries are exempt from MIT licensing and may (at its option) instead license any source code authored for the WineFix project under the Zero-Clause BSD license.


# Credits

Big thanks to the following projects:

- [AffinityOnLinux](https://github.com/seapear/AffinityOnLinux)
- [Harmony](https://github.com/pardeike/Harmony)
- [ElementalWarrior wine](https://gitlab.winehq.org/ElementalWarrior/wine)
- [Upstream wine](https://gitlab.winehq.org/wine/wine)
- [Affinity by Canva](https://www.affinity.studio/)
