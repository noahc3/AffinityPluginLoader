# LoginFix

LoginFix is an APL plugin which fixes the Canva sign-in flow crashing under Wine.

WineFix's own "Canva sign-in dialog patched out" fix is a temporary workaround, pending a protocol handler fix. LoginFix is that fix — it lets sign-in complete instead of disabling it.

## Install

1. [Install APL](../guide/installation.md) first.
2. Download `loginfix-vX.X.X.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases) and extract into your Affinity install directory.

LoginFix has no configurable settings, once installed, it just works.

## What It Fixes

`Serif.Affinity.Application.ProcessCommandLineArguments` has one branch, only reached for an unrelated `affinity-open-file:` argument, that calls the WinRT type `Windows.ApplicationModel.DataTransfer.SharedStorageAccessManager`. Wine has no implementation of that type.

The .NET CLR resolves every type referenced anywhere in a method's body when it JITs that method, not just the branch actually taken. So any call into `ProcessCommandLineArguments` throws `System.TypeLoadException`, regardless of which arguments were passed.

This matters because the Canva sign-in callback goes through this exact method. The second `Affinity.exe` instance the OS launches for the callback URL forwards its arguments to the already-running instance over a named pipe, and that named-pipe handler lands in `ProcessCommandLineArguments`. So login always crashed.

Harmony itself cannot patch `ProcessCommandLineArguments` directly either, patching a method requires Harmony to decompile its IL, which means resolving every operand in the method body, including the poisoned `SharedStorageAccessManager` call.

LoginFix instead patches `ProcessCommandLineArguments`'s two callers, `ProcessArguments()` (the app's own startup command line) and `SingleInstanceThread()` (the named-pipe listener that receives the sign-in callback). Neither references the poisoned type directly, only by method signature, which Harmony can resolve without a problem. Both are fully replaced with a safe reimplementation that never calls the real `ProcessCommandLineArguments`.

## Compatibility with WineFix

LoginFix and WineFix patch different methods and can run together. If both are installed, WineFix's sign-in dialog suppression and LoginFix's working sign-in flow may conflict, since one hides the dialog and the other completes it.

## Licensing

LoginFix is licensed under GPLv2, with a Zero-Clause BSD exemption for Canva and its subsidiaries. See the [LICENSE](https://github.com/noahc3/AffinityPluginLoader/blob/main/LoginFix/LICENSE) file.
