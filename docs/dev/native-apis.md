# Native Code APIs

APL provides two utilities in `AffinityPluginLoader.Native` for patching native (unmanaged) code at runtime. These complement Harmony's .NET IL patching with the ability to hook COM interfaces and patch native DLLs in memory.

!!! warning "Unstable API"
    APL is experimental. While APL is still v0, expect breaking changes to the plugin API between updates.

## ComHook

Hook COM interface methods by patching vtable entries. COM vtables are shared across all instances of a given implementation, so hooking one object's vtable affects all objects of that type.

### ComHook.Hook

Replace a COM vtable entry with a managed delegate. Returns a delegate wrapping the original native method for call-through.

```csharp
using AffinityPluginLoader.Native;

// Delegate must have [UnmanagedFunctionPointer] and take IntPtr as first param (COM 'this')
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate void MyMethodFn(IntPtr self, int arg1, float arg2);

// Hook vtable entry — returns the original method as a callable delegate
MyMethodFn original = ComHook.Hook<MyMethodFn>(comObjectPtr, vtableIndex, new MyMethodFn(MyHook));

static void MyHook(IntPtr self, int arg1, float arg2)
{
    // Custom logic before/after/instead of the original
    original(self, arg1, arg2); // call through to original
}
```

**Parameters:**

| Parameter | Description |
|---|---|
| `comObject` | `IntPtr` to any COM instance using the target vtable |
| `vtableIndex` | Zero-based method index in the vtable |
| `hook` | Replacement delegate (kept alive internally to prevent GC) |

**Returns:** A delegate wrapping the original native method.

**Notes:**

- COM vtable indices are defined by the interface IDL and are stable across all implementations and versions. Count from `IUnknown` (indices 0–2: QueryInterface, AddRef, Release), then the interface's own methods in declaration order.
- The hook delegate is pinned internally — you don't need to prevent it from being garbage collected.
- `VirtualProtect` is called automatically to make the vtable writable during patching.

### ComHook.GetMethod

Read a COM vtable entry as a callable delegate without modifying the vtable.

```csharp
MyMethodFn method = ComHook.GetMethod<MyMethodFn>(comObjectPtr, vtableIndex);
method(comObjectPtr, 42, 3.14f); // call the native method directly
```

This is useful for reading methods you want to call but not hook, or for capturing the original function pointer before installing hooks on other entries.

### Example: Hooking a D2D1 COM Interface

```csharp
// Create a COM object to discover the vtable
int hr = D2D1CreateFactory(0, IID_ID2D1Factory, IntPtr.Zero, out IntPtr factory);

// Read a method without hooking (ID2D1Factory::CreatePathGeometry = index 10)
var createGeometry = ComHook.GetMethod<CreatePathGeometryFn>(factory, 10);

// Hook a method (ID2D1GeometrySink::AddBezier = index 11)
originalAddBezier = ComHook.Hook<AddBezierFn>(sinkPtr, 11, new AddBezierFn(MyAddBezier));
```

## NativePatch

Utilities for patching native DLLs in memory at runtime. Handles PE header parsing, section lookup, pattern scanning, and memory protection changes.

### NativePatch.TryGetSection

Get the start address and size of a named section in a loaded module.

```csharp
if (NativePatch.TryGetSection("d2d1", ".text", out IntPtr start, out int size))
{
    // start = memory address of .text section
    // size = section size in bytes
    // Use for custom scanning logic
}
```

**Parameters:**

| Parameter | Description |
|---|---|
| `moduleName` | DLL name without extension (e.g. `"d2d1"`, `"kernel32"`) |
| `sectionName` | PE section name (e.g. `".text"`, `".rdata"`, `".data"`) |
| `start` | (out) Start address of the section in memory |
| `size` | (out) Size of the section in bytes |

**Returns:** `true` if the module is loaded and the section was found.

### NativePatch.Patch

Scan a module section for a byte pattern and replace the first match.

```csharp
// Find and replace an exact byte sequence
IntPtr site = NativePatch.Patch("d2d1", ".text",
    pattern:     new byte[] { 0xf3, 0x0f, 0x10, 0x05 },
    replacement: new byte[] { 0x0f, 0x57, 0xc0, 0x90 });

// With wildcard mask (0xFF = must match, 0x00 = wildcard)
IntPtr site = NativePatch.Patch("mylib", ".text",
    pattern:     new byte[] { 0x48, 0x8b, 0x00, 0x10 },
    replacement: new byte[] { 0x48, 0x31, 0xc0, 0x90 },
    mask:        new byte[] { 0xff, 0xff, 0x00, 0xff });
```

**Parameters:**

| Parameter | Description |
|---|---|
| `moduleName` | DLL name |
| `sectionName` | Section to scan |
| `pattern` | Byte pattern to find |
| `replacement` | Bytes to write at the match site |
| `mask` | Optional mask (same length as pattern). `0xFF` = must match, `0x00` = wildcard. |

**Returns:** The address where the patch was applied, or `IntPtr.Zero` if the pattern was not found.

**Notes:**

- `VirtualProtect` is called automatically to make the memory writable during patching and restored afterward.
- Only the first match is patched. If no match is found, nothing is modified and `IntPtr.Zero` is returned.
- For complex scan logic (e.g. following RIP-relative offsets), use `TryGetSection` and implement your own scanner.

### Example: Patching a Float Constant Load

```csharp
// Replace `movss xmm0, [25.0f]` with `xorps xmm0, xmm0` (loads 0.0)
// This is a simplified example — real code may need RIP-relative resolution
NativePatch.Patch("d2d1", ".text",
    pattern:     new byte[] { 0xf3, 0x0f, 0x10, 0x05 },  // movss xmm0, [rip+...]
    replacement: new byte[] { 0x0f, 0x57, 0xc0, 0x90 },   // xorps xmm0, xmm0 + nop
    mask:        new byte[] { 0xff, 0xff, 0xff, 0xff });
```

## When to Use Which API

| Scenario | API |
|---|---|
| Hook a COM interface method (D2D1, DirectWrite, DXGI, etc.) | `ComHook.Hook` |
| Call a COM method without hooking it | `ComHook.GetMethod` |
| Detour a native function by address (inline hook) | `NativeHook.Hook` |
| Patch a specific byte pattern in a native DLL | `NativePatch.Patch` |
| Custom scanning of a native DLL's memory | `NativePatch.TryGetSection` |
| Patch .NET methods (Affinity's managed code) | [Harmony](creating-a-plugin.md#patching-with-harmony) |

## NativeHook

Inline function detouring for native code. Overwrites a function's prologue with a jump to a managed delegate, and creates an executable trampoline so the hook can call through to the original function.

Supports two prologue sizes:

- **Large (≥ 12 bytes):** Direct absolute jump (`mov rax, imm64; jmp rax`) at the target site.
- **Small (≥ 5 bytes):** Relative jump (`jmp rel32`) to a nearby relay thunk allocated within ±2GB of the target.

### NativeHook.Hook

Detour a native function at a given address.

```csharp
using AffinityPluginLoader.Native;

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate int MyFuncFn(IntPtr arg1, IntPtr arg2);

// Hook the function — returns a delegate that calls the original
MyFuncFn original = NativeHook.Hook<MyFuncFn>(funcAddress, prologueSize, new MyFuncFn(MyHook));

static int MyHook(IntPtr arg1, IntPtr arg2)
{
    // Custom logic — can call original() to pass through
    return original(arg1, arg2);
}
```

**Parameters:**

| Parameter | Description |
|---|---|
| `target` | `IntPtr` address of the function to hook |
| `prologueSize` | Number of bytes to overwrite (≥ 5). Must end on an instruction boundary. |
| `hook` | Replacement delegate (kept alive internally to prevent GC) |

**Returns:** A delegate wrapping the original function (via trampoline).

!!! warning "Prologue constraints"
    - `prologueSize` must be ≥ 5 (for relative jump) or ≥ 12 (for absolute jump).
    - The overwritten bytes must end on an instruction boundary.
    - The relocated bytes **must not** contain RIP-relative instructions — choose a prologue size that avoids them.
    - The target function must not be executing on any thread when hooked.

### Example: Hooking a Non-Exported Function by Pattern Scan

```csharp
// Find the function by scanning for its unique prologue bytes
if (NativePatch.TryGetSection("d2d1", ".text", out IntPtr start, out int size))
{
    byte[] pattern = { 0x56, 0x53, 0x48, 0x83, 0xEC, 0x48 }; // push rsi; push rbx; sub rsp,0x48
    IntPtr funcAddr = ScanForPattern(start, size, pattern);

    // Hook with 6-byte prologue (uses relative jump + nearby relay)
    original = NativeHook.Hook<MyFuncFn>(funcAddr, 6, new MyFuncFn(MyHook));
}
```
