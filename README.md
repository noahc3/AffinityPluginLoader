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

Plugins extend `AffinityPlugin` and override stage methods to hook into Affinity's loading pipeline. APL calls your plugin at each stage as Affinity starts up:

| Stage | Method | What's available |
|---|---|---|
| 0 – Load | `OnLoad` | Plugin discovered, settings initialized. No Affinity types yet. |
| 1 – Patch | `OnPatch` | Serif assemblies loaded. Apply Harmony patches here. |
| 2 – ServicesReady | `OnServicesReady` | Affinity's DI container and services initialized. |
| 3 – Ready | `OnReady` | Full runtime including native engine, tools, effects. |
| 4 – UiReady | `OnUiReady` | Main window loaded. Full UI tree available. |
| 5 – StartupComplete | `OnStartupComplete` | Splash hidden, app idle. Safe to show dialogs. |

Each stage method receives an `IPluginContext` with:
- `Harmony` — shared Harmony instance for patching
- `Settings` — your plugin's settings store (if you defined settings)
- `Patch(description, action)` — apply a patch with automatic deferral if dependencies aren't loaded yet
- `Log` / `LogWarning` / `LogError` — logging helpers

### Settings API

Override `DefineSettings()` to declare configuration options. APL auto-generates a preferences page in Affinity's preferences dialog using native Affinity controls.

```csharp
public override PluginSettingsDefinition DefineSettings()
{
    return new PluginSettingsDefinition("myplugin")
        .AddSection("General")
        .AddBool("my_toggle", "Enable feature", defaultValue: true,
            description: "Description shown below the toggle.")
        .AddEnum("my_choice", "Pick one", new List<EnumOption>
        {
            new EnumOption("a", "Option A"),
            new EnumOption("b", "Option B"),
        })
        .AddSlider("my_slider", "Amount", 0, 100, defaultValue: 50);
}
```

Supported setting types: `Bool`, `String`, `Enum`, `Slider`, `DropdownSlider`. Layout elements like `AddSection`, `AddInlineText`, `AddInlineMutedText`, and `AddInlineXaml` are also available. Settings descriptions support basic markdown formatting.

Settings are persisted as TOML in the `apl/config/` directory and can be overridden via environment variables (`APL__PLUGINID__KEY`).

### Minimal Plugin Example

```csharp
using HarmonyLib;
using AffinityPluginLoader;
using AffinityPluginLoader.Settings;

public class MyPlugin : AffinityPlugin
{
    public override PluginSettingsDefinition DefineSettings()
    {
        return new PluginSettingsDefinition("myplugin")
            .AddBool("enabled", "Enable patch", defaultValue: true);
    }

    public override void OnPatch(Harmony harmony, IPluginContext context)
    {
        if (context.Settings.GetEffectiveValue<bool>("enabled"))
        {
            context.Patch("My patch", h =>
            {
                // Use reflection to find target types, then apply Harmony patches
            });
        }
    }
}
```

See [WineFix/](WineFix/) for a full working example with multiple patches, settings, and deferred patching.

### Building

Build scripts are provided for Windows and Linux.

> [!TIP]
> Developing on Linux? Use `./docker-build.sh` to build inside a Docker container with all dependencies pre-configured. See `docker/Dockerfile` for the full list of build dependencies if you prefer to build on your host system. 
>
> APL fully supports building on Linux via mingw-w64 and the dotnet SDK, you'll just need to grab a Windows SDK header and library from Wine.

Use `./deploy.sh` to build and deploy directly to your Affinity install directory for testing.


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
