# WineFix

WineFix is an APL plugin which patches bugs encountered when running Affinity on Linux under Wine using runtime code patches.

> [!TIP]
> 📖 **Full documentation at [apl.ncuroe.dev/winefix](https://apl.ncuroe.dev/winefix/)**

## Install

1. [Install APL](https://apl.ncuroe.dev/guide/installation/) first.
2. Download `winefix-vX.X.X.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases) and extract into your Affinity install directory.

For detailed instructions, see the [WineFix Installation Guide](https://apl.ncuroe.dev/winefix/installation/).

## Included Patches

- Preferences fail to save on application exit
- Vector path preview lines drawn incorrectly
- Color picker zoom preview displays a black image on Wayland
- Intermittent startup crash from parallel font enumeration
- Canva sign-in works via a paste-URL workaround for the Wine protocol handler

## Configuration

WineFix is configurable from Affinity's preferences dialog, TOML files, or environment variables. See the [Configuration Reference](https://apl.ncuroe.dev/winefix/configuration/) for all settings and options.

## Known Open Bugs

- Accepting crash reporting causes permanent crash until prefs are cleared
- Embedded SVG document editor crashes after being open for some time

We are open to resolving any Wine-specific bugs. Feel free to [open an issue](https://github.com/noahc3/AffinityPluginLoader/issues) requesting a patch.

## Licensing

WineFix is licensed under **GPLv2**. See the [LICENSE](LICENSE) file.

The bundled `d2d1.dll` source (under `WineFix/lib/d2d1/`) is licensed under **LGPLv2.1** per upstream Wine licensing. Changes have been applied to implement a cubic bezier subdivision algorithm and to allow building d2d1.dll standalone.

### License Exemption

[Canva](https://github.com/canva) and its subsidiaries are exempt from GPLv2 licensing and may (at its option) instead license any source code authored for the WineFix project under the Zero-Clause BSD license. This exemption **does not apply** to the d2d1.dll source code under `WineFix/lib/d2d1/`.


# Credits

Big thanks to the following projects:

- [AffinityOnLinux](https://github.com/seapear/AffinityOnLinux)
- [Harmony](https://github.com/pardeike/Harmony)
- [ElementalWarrior wine](https://gitlab.winehq.org/ElementalWarrior/wine)
- [Upstream wine](https://gitlab.winehq.org/wine/wine)
- [Affinity by Canva](https://www.affinity.studio/)
