using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace vOptimizer.Tweaks
{
    /// <summary>
    /// Interfaces with the Win32 Power Plan API to adjust CPU Core Parking and Energy Preference settings dynamically.
    /// </summary>
    public static class PowerPlanManager
    {
        [DllImport("powrprof.dll", EntryPoint = "PowerGetActiveScheme")]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("powrprof.dll", EntryPoint = "PowerWriteACValueIndex")]
        private static extern uint PowerWriteACValueIndex(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            uint AcValueIndex
        );

        [DllImport("powrprof.dll", EntryPoint = "PowerReadACValueIndex")]
        private static extern uint PowerReadACValueIndex(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            out uint AcValueIndex
        );

        [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
        private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid ActivePolicyGuid);

        // Power Settings GUIDs
        private static readonly Guid GUID_PROCESSOR_SETTINGS_SUBGROUP = new Guid("54533251-50e3-4f7b-9945-4739174640f4");
        private static readonly Guid GUID_PROCESSOR_CORE_PARKING_MIN_CORES = new Guid("0cc5b647-c36e-4d5d-940f-782f91a88010");
        private static readonly Guid GUID_PROCESSOR_CORE_PARKING_MAX_CORES = new Guid("ea06c951-1d8c-47a4-aa90-07155a75b255");
        private static readonly Guid GUID_PROCESSOR_ENERGY_PERFORMANCE_PREFERENCE = new Guid("3668a663-3452-4fd7-861d-793434d48557");

        // Saved original values for restoring
        private static uint _originalMinCores = 10;
        private static uint _originalMaxCores = 100;
        private static uint _originalEpp = 50;
        private static bool _originalsSaved = false;

        /// <summary>
        /// Configure CPU performance settings (unpark all cores, set EPP to maximum performance).
        /// </summary>
        public static void SetGamingPowerProfile(bool enableGamingMode)
        {
            try
            {
                // 1. Get the current active power scheme GUID
                uint result = PowerGetActiveScheme(IntPtr.Zero, out IntPtr activeGuidPtr);
                if (result != 0 || activeGuidPtr == IntPtr.Zero)
                {
                    Debug.WriteLine("[PowerPlanManager] Failed to read active power scheme.");
                    return;
                }

                Guid activeSchemeGuid = Marshal.PtrToStructure<Guid>(activeGuidPtr);
                Marshal.FreeHGlobal(activeGuidPtr); // Free the memory allocated by the API

                if (enableGamingMode)
                {
                    // 2. Save original values before writing new ones
                    if (!_originalsSaved)
                    {
                        PowerReadACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref GUID_PROCESSOR_SETTINGS_SUBGROUP, ref GUID_PROCESSOR_CORE_PARKING_MIN_CORES, out _originalMinCores);
                        PowerReadACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref GUID_PROCESSOR_SETTINGS_SUBGROUP, ref GUID_PROCESSOR_CORE_PARKING_MAX_CORES, out _originalMaxCores);
                        PowerReadACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref GUID_PROCESSOR_SETTINGS_SUBGROUP, ref GUID_PROCESSOR_ENERGY_PERFORMANCE_PREFERENCE, out _originalEpp);
                        _originalsSaved = true;
                        Debug.WriteLine($"[PowerPlanManager] Saved original settings: MinCores={_originalMinCores}%, MaxCores={_originalMaxCores}%, EPP={_originalEpp}");
                    }

                    // 3. Write maximum performance values (100% min/max cores active = no core parking, EPP = 0 = full performance preference)
                    PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref GUID_PROCESSOR_SETTINGS_SUBGROUP, ref GUID_PROCESSOR_CORE_PARKING_MIN_CORES, 100);
                    PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref GUID_PROCESSOR_SETTINGS_SUBGROUP, ref GUID_PROCESSOR_CORE_PARKING_MAX_CORES, 100);
                    PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref GUID_PROCESSOR_SETTINGS_SUBGROUP, ref GUID_PROCESSOR_ENERGY_PERFORMANCE_PREFERENCE, 0);
                    
                    // 4. Flush changes by setting active scheme again
                    PowerSetActiveScheme(IntPtr.Zero, ref activeSchemeGuid);
                    Debug.WriteLine("[PowerPlanManager] CPU Core Parking disabled & EPP set to 0 (Full Performance).");
                }
                else
                {
                    // 5. Restore original settings
                    if (_originalsSaved)
                    {
                        PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref GUID_PROCESSOR_SETTINGS_SUBGROUP, ref GUID_PROCESSOR_CORE_PARKING_MIN_CORES, _originalMinCores);
                        PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref GUID_PROCESSOR_SETTINGS_SUBGROUP, ref GUID_PROCESSOR_CORE_PARKING_MAX_CORES, _originalMaxCores);
                        PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref GUID_PROCESSOR_SETTINGS_SUBGROUP, ref GUID_PROCESSOR_ENERGY_PERFORMANCE_PREFERENCE, _originalEpp);
                        
                        PowerSetActiveScheme(IntPtr.Zero, ref activeSchemeGuid);
                        _originalsSaved = false;
                        Debug.WriteLine("[PowerPlanManager] CPU Power Plan settings restored to defaults.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PowerPlanManager] Exception encountered: {ex.Message}");
            }
        }
    }
}
