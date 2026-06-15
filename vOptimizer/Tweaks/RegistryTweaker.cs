using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace vOptimizer.Tweaks
{
    /// <summary>
    /// Manages low-level Windows Registry tweaks to decrease OS scheduling latency and gaming network ping.
    /// </summary>
    public static class RegistryTweaker
    {
        private const string SystemProfilePath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string TcpipInterfacesPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

        /// <summary>
        /// Applies or restores low-level latency and network settings.
        /// </summary>
        /// <param name="enable">If true, tweaks are applied; if false, stock Windows defaults are restored.</param>
        public static void ApplyLatencyTweaks(bool enable)
        {
            try
            {
                // 1. System Responsiveness & Network Throttling Index
                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(SystemProfilePath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            // 0% CPU reserved for background tasks under heavy loads (Default is 20%)
                            key.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                            
                            // Tắt bóp băng thông mạng khi CPU chịu tải nặng (0xffffffff)
                            key.SetValue("NetworkThrottlingIndex", unchecked((int)0xffffffff), RegistryValueKind.DWord);
                            
                            Debug.WriteLine("[RegistryTweaker] Applied SystemResponsiveness = 0 & NetworkThrottlingIndex = Disabled.");
                        }
                        else
                        {
                            // Restore defaults
                            key.SetValue("SystemResponsiveness", 20, RegistryValueKind.DWord);
                            key.SetValue("NetworkThrottlingIndex", 10, RegistryValueKind.DWord);
                            
                            Debug.WriteLine("[RegistryTweaker] Restored stock SystemResponsiveness & NetworkThrottlingIndex.");
                        }
                    }
                }

                // 2. TCP Tweaks (Disabling Nagle's Algorithm for lower ping)
                using (RegistryKey? interfacesKey = Registry.LocalMachine.OpenSubKey(TcpipInterfacesPath, true))
                {
                    if (interfacesKey != null)
                    {
                        int interfaceCount = 0;
                        foreach (string subkeyName in interfacesKey.GetSubKeyNames())
                        {
                            try
                            {
                                using (RegistryKey? interfaceKey = interfacesKey.OpenSubKey(subkeyName, true))
                                {
                                    if (interfaceKey != null)
                                    {
                                        if (enable)
                                        {
                                            // Send ACK replies instantly (disable delayed ACKs)
                                            interfaceKey.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                                            // Send TCP segments instantly (disable Nagle's bundling)
                                            interfaceKey.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                                            interfaceCount++;
                                        }
                                        else
                                        {
                                            // Delete custom values to fall back to Windows defaults
                                            interfaceKey.DeleteValue("TcpAckFrequency", false);
                                            interfaceKey.DeleteValue("TCPNoDelay", false);
                                            interfaceCount++;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Skip read-only or locked adapters
                            }
                        }

                        Debug.WriteLine(enable
                            ? $"[RegistryTweaker] Applied TCP Ack/NoDelay to {interfaceCount} network interfaces."
                            : $"[RegistryTweaker] Restored TCP default state on {interfaceCount} network interfaces.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RegistryTweaker] Error writing to registry: {ex.Message}");
            }
        }
    }
}
