using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace vOptimizer.Tweaks
{
    /// <summary>
    /// Adjusts system timer resolution via NT APIs to decrease input lag and improve frame pacing during game sessions.
    /// </summary>
    public static class TimerResolution
    {
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetTimerResolution(uint desiredResolution, bool setResolution, out uint actualResolution);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQueryTimerResolution(out uint maximumResolution, out uint minimumResolution, out uint currentResolution);

        private static bool _isTuned = false;

        /// <summary>
        /// Requests 0.5ms timer resolution when true, and releases it back to Windows defaults when false.
        /// </summary>
        public static void SetHighResolution(bool enable)
        {
            try
            {
                if (enable)
                {
                    if (!_isTuned)
                    {
                        // NtSetTimerResolution expects values in 100ns units.
                        // 5000 units = 5000 * 100ns = 500,000ns = 500us = 0.5ms
                        int result = NtSetTimerResolution(5000, true, out uint actualResolution);
                        
                        if (result == 0) // STATUS_SUCCESS
                        {
                            _isTuned = true;
                            Debug.WriteLine($"[TimerResolution] System timer resolution locked at {actualResolution / 10000.0:F3} ms (Requested 0.500 ms).");
                        }
                        else
                        {
                            Debug.WriteLine($"[TimerResolution] NtSetTimerResolution failed with status: {result:X}");
                        }
                    }
                }
                else
                {
                    if (_isTuned)
                    {
                        // Calling NtSetTimerResolution with false releases the request, restoring Windows defaults
                        NtSetTimerResolution(5000, false, out uint actualResolution);
                        _isTuned = false;
                        Debug.WriteLine($"[TimerResolution] Released timer resolution setting. System default restored ({actualResolution / 10000.0:F3} ms).");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TimerResolution] Exception encountered: {ex.Message}");
            }
        }
    }
}
