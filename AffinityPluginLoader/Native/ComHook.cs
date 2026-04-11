using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AffinityPluginLoader.Core;

namespace AffinityPluginLoader.Native
{
    /// <summary>
    /// Hooks COM interface methods by patching vtable entries in memory.
    /// COM vtables are shared across all instances of a given implementation,
    /// so hooking one object's vtable affects all objects of that type.
    /// </summary>
    public static class ComHook
    {
        // prevent GC of hook delegates whose function pointers are written into vtables
        private static readonly List<Delegate> _pinnedDelegates = new List<Delegate>();

        /// <summary>
        /// Replace a COM vtable entry with a managed delegate.
        /// Returns a delegate wrapping the original native method.
        /// </summary>
        /// <typeparam name="T">
        /// Delegate type decorated with [UnmanagedFunctionPointer].
        /// First parameter must be IntPtr (the COM 'this' pointer).
        /// </typeparam>
        /// <param name="comObject">Pointer to any COM instance using the target vtable</param>
        /// <param name="vtableIndex">Zero-based method index in the vtable</param>
        /// <param name="hook">Replacement delegate</param>
        /// <returns>Delegate wrapping the original native method</returns>
        public static T Hook<T>(IntPtr comObject, int vtableIndex, T hook) where T : class
        {
            if (comObject == IntPtr.Zero)
                throw new ArgumentNullException(nameof(comObject));
            if (!(hook is Delegate hookDelegate))
                throw new ArgumentException("T must be a delegate type");

            IntPtr vtable = Marshal.ReadIntPtr(comObject);
            IntPtr entryAddr = vtable + vtableIndex * IntPtr.Size;
            IntPtr original = Marshal.ReadIntPtr(entryAddr);

            IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(hookDelegate);

            VirtualProtect(entryAddr, (UIntPtr)IntPtr.Size, PAGE_READWRITE, out uint oldProtect);
            Marshal.WriteIntPtr(entryAddr, hookPtr);
            VirtualProtect(entryAddr, (UIntPtr)IntPtr.Size, oldProtect, out _);

            _pinnedDelegates.Add(hookDelegate);

            Logger.Debug($"ComHook: patched vtable[{vtableIndex}] at 0x{entryAddr.ToInt64():X} " +
                         $"(0x{original.ToInt64():X} -> 0x{hookPtr.ToInt64():X})");

            return Marshal.GetDelegateForFunctionPointer<T>(original);
        }

        /// <summary>
        /// Read a COM vtable entry and return it as a callable delegate without modifying the vtable.
        /// </summary>
        public static T GetMethod<T>(IntPtr comObject, int vtableIndex) where T : class
        {
            if (comObject == IntPtr.Zero)
                throw new ArgumentNullException(nameof(comObject));

            IntPtr vtable = Marshal.ReadIntPtr(comObject);
            IntPtr original = Marshal.ReadIntPtr(vtable + vtableIndex * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer<T>(original);
        }

        private const uint PAGE_READWRITE = 0x04;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize,
            uint flNewProtect, out uint lpflOldProtect);
    }
}
