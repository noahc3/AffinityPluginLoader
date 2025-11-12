# AffinityPluginLoader

A managed code plugin loader & injector hook for Affinity by Canva (Affinity v3).

APL gives you a simple method to load custom code into Affinity and perform custom patches at runtime using
the Harmony library. No more patching DLL files on disk.

APL supports Windows and Linux (Wine). MacOS support is not planned at this time.

## Install

1. Download `affinitypluginloader-vX.X.X.X-amd64.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases).
2. Extract the contents of the archive into your Affinity install directory
  - This is `C:/Program Files/Affinity/Affinity/` by default on Windows, or
    `path/to/wineprefix/drive_c/Program Files/Affinity/Affinity/` on Linux.

That's it. On Windows, just launch AffinityHook.exe instead of Affinity.exe. On Linux, launch AffinityHook.exe
with Wine instead of Affinity.exe.

Alternatively if you want your existing shortcuts to work without any changes, you can:

- Rename `Affinity.exe` to `Affinity.real.exe`
- Rename `AffinityHook.exe` to `Affinity.exe`

However, doing this means updates to Affinity may not work correctly or AffinityHook may be removed on updates.
As such it's recommended you update your existing shortcuts or create new shortcuts for `AffinityHook.exe`.

## WineFix

WineFix is an APL plugin which aims to patch bugs encountered when running Affinity on Linux under Wine using
runtime code patches. 

To install WineFix, download `apl-winefix-vX.X.X.X.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases) and extract the `plugins`
directory to your Affinity install directory. That is, `WineFix.dll` should be inside the `plugins` folder
in your Affinity directory after extraction.

### Included Patches

As of now WineFix fixes the following bugs:

- Fixed: Preferences not saving on Linux

**Warning: WineFix currently patches out the Canva sign-in dialog prompt when launching Affinity. This is
temporary and the sign-in dialog prompt will be restored as soon as we have a known consistent way to fix
the sign-in browser redirect and Affinity protocol handler.**

More fixes are planned. We are currently researching potential solutions for the following bugs:

- Pen preview line doesn't match actual stroke path
- Color picker doesn't always display highlighted color

We are open to resolving any Wine-specific bugs. Feel free to open an issue requesting a patch for any
particular bug you encounter. Just please keep in mind these bugs take time to research and develop patches for,
especially if the bug needs to be patched in native code.

## Developing Plugins

A better guide will be written up soon. For now you should reference the source code of WineFix for how
to get the basic plugin structure going and how you can patch code using IL transpilation. Also take a look the
source code of AffinityPluginLoader for an example of how to inject custom UI and XAML.

## Licensing

AffinityHook, AffinityBootstrap, AffinityPluginLoader, and the project solution configuration files in the root
directory of the repository are licensed under the MIT License except for the exemption noted below. You can 
find a copy of the license in the LICENSE file under the directories for each respective project.

WineFix is licensed under the terms of the GPLv2 except for the exemption noted below. You can find a copy of 
the license in the LICENSE file in the WineFix directory.

### License Exemption

[Canva](https://github.com/canva) and it's subsidiaries are exempt from MIT and GPLv2 licensing and may (at its
option) instead license any source code authored for the AffinityHook, AffinityBootstrap, AffinityPluginLoader
and WineFix projects under the Zero-Clause BSD license.

## Credits

Big thanks to the following projects:

- [AffinityOnLinux](https://github.com/seapear/AffinityOnLinux)
- [Harmony](https://github.com/pardeike/Harmony)
- [ElementalWarrior wine](https://gitlab.winehq.org/ElementalWarrior/wine)
- [Upstream wine](https://gitlab.winehq.org/wine/wine)
- [Affinity by Canva](https://www.affinity.studio/)
