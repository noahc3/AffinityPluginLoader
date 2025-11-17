# Affinity Plugin Loader

> [!NOTE]
> *Information on WineFix has moved, see [WineFix/](WineFix#winefix)*


A managed code plugin loader & injector hook for Affinity by Canva (Affinity v3).

APL gives you a simple method to load custom code into Affinity and perform custom patches at runtime using the Harmony library. No more patching DLL files on disk.

APL supports Windows and Linux (Wine). MacOS support is not planned at this time.

<img width="2880" height="1667" alt="image" src="https://github.com/user-attachments/assets/dfb26511-22a4-4bb4-be90-1bcce3cd6909" />

## Install

1. Download `affinitypluginloader-vX.X.X-amd64.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases).
2. Extract the contents of the archive into your Affinity install directory
  - This is `C:/Program Files/Affinity/Affinity/` by default on Windows, or
    `path/to/wineprefix/drive_c/Program Files/Affinity/Affinity/` on Linux.

That's it. On Windows, just launch AffinityHook.exe instead of Affinity.exe. On Linux, launch AffinityHook.exe
with Wine instead of Affinity.exe.

Alternatively if you want your existing shortcuts to work without any changes, you can:

- Rename `Affinity.exe` to `Affinity.real.exe`
- Rename `AffinityHook.exe` to `Affinity.exe`

However, doing this means updates to Affinity may not work correctly or Affinity Hook may be removed on updates.
As such it's recommended you update your existing shortcuts or create new shortcuts for `AffinityHook.exe`.


## Developing Plugins

A better guide will be written up soon. For now you should reference the source code of WineFix for how to get the basic plugin structure going and how you can patch code using IL transpilation. Also take a look the source code of Affinity Plugin Loader for an example of how to inject custom UI and XAML.

Build scripts are provided for Windows and Linux.

> [!TIP]
> Developing on Linux? You may want to use [apl-devcontainer](https://github.com/noahc3/apl-devcontainer) to get a working build environment. Feel free to take a look at the Dockerfile to get an idea of what you'll need on your host system to build without Docker. 

> APL fully supports building on Linux via mingw-w64 and the dotnet SDK, you'll just need to grab a Windows SDK header and library from Wine.


## Licensing

Affinity Hook, Affinity Bootstrap, Affinity Plugin Loader, and the project solution configuration files in the root
directory of the repository are licensed under the MIT License except for the exemption noted below. You can 
find a copy of the license in the LICENSE file under the directories for each respective project.

> [!WARNING]
> WineFix is offered under a different license. See [WineFix#Licensing](WineFix#Licensing) for information.


### License Exemption

[Canva](https://github.com/canva) and it's subsidiaries are exempt from MIT licensing and may (at its option) instead license any source code authored for the Affinity Hook, Affinity Bootstrap, and Affinity Plugin Loader projects under the Zero-Clause BSD license.


# Credits

Big thanks to the following projects:

- [AffinityOnLinux](https://github.com/seapear/AffinityOnLinux)
- [Harmony](https://github.com/pardeike/Harmony)
- [ElementalWarrior wine](https://gitlab.winehq.org/ElementalWarrior/wine)
- [Upstream wine](https://gitlab.winehq.org/wine/wine)
- [Affinity by Canva](https://www.affinity.studio/)
