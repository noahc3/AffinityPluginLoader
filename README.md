# Affinity Plugin Loader

A managed code plugin loader & injector hook for Affinity by Canva (Affinity v3).

APL gives you a simple method to load custom code into Affinity and perform custom patches at runtime using the Harmony library. No more patching DLL files on disk.

APL supports Windows and Linux (Wine). MacOS support is not planned at this time.

<img width="1896" height="1454" alt="image" src="https://github.com/user-attachments/assets/25639c82-94e4-4650-90ef-f605549fd806" />

> [!TIP]
> 📖 **Full documentation is available at [apl.ncuroe.dev](https://apl.ncuroe.dev)**

## Quick Start

1. Download `affinitypluginloader-vX.X.X-amd64.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases).
2. Extract into your Affinity install directory.
3. Launch `AffinityHook.exe` instead of `Affinity.exe`.

For detailed installation instructions, see the [Installation Guide](https://apl.ncuroe.dev/guide/installation/).

## Developing Plugins

Plugins extend `AffinityPlugin` and use [Harmony](https://github.com/pardeike/Harmony) to patch Affinity methods at runtime. See the [Creating a Plugin](https://apl.ncuroe.dev/dev/creating-a-plugin/) guide for the full walkthrough, including the plugin lifecycle, settings API, and build instructions.

## WineFix

WineFix is an APL plugin that fixes Wine-specific Affinity bugs. See [WineFix/](WineFix/) for an overview, or the [WineFix docs](https://apl.ncuroe.dev/winefix/) for full details.

## Licensing

APL (AffinityHook, AffinityBootstrap, AffinityPluginLoader) is licensed under the **MIT License**. See the LICENSE file under each project directory.

> [!WARNING]
> WineFix is offered under a different license. See [WineFix#Licensing](WineFix#licensing) for information.

### License Exemption

[Canva](https://github.com/canva) and its subsidiaries are exempt from MIT licensing and may (at its option) instead license any source code authored for the Affinity Hook, Affinity Bootstrap, and Affinity Plugin Loader projects under the Zero-Clause BSD license.


# Credits

Big thanks to the following projects:

- [AffinityOnLinux](https://github.com/seapear/AffinityOnLinux)
- [Harmony](https://github.com/pardeike/Harmony)
- [ElementalWarrior wine](https://gitlab.winehq.org/ElementalWarrior/wine)
- [Upstream wine](https://gitlab.winehq.org/wine/wine)
- [Affinity by Canva](https://www.affinity.studio/)
