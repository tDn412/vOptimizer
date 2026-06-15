using System;
using System.Runtime.InteropServices;

namespace vOptimizer.Tweaks
{
    /// <summary>
    /// Wrapper around Windows System Restore APIs to create restore points programmatically before tweaks are applied.
    /// </summary>
    public static class RestorePoint
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RESTOREPOINTINFO
        {
            public int dwEventType;
            public int dwRestorePtType;
            public long llSequenceNumber;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STATEMGRSTATUS
        {
            public uint nStatus;
            public long llSequenceNumber;
        }

        [DllImport("srclient.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SRSetRestorePoint(ref RESTOREPOINTINFO pRestorePtSpec, out STATEMGRSTATUS pStatus);

        // Event types
        private const int BEGIN_SYSTEM_CHANGE = 100;
        private const int END_SYSTEM_CHANGE = 101;

        // Restore point types
        private const int MODIFY_SETTINGS = 12;

        /// <summary>
        /// Programmatically creates a system restore point on Windows.
        /// </summary>
        /// <param name="description">The name of the restore point (e.g., "vOptimizer_PreOptimization")</param>
        /// <returns>True if created successfully; false otherwise.</returns>
        public static bool Create(string description)
        {
            try
            {
                RESTOREPOINTINFO info = new RESTOREPOINTINFO
                {
                    dwEventType = BEGIN_SYSTEM_CHANGE,
                    dwRestorePtType = MODIFY_SETTINGS,
                    llSequenceNumber = 0,
                    szDescription = description
                };

                STATEMGRSTATUS status;
                
                // 1. Initialize restore point creation
                bool success = SRSetRestorePoint(ref info, out status);
                if (success)
                {
                    // 2. Finalize/Commit the restore point
                    RESTOREPOINTINFO commitInfo = new RESTOREPOINTINFO
                    {
                        dwEventType = END_SYSTEM_CHANGE,
                        dwRestorePtType = MODIFY_SETTINGS,
                        llSequenceNumber = status.llSequenceNumber,
                        szDescription = description
                    };
                    
                    return SRSetRestorePoint(ref commitInfo, out status);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RestorePoint] Exception during creation: {ex.Message}");
            }
            return false;
        }
    }
}
