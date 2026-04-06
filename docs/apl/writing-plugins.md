# Writing Plugins

APL plugins are .NET class libraries that implement the `IAffinityPlugin` interface.

## Plugin Structure

Create a .NET class library targeting the same framework as APL, then implement `IAffinityPlugin`:

```csharp
using HarmonyLib;
using AffinityPluginLoader;

public class MyPlugin : IAffinityPlugin
{
    public void Initialize(Harmony harmony)
    {
        // Apply your patches here
    }
}
```

Your `Initialize()` method receives a `Harmony` instance you can use to apply runtime patches via prefixes, postfixes, and IL transpilers.

## Finding Patch Targets

Use `AppDomain.CurrentDomain.GetAssemblies()` to find loaded Affinity assemblies, then use reflection to locate the types and methods you want to patch:

```csharp
var assembly = AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "Serif.Affinity");

var targetType = assembly?.GetType("Some.Namespace.TargetClass");
var targetMethod = targetType?.GetMethod("TargetMethod");
```

## Applying Patches

Use Harmony's standard patching API:

```csharp
harmony.Patch(targetMethod,
    prefix: new HarmonyMethod(typeof(MyPlugin), nameof(MyPrefix)));
```

See the [Harmony documentation](https://harmony.pardeike.net/) for full details on prefixes, postfixes, and transpilers.

!!! tip
    Reference the [WineFix source code](https://github.com/noahc3/AffinityPluginLoader/tree/main/WineFix) for a complete working example of the plugin pattern.

## Building

### Linux

Use the provided Docker build script for a reproducible build environment:

```bash
./docker-build.sh
```

### Windows

```bash
build.bat
```
