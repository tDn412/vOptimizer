using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace vOptimizer.Core
{
    /// <summary>
    /// Provides low-level system memory purging capabilities to reduce RAM overhead and prevent micro-stutters.
    /// </summary>
    public static class MemoryOptimizer
    {
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern int EmptyWorkingSet(IntPtr hProcess);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtSetSystemInformation(
            int systemInformationClass, 
            IntPtr systemInformation, 
            int systemInformationLength
        );

        private const int SystemMemoryListInformation = 80;
        private const int MemoryPurgeStandbyList = 4;

        /// <summary>
        /// Asynchronously purges background RAM working sets, clears system standby lists, and runs garbage collection.
        /// </summary>
        public static Task PurgeSystemMemoryAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    Debug.WriteLine("[MemoryOptimizer] Starting system memory purge...");

                    // 1. Empty working sets of all idle processes
                    EmptyAllWorkingSets();

                    // 2. Clear Windows system-wide standby cache list
                    ClearStandbyList();

                    // 3. Collect garbage for our own application
                    EmptyWorkingSet(Process.GetCurrentProcess().Handle);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    Debug.WriteLine("[MemoryOptimizer] System memory purge completed.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MemoryOptimizer] Error during purge: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Clears the Windows Standby Cache List using NtSetSystemInformation.
        /// </summary>
        private static void ClearStandbyList()
        {
            try
            {
                int command = MemoryPurgeStandbyList;
                IntPtr pCommand = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(pCommand, command);

                // Call NtSetSystemInformation to clear the standby cache
                uint result = NtSetSystemInformation(SystemMemoryListInformation, pCommand, sizeof(int));
                
                Marshal.FreeHGlobal(pCommand);
                
                if (result == 0)
                {
                    Debug.WriteLine("[MemoryOptimizer] Cleared Windows Standby List successfully.");
                }
                else
                {
                    Debug.WriteLine($"[MemoryOptimizer] NtSetSystemInformation returned status code: {result:X}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryOptimizer] Failed to clear Standby List: {ex.Message}");
            }
        }

        /// <summary>
        /// Iterates through all running processes and empties their physical RAM working sets.
        /// </summary>
        private static void EmptyAllWorkingSets()
        {
            Process currentProc = Process.GetCurrentProcess();
            int count = 0;

            foreach (Process proc in Process.GetProcesses())
            {
                try
                {
                    // Skip system processes and our own process
                    if (proc.Id == currentProc.Id || proc.Id <= 4) continue;
                    
                    // Call EmptyWorkingSet to page out unused physical memory pages
                    if (EmptyWorkingSet(proc.Handle) != 0)
                    {
                        count++;
                    }
                }
                catch
                {
                    // Ignore processes we do not have rights to modify (protected / anti-cheat)
                }
            }
            Debug.WriteLine($"[MemoryOptimizer] Emptied working sets for {count} background processes.");
        }
    }
}
