# Installation

## Download

Download `affinitypluginloader-vX.X.X-amd64.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases).

## Install

Extract the contents of the archive into your Affinity install directory:

- **Windows:** `C:\Program Files\Affinity\Affinity\`
- **Linux (Wine):** `<wineprefix>/drive_c/Program Files/Affinity/Affinity/`

## Launch

Launch `AffinityHook.exe` instead of `Affinity.exe`. On Linux, launch it with Wine the same way you would launch Affinity.

!!! tip "Keep existing shortcuts working"
    You can rename `Affinity.exe` to `Affinity.real.exe` and rename `AffinityHook.exe` to `Affinity.exe`. However, Affinity updates may overwrite or remove the hook if you do this. It's recommended to update your shortcuts to point to `AffinityHook.exe` instead.

## Installing Plugins

Place plugin DLLs in the `apl/plugins/` directory inside your Affinity install folder. APL discovers and loads them automatically on launch.

## Directory Layout

After installation, your Affinity directory will look like this:

```
Affinity/
├── AffinityHook.exe          # APL entry point
├── AffinityBootstrap.dll      # Native bootstrap
├── AffinityPluginLoader.dll   # Core plugin loader
├── 0Harmony.dll               # Harmony library
├── Affinity.exe               # Original Affinity executable
└── apl/
    ├── plugins/               # Place plugin DLLs here
    ├── config/                # TOML settings files (auto-generated)
    └── logs/                  # Log files (when file logging is enabled)
```
