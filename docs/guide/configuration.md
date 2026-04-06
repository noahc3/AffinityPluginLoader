# Configuration

APL and its plugins are configured through a preferences page inside Affinity's preferences dialog, TOML files on disk, or environment variables.

## Preferences GUI

APL injects a preferences page into Affinity's built-in preferences dialog (Edit → Preferences). Each plugin that defines settings gets its own tab. Changes made in the GUI are saved when the preferences dialog is closed.

## TOML Files

Settings are stored as TOML files in the `apl/config/` directory inside your Affinity install folder. Each plugin gets its own file named `<pluginid>.toml`. APL auto-generates these files with defaults and comments on first launch.

For example, APL's own settings are in `apl/config/apl.toml`:

```toml
# Logging

# Enable logging to file
# Write APL and plugin log output to apl/logs/apl.latest.log.
# Values: true, false
file_logging = false

# Log level
# Minimum severity level for log messages.
# Values: DEBUG, INFO, WARNING, ERROR, NONE
log_level = "INFO"

# Advanced

# Force WPF fallback controls
# Use standard WPF controls instead of Affinity's built-in controls for plugin preferences pages.
# Values: true, false
force_wpf_controls = false
```

You can edit these files while Affinity is closed. Changes take effect on next launch.

## Environment Variables

Any setting can be overridden via environment variables using the format:

```
APL__<PLUGINID>__<KEY>=<value>
```

All parts are uppercase. For example:

| Setting | Environment Variable |
|---|---|
| APL log level | `APL__APL__LOG_LEVEL=DEBUG` |
| APL file logging | `APL__APL__FILE_LOGGING=true` |

Environment variable overrides take priority over both the GUI and TOML values. They are temporary — the override only applies while the variable is set. The underlying TOML/GUI value is not modified, so removing the environment variable restores the original setting on next launch.

Boolean values accept `true`/`false` or `1`/`0`.

## APL Settings Reference

These are APL's own built-in settings (plugin ID: `apl`):

| Key | Type | Default | Description |
|---|---|---|---|
| `file_logging` | bool | `false` | Write log output to `apl/logs/apl.latest.log`. Up to 5 rotated log files are kept. |
| `log_level` | enum | `INFO` | Minimum log severity. Values: `DEBUG`, `INFO`, `WARNING`, `ERROR`, `NONE`. |
| `force_wpf_controls` | bool | `false` | Use standard WPF controls instead of Affinity's native controls for plugin preferences pages. |

## Logging

When launched from a terminal, APL writes log output to the console. Enable `file_logging` to also write to `apl/logs/apl.latest.log`. Log files are rotated automatically — up to 5 previous logs are kept (`apl.1.log` through `apl.5.log`).

Log output includes timestamps, severity levels, and the source plugin:

```
[14:32:01] [INFO] [APL/Core] APL logging initialized
[14:32:01] [INFO] [APL/Core] Stage 0 complete: 2 plugins discovered, settings loaded
[14:32:02] [INFO] [APL/WineFix] Skipping ColorPicker Wayland fix (setting: auto)
```
