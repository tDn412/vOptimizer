using System;
using System.Runtime.InteropServices;

namespace vOptimizer.Core
{
    /// <summary>
    /// Wrapper around WinRing0 kernel driver for low-level register and memory access.
    /// </summary>
    public static class WinRing0
    {
        private const string DllName = "WinRing0x64.dll";

        [DllImport(DllName, EntryPoint = "InitializeOls")]
        public static extern bool InitializeOls();

        [DllImport(DllName, EntryPoint = "DeinitializeOls")]
        public static extern void DeinitializeOls();

        [DllImport(DllName, EntryPoint = "GetDllStatus")]
        public static extern uint GetDllStatus();

        [DllImport(DllName, EntryPoint = "Rdmsr")]
        public static extern bool Rdmsr(uint index, out uint eax, out uint edx);

        [DllImport(DllName, EntryPoint = "Wrmsr")]
        public static extern bool Wrmsr(uint index, uint eax, uint edx);
    }
}
