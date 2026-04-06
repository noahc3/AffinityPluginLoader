# Getting Started

## Install

1. Download `affinitypluginloader-vX.X.X-amd64.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases).
2. Extract the contents into your Affinity install directory:
    - **Windows:** `C:\Program Files\Affinity\Affinity\`
    - **Linux (Wine):** `<wineprefix>/drive_c/Program Files/Affinity/Affinity/`

## Launch

Launch `AffinityHook.exe` instead of `Affinity.exe`.

On Linux, launch it with Wine the same way you would launch Affinity.

!!! tip
    To keep your existing shortcuts working, you can rename `Affinity.exe` to `Affinity.real.exe` and rename `AffinityHook.exe` to `Affinity.exe`. However, Affinity updates may overwrite or remove the hook if you do this.

## Installing Plugins

Place plugin DLLs in the `plugins/` directory inside your Affinity install folder. APL discovers and loads them automatically on launch.
