# LoginFix

LoginFix is an APL plugin which fixes the Canva sign-in flow crashing under Wine.

WineFix's own "Canva sign-in dialog patched out" fix is a temporary workaround, pending a protocol handler fix. LoginFix is that fix: it lets sign-in complete instead of disabling it.

## Install

1. [Install APL](https://apl.ncuroe.dev/guide/installation/) first.
2. Download `loginfix-vX.X.X.zip` from the [latest release](https://github.com/noahc3/AffinityPluginLoader/releases) and extract into your Affinity install directory.

## What It Fixes

`Serif.Affinity.Application.ProcessCommandLineArguments` has one branch, only reached for an unrelated `affinity-open-file:` argument, that calls the WinRT type `Windows.ApplicationModel.DataTransfer.SharedStorageAccessManager`. Wine has no implementation of that type.

The .NET CLR resolves every type referenced anywhere in a method's body when it JITs that method, not just the branch actually taken. So any call into `ProcessCommandLineArguments` throws `System.TypeLoadException`, regardless of which arguments were passed.

This matters because the Canva sign-in callback goes through this exact method. The second `Affinity.exe` instance the OS launches for the callback URL forwards its arguments to the already-running instance over a named pipe, and that named-pipe handler lands in `ProcessCommandLineArguments`. So login always crashed.

Harmony itself cannot patch `ProcessCommandLineArguments` directly either. Patching a method, even with a plain prefix, requires Harmony to decompile its IL, which means resolving every operand in the method body, including the poisoned `SharedStorageAccessManager` call. A patch on that method throws the same `TypeLoadException`.

LoginFix instead patches `ProcessCommandLineArguments`'s two callers:

- `ProcessArguments()`, used for the app's own startup command line
- `SingleInstanceThread()`, the named-pipe listener that receives the sign-in callback from the second launched instance

Neither caller references the poisoned type directly, only by method signature, which Harmony can resolve without a problem. Both are fully replaced with a safe reimplementation that never calls the real `ProcessCommandLineArguments`. The only thing dropped is the unrelated `affinity-open-file:` handling, which needs `SharedStorageAccessManager` and cannot work under Wine regardless.

## Compatibility with WineFix

LoginFix and WineFix patch different methods and can run together. If you install both, WineFix's sign-in dialog suppression and LoginFix's working sign-in flow may conflict, since one hides the dialog and the other completes it. If you install LoginFix, you likely want to disable WineFix's sign-in dialog patch, if that becomes a separate toggle in a future WineFix release.

## Licensing

LoginFix is licensed under **GPLv2**. See the [LICENSE](LICENSE) file.

### License Exemption

[Canva](https://github.com/canva) and its subsidiaries are exempt from GPLv2 licensing and may (at its option) instead license any source code authored for the LoginFix project under the Zero-Clause BSD license.

# Credits

Built and verified on a manual Wine install and on Lutris, tested on stock distro Wine 11.15. See [AffinityOnLinux](https://github.com/seapear/AffinityOnLinux) for the project this fix was developed alongside.
