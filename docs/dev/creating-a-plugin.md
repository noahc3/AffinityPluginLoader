# Creating a Plugin

This guide walks through creating an APL plugin from scratch.

!!! warning "Unstable API"
    APL is experimental. While APL is still v0, expect breaking changes to the plugin API between updates.


## Prerequisites

- .NET Framework 4.8 SDK (or the Docker build environment — see [Building](#building))
- A reference to `AffinityPluginLoader.dll` and `0Harmony.dll` from an APL release

## Project Setup

Create a .NET Framework 4.8 class library. Add references to `AffinityPluginLoader.dll` and `0Harmony.dll`.

## Minimal Plugin

Plugins extend the `AffinityPlugin` base class. At minimum, override `OnPatch` to apply Harmony patches when Affinity's assemblies are loaded:

```csharp
using HarmonyLib;
using AffinityPluginLoader;

public class MyPlugin : AffinityPlugin
{
    public override void OnPatch(Harmony harmony, IPluginContext context)
    {
        context.Patch("My patch", h =>
        {
            // Find and patch target methods here
        });
    }
}
```

Place the compiled DLL in `apl/plugins/` and launch Affinity through `AffinityHook.exe`.

## Plugin Metadata

APL reads standard .NET assembly attributes to display plugin info in the preferences dialog:

```xml
<PropertyGroup>
    <AssemblyTitle>My Plugin</AssemblyTitle>
    <Version>1.0.0</Version>
    <Company>Your Name</Company>
    <Description>A short description of what this plugin does.</Description>
</PropertyGroup>
```

The plugin ID is derived from the `AssemblyProduct` (or `AssemblyTitle`) attribute, lowercased with spaces replaced by hyphens.

## Plugin Lifecycle

APL loads plugins through a staged pipeline that mirrors Affinity's own startup sequence. Each stage corresponds to a virtual method on `AffinityPlugin` that you can override:

| Stage | Method | What's Available |
|---|---|---|
| 0 – Load | `OnLoad` | Plugin discovered, settings initialized. No Affinity types yet. |
| 1 – Patch | `OnPatch` | Serif assemblies loaded. Apply Harmony patches here. |
| 2 – ServicesReady | `OnServicesReady` | Affinity's DI container and services initialized. |
| 3 – Ready | `OnReady` | Full runtime including native engine, tools, effects. |
| 4 – UiReady | `OnUiReady` | Main window loaded. Full UI tree available. |
| 5 – StartupComplete | `OnStartupComplete` | Splash hidden, app idle. Safe to show dialogs. |

You only need to override the stages you use. Most plugins only need `OnPatch`.

### Stage Details

**Stage 0 – Load:** Called immediately after plugin discovery. Settings are already loaded from TOML. Use this for early setup that doesn't require any Affinity types.

**Stage 1 – Patch:** The main patching stage. Serif assemblies (`Serif.Affinity`, `Serif.Interop.Persona`, etc.) are loaded. Apply Harmony patches here. Use `context.Patch()` for automatic deferral if a dependency isn't loaded yet (see [Patching with Harmony](#patching-with-harmony)).

**Stages 2–5:** Triggered by Harmony postfixes on Affinity's internal lifecycle methods (`InitialiseServices`, `OnServicesInitialised`, `OnMainWindowLoaded`, `PostLoad`). Use these for work that depends on Affinity's runtime being progressively more initialized.

### IPluginContext

Every stage method receives an `IPluginContext` with:

- `Harmony` — shared Harmony instance for patching
- `Settings` — your plugin's `SettingsStore` (null if you didn't define settings)
- `CurrentStage` — the current `LoadStage` enum value
- `Patch(description, action)` — apply a patch with automatic deferral (see below)
- `Log()` / `LogWarning()` / `LogError()` — logging helpers that tag output with your plugin ID

## Patching with Harmony

### Finding Patch Targets

Use reflection to find types and methods in Affinity's assemblies at runtime:

```csharp
public override void OnPatch(Harmony harmony, IPluginContext context)
{
    context.Patch("Fix something", h =>
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

        var targetType = assembly?.GetType("Some.Namespace.TargetClass");
        var targetMethod = targetType?.GetMethod("TargetMethod",
            BindingFlags.Public | BindingFlags.Instance);

        h.Patch(targetMethod,
            prefix: new HarmonyMethod(typeof(MyPlugin), nameof(MyPrefix)));
    });
}

static bool MyPrefix() => false; // Skip original method
```

### Automatic Patch Deferral

Use `context.Patch()` instead of calling `harmony.Patch()` directly. If your patch throws a `TypeLoadException` (because a transitive dependency isn't loaded yet), APL automatically defers it and retries when new assemblies are loaded:

```csharp
context.Patch("My deferred patch", h =>
{
    // This is safe even if the target type's dependencies
    // aren't loaded yet — APL will retry automatically
    h.Patch(targetMethod, prefix: new HarmonyMethod(...));
});
```

### Patch Types

Harmony supports several patch types. The most common ones are:

- **Prefix** — runs before the original method. Can skip the original by returning `false`.
- **Postfix** — runs after the original method. Can modify the return value.
- **Transpiler** — rewrites the IL instructions of the original method.

See the [Harmony patching documentation](https://harmony.pardeike.net/articles/patching.html) for the full list of patch types and detailed usage.

### Native Code Patching

For patching native (unmanaged) DLLs — COM interface hooking, in-memory byte patching — see the [Native Code APIs](native-apis.md) documentation.

## Settings API

Override `DefineSettings()` to declare configuration options for your plugin. APL auto-generates a preferences page in Affinity's preferences dialog and persists values to a TOML file.

```csharp
public override PluginSettingsDefinition DefineSettings()
{
    return new PluginSettingsDefinition("myplugin")
        .AddSection("General")
        .AddBool("my_toggle", "Enable feature",
            defaultValue: true,
            description: "Description shown below the toggle.")
        .AddEnum("my_choice", "Pick one", new List<EnumOption>
        {
            new EnumOption("a", "Option A"),
            new EnumOption("b", "Option B"),
        })
        .AddSlider("my_slider", "Amount", 0, 100, defaultValue: 50);
}
```

### Setting Types

| Method | Control | Value Type |
|---|---|---|
| `AddBool` | Toggle switch | `bool` |
| `AddString` | Text input | `string` |
| `AddEnum` | Dropdown | `string` (one of the option values) |
| `AddSlider` | Slider | `double` |
| `AddDropdownSlider` | Dropdown with slider | `double` |

### Setting Options

All setting types accept these common parameters:

| Parameter | Description |
|---|---|
| `key` | Unique key used in TOML and environment variables |
| `displayName` | Label shown in the preferences UI |
| `defaultValue` | Value used when no config file or override exists |
| `description` | Help text shown below the control. Supports basic markdown. |
| `restartRequired` | When `true`, shows a restart notice when the value changes |
| `infoMessage` | Tooltip text shown on an (i) icon next to the setting name |

Slider types additionally accept `minimum`, `maximum`, and `precision` (decimal places).

### Layout Elements

You can add non-setting elements to organize the preferences page:

| Method | Description |
|---|---|
| `AddSection(title)` | Section header. Also groups settings under a TOML table. |
| `AddInlineText(text)` | Plain text paragraph |
| `AddInlineMutedText(text)` | Dimmed/secondary text |
| `AddInlineXaml(xaml, dataContext)` | Custom XAML content |

### Reading Settings

Use the `SettingsStore` on `context.Settings` to read values:

```csharp
// Get the effective value (respects environment variable overrides)
bool enabled = context.Settings.GetEffectiveValue<bool>("my_toggle");

// Get the stored value only (ignores env overrides)
string choice = context.Settings.GetValue<string>("my_choice");

// Check if a setting is overridden by an environment variable
bool isOverridden = context.Settings.IsOverriddenByEnv("my_toggle");
```

Use `GetEffectiveValue<T>()` in most cases — it checks for environment variable overrides first, then falls back to the TOML/GUI value.

### Environment Variable Overrides

Any plugin setting can be overridden via environment variables:

```
APL__<PLUGINID>__<KEY>=<value>
```

For example, a plugin with ID `myplugin` and setting key `my_toggle`:

```bash
APL__MYPLUGIN__MY_TOGGLE=true
```

### Custom Preferences XAML

For advanced cases, override `GetCustomPreferencesXaml()` to provide custom XAML for your plugin's preferences tab instead of the auto-generated UI.

## Building

### Linux

Use the provided Docker build script for a reproducible build environment:

```bash
./docker-build.sh
```

To build and deploy directly to your Affinity install directory for testing:

```bash
./deploy.sh --set-affinity-path /path/to/affinity   # one-time setup
./deploy.sh                                           # build and deploy
```

### Windows

```bash
build.bat
```

## Example

See the [WineFix source code](https://github.com/noahc3/AffinityPluginLoader/tree/main/WineFix) for a complete working plugin with multiple patches, settings with sections, conditional patching based on settings, and deferred patch application.
