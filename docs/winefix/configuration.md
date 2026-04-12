# WineFix Configuration

WineFix settings are configurable from Affinity's preferences dialog (under the WineFix tab), by editing the TOML file on disk, or via environment variables.

For general information about how APL settings work (GUI, TOML files, environment variables), see [APL Configuration](../guide/configuration.md).

## Settings Reference

WineFix uses plugin ID `winefix`. Settings are stored in `apl/config/winefix.toml`.

### Patches

| Key | Type | Default | Restart Required | Description |
|---|---|---|---|---|
| `color_picker_magnifier_fix` | enum | `auto` | Yes | Wayland zoom preview fix. Replaces `CopyFromScreen` (which returns black on Wayland) with a `BitBlt` from the canvas window. |
| `color_picker_sampling_mode` | enum | `native` | No | Controls how the color picker samples color values. See [Sampling Modes](index.md#color-picker-sampling-modes). |
| `canva_sign_in_helper` | bool | `true` | Yes | Patch the Canva sign-in dialog to include a helper textbox and instructions to complete sign-in without a protocol URL handler. |
| `command_line_file_open` | bool | `true` | Yes | Fix opening files from the command line or desktop file manager. Bypasses Affinity's broken `ProcessCommandLineArguments` which references a WinRT type missing in Wine. |

#### `color_picker_magnifier_fix`

| Value | Behavior |
|---|---|
| `auto` | Apply the fix only if Wayland or XWayland is detected (checks `WAYLAND_DISPLAY`). |
| `enabled` | Always apply the fix. |
| `disabled` | Never apply the fix. |

!!! note
    Enabling this on X11 desktop environments will prevent the zoom preview from displaying content outside the bounds of the canvas window.

#### `color_picker_sampling_mode`

| Value | Behavior |
|---|---|
| `native` | Use Affinity's built-in color sampling. Colors within the canvas use the document's native color space. The highlighted pixel in the zoom preview may differ slightly from the sampled value. |
| `exact` | Pick the exact color of the highlighted pixel in the zoom preview. Samples from a screen capture in sRGB. Not recommended for CMYK or wide-gamut documents. |

### Crash Fixes

| Key | Type | Default | Restart Required | Description |
|---|---|---|---|---|
| `force_sync_font_enum` | bool | `true` | Yes | Disable parallel font enumeration to reduce startup crashes. May increase startup time on systems with many fonts. |

## Environment Variable Overrides

Any WineFix setting can be overridden via environment variables using the format:

```
APL__WINEFIX__<KEY>=<value>
```

| Setting | Environment Variable | Example |
|---|---|---|
| Color picker magnifier fix | `APL__WINEFIX__COLOR_PICKER_MAGNIFIER_FIX` | `APL__WINEFIX__COLOR_PICKER_MAGNIFIER_FIX=disabled` |
| Color picker sampling mode | `APL__WINEFIX__COLOR_PICKER_SAMPLING_MODE` | `APL__WINEFIX__COLOR_PICKER_SAMPLING_MODE=exact` |
| Force synchronous font enumeration | `APL__WINEFIX__FORCE_SYNC_FONT_ENUM` | `APL__WINEFIX__FORCE_SYNC_FONT_ENUM=false` |
| Command-line file opening fix | `APL__WINEFIX__COMMAND_LINE_FILE_OPEN` | `APL__WINEFIX__COMMAND_LINE_FILE_OPEN=false` |

Environment variable overrides take priority over both the GUI and TOML values. They are temporary — the override only applies while the variable is set.
