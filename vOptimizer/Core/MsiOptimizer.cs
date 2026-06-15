using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace vOptimizer.Core
{
    /// <summary>
    /// Configures GPU and network devices to use Message Signaled Interrupts (MSI) mode instead of line-based interrupts.
    /// This reduces DPC latency and scheduling conflicts under heavy workloads.
    /// </summary>
    public static class MsiOptimizer
    {
        private const string PciPath = @"SYSTEM\CurrentControlSet\Enum\PCI";
        private const string DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";
        private const string NetworkClassGuid = "{4d36e972-e325-11ce-bfc1-08002be10318}";

        /// <summary>
        /// Scans PCI nodes and toggles MSI mode for Display and Network adapters.
        /// </summary>
        /// <param name="enable">If true, enables MSI mode (MSISupported = 1). If false, disables it.</param>
        public static void OptimizeMsiMode(bool enable)
        {
            try
            {
                using (RegistryKey? pciKey = Registry.LocalMachine.OpenSubKey(PciPath, true))
                {
                    if (pciKey == null)
                    {
                        Debug.WriteLine("[MsiOptimizer] Could not access HKLM\\SYSTEM\\CurrentControlSet\\Enum\\PCI.");
                        return;
                    }

                    int count = 0;

                    // 1. Loop through all hardware Device IDs (e.g. VEN_10DE&DEV_25A0...)
                    foreach (string deviceId in pciKey.GetSubKeyNames())
                    {
                        using (RegistryKey? deviceKey = pciKey.OpenSubKey(deviceId, true))
                        {
                            if (deviceKey == null) continue;

                            // 2. Loop through instances of the Device ID
                            foreach (string instanceId in deviceKey.GetSubKeyNames())
                            {
                                using (RegistryKey? instanceKey = deviceKey.OpenSubKey(instanceId, true))
                                {
                                    if (instanceKey == null) continue;

                                    // Check if the device is a Display Card or Network Adapter
                                    string classGuid = instanceKey.GetValue("ClassGUID")?.ToString() ?? "";
                                    if (classGuid.Equals(DisplayClassGuid, StringComparison.OrdinalIgnoreCase) ||
                                        classGuid.Equals(NetworkClassGuid, StringComparison.OrdinalIgnoreCase))
                                    {
                                        string deviceDesc = instanceKey.GetValue("DeviceDesc")?.ToString() ?? "Unknown Device";
                                        
                                        // Clean up description string (removes leading semicolon if present)
                                        if (deviceDesc.Contains(";"))
                                        {
                                            deviceDesc = deviceDesc.Split(';')[1];
                                        }

                                        // 3. Create or open the Interrupt Management path
                                        using (RegistryKey? intMgmt = instanceKey.CreateSubKey(@"Device Parameters\Interrupt Management", true))
                                        {
                                            using (RegistryKey? msiProps = intMgmt.CreateSubKey("MessageSignaledInterruptProperties", true))
                                            {
                                                if (enable)
                                                {
                                                    msiProps.SetValue("MSISupported", 1, RegistryValueKind.DWord);
                                                    Debug.WriteLine($"[MsiOptimizer] Enabled MSI Mode for: {deviceDesc}");
                                                }
                                                else
                                                {
                                                    msiProps.SetValue("MSISupported", 0, RegistryValueKind.DWord);
                                                    Debug.WriteLine($"[MsiOptimizer] Disabled MSI Mode for: {deviceDesc}");
                                                }
                                                count++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    Debug.WriteLine($"[MsiOptimizer] Configured MSI mode on {count} hardware devices.");
                }
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine("[MsiOptimizer] Access denied. vOptimizer must run as Administrator.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MsiOptimizer] Exception: {ex.Message}");
            }
        }
    }
}
