# WineFix Installation

## Prerequisites

[APL must be installed](../guide/installation.md) before installing WineFix.

## Download

Download `winefix-vX.X.X.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases), or download the combined `affinitypluginloader-plus-winefix.tar.xz` bundle which includes both APL and WineFix.

## Install

Extract the WineFix archive into your Affinity install directory. After extraction, the following files should be present:

```
Affinity/
├── d2d1.dll                   # Patched Wine d2d1 (next to Affinity.exe)
└── apl/
    └── plugins/
        └── WineFix.dll        # WineFix plugin
```

WineFix will be loaded automatically the next time you launch Affinity through AffinityHook.

## Included Files

For reference (e.g. if you need to uninstall WineFix manually), the release archive contains:

| File | Location | Purpose |
|---|---|---|
| `WineFix.dll` | `apl/plugins/WineFix.dll` | WineFix plugin (Harmony patches) |
| `d2d1.dll` | `d2d1.dll` (next to `Affinity.exe`) | Patched Wine d2d1 library (cubic bezier fix) |

Additionally, WineFix generates the following files on first launch:

| File | Location | Purpose |
|---|---|---|
| `winefix.toml` | `apl/config/winefix.toml` | Settings file (auto-generated with defaults) |

## Uninstall

Delete the files listed above from your Affinity install directory. If you also want to remove the auto-generated config, delete `apl/config/winefix.toml`.
