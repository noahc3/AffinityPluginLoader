using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AffinityPluginLoader.Core;

namespace AffinityPluginLoader.Native
{
    /// <summary>
    /// Inline function detouring for native code. Overwrites a function's prologue
    /// with a jump to a managed delegate, and creates an executable trampoline
    /// containing the original prologue bytes so the hook can call through to the
    /// original function.
    ///
    /// Supports two modes:
    ///   - Large prologue (>= 12 bytes): direct absolute jump at target site
    ///   - Small prologue (>= 5 bytes): relative jump to a nearby relay thunk
    ///
    /// IMPORTANT: prologueSize must end on an instruction boundary, and the
    /// relocated bytes must not contain RIP-relative instructions (use a larger
    /// or smaller prologue to avoid them).
    /// </summary>
    public static class NativeHook
    {
        private const int AbsJmpSize = 12; // mov rax, imm64 (10) + jmp rax (2)
        private const int RelJmpSize = 5;  // jmp rel32
        private static readonly List<Delegate> _pinnedDelegates = new();
        private static readonly List<IntPtr> _allocations = new();

        /// <summary>
        /// Hook a native function at the given address.
        /// </summary>
        /// <typeparam name="T">Delegate type with [UnmanagedFunctionPointer]</typeparam>
        /// <param name="target">Address of the function to hook</param>
        /// <param name="prologueSize">
        /// Number of bytes to overwrite. Must be >= 5 and end on an instruction boundary.
        /// The overwritten bytes must not contain RIP-relative instructions.
        /// </param>
        /// <param name="hook">Replacement delegate</param>
        /// <returns>Delegate that calls the original function</returns>
        public static T Hook<T>(IntPtr target, int prologueSize, T hook) where T : class
        {
            if (target == IntPtr.Zero)
                throw new ArgumentNullException(nameof(target));
            if (prologueSize < RelJmpSize)
                throw new ArgumentException($"prologueSize must be >= {RelJmpSize}");
            if (!(hook is Delegate hookDel))
                throw new ArgumentException("T must be a delegate type");

            IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(hookDel);

            // Allocate trampoline (for calling original): prologue + absolute jump back
            int trampolineSize = prologueSize + AbsJmpSize;
            IntPtr trampoline = VirtualAlloc(IntPtr.Zero, (UIntPtr)trampolineSize,
                MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
            if (trampoline == IntPtr.Zero)
                throw new OutOfMemoryException("VirtualAlloc failed for trampoline");

            // Copy original prologue to trampoline + jump back
            byte[] originalBytes = new byte[prologueSize];
            Marshal.Copy(target, originalBytes, 0, prologueSize);
            Marshal.Copy(originalBytes, 0, trampoline, prologueSize);
            WriteAbsoluteJump(trampoline + prologueSize, target + prologueSize);

            // Overwrite target prologue
            VirtualProtect(target, (UIntPtr)prologueSize, PAGE_EXECUTE_READWRITE, out uint oldProtect);

            if (prologueSize >= AbsJmpSize)
            {
                // Direct absolute jump to hook
                WriteAbsoluteJump(target, hookPtr);
            }
            else
            {
                // Allocate relay near target for absolute jump, use rel32 at target
                IntPtr relay = AllocateNear(target, AbsJmpSize);
                if (relay == IntPtr.Zero)
                    throw new OutOfMemoryException("Failed to allocate relay near target");
                WriteAbsoluteJump(relay, hookPtr);
                WriteRelativeJump(target, relay);
            }

            // NOP remaining bytes
            for (int i = (prologueSize >= AbsJmpSize ? AbsJmpSize : RelJmpSize); i < prologueSize; i++)
                Marshal.WriteByte(target + i, 0x90);

            VirtualProtect(target, (UIntPtr)prologueSize, oldProtect, out _);

            _pinnedDelegates.Add(hookDel);
            _allocations.Add(trampoline);

            T original = Marshal.GetDelegateForFunctionPointer<T>(trampoline);

            Logger.Debug($"NativeHook: detoured 0x{target.ToInt64():X} -> 0x{hookPtr.ToInt64():X} " +
                         $"(trampoline at 0x{trampoline.ToInt64():X}, {prologueSize} bytes relocated)");

            return original;
        }

        private static unsafe void WriteAbsoluteJump(IntPtr site, IntPtr target)
        {
            byte* p = (byte*)site;
            p[0] = 0x48; p[1] = 0xB8;          // mov rax, imm64
            *(long*)(p + 2) = target.ToInt64();
            p[10] = 0xFF; p[11] = 0xE0;         // jmp rax
        }

        private static unsafe void WriteRelativeJump(IntPtr site, IntPtr target)
        {
            byte* p = (byte*)site;
            p[0] = 0xE9; // jmp rel32
            *(int*)(p + 1) = (int)(target.ToInt64() - site.ToInt64() - 5);
        }

        /// <summary>
        /// Allocate executable memory within ±2GB of the given address.
        /// </summary>
        private static IntPtr AllocateNear(IntPtr target, int size)
        {
            long addr = target.ToInt64();
            long low = Math.Max(addr - 0x7FFF0000L, 0x10000L);
            long high = addr + 0x7FFF0000L;

            // Scan in 64KB increments (allocation granularity)
            for (long a = addr & ~0xFFFFL; a >= low; a -= 0x10000)
            {
                IntPtr result = VirtualAlloc((IntPtr)a, (UIntPtr)size,
                    MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                if (result != IntPtr.Zero)
                {
                    _allocations.Add(result);
                    return result;
                }
            }
            for (long a = (addr + 0x10000) & ~0xFFFFL; a <= high; a += 0x10000)
            {
                IntPtr result = VirtualAlloc((IntPtr)a, (UIntPtr)size,
                    MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                if (result != IntPtr.Zero)
                {
                    _allocations.Add(result);
                    return result;
                }
            }
            return IntPtr.Zero;
        }

        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize,
            uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize,
            uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
    }
}
