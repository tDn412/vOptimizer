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

                // 3. Win32PrioritySeparation (Foreground priority boost)
                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl", true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue("Win32PrioritySeparation", 38, RegistryValueKind.DWord);
                            Debug.WriteLine("[RegistryTweaker] Applied Win32PrioritySeparation = 38.");
                        }
                        else
                        {
                            key.SetValue("Win32PrioritySeparation", 2, RegistryValueKind.DWord);
                            Debug.WriteLine("[RegistryTweaker] Restored Win32PrioritySeparation = 2.");
                        }
                    }
                }

                // 4. Keyboard Repeat Latency Optimization
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard", true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue("KeyboardDelay", "0", RegistryValueKind.String);
                            key.SetValue("KeyboardSpeed", "31", RegistryValueKind.String);
                            Debug.WriteLine("[RegistryTweaker] Applied Keyboard repeat delay = 0 & speed = 31.");
                        }
                        else
                        {
                            key.SetValue("KeyboardDelay", "1", RegistryValueKind.String);
                            key.SetValue("KeyboardSpeed", "31", RegistryValueKind.String);
                            Debug.WriteLine("[RegistryTweaker] Restored Keyboard repeat defaults.");
                        }
                    }
                }

                // 5. Disable Accessibility Hotkeys (StickyKeys, ToggleKeys, FilterKeys/Keyboard Response) to prevent gaming popups
                using (RegistryKey? stickyKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Accessibility\StickyKeys", true))
                using (RegistryKey? keyboardRespKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Accessibility\Keyboard Response", true))
                using (RegistryKey? toggleKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Accessibility\ToggleKeys", true))
                {
                    if (stickyKey != null && keyboardRespKey != null && toggleKey != null)
                    {
                        if (enable)
                        {
                            stickyKey.SetValue("Flags", "510", RegistryValueKind.String);
                            keyboardRespKey.SetValue("Flags", "122", RegistryValueKind.String);
                            toggleKey.SetValue("Flags", "58", RegistryValueKind.String);
                            Debug.WriteLine("[RegistryTweaker] Disabled accessibility keyboard hotkeys.");
                        }
                        else
                        {
                            stickyKey.SetValue("Flags", "506", RegistryValueKind.String);
                            keyboardRespKey.SetValue("Flags", "126", RegistryValueKind.String);
                            toggleKey.SetValue("Flags", "62", RegistryValueKind.String);
                            Debug.WriteLine("[RegistryTweaker] Restored accessibility keyboard hotkeys default flags.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RegistryTweaker] Error writing to registry: {ex.Message}");
            }
        }

        /// <summary>
        /// Disables or restores Windows telemetry policies and diagnostics services to reduce idle CPU background overhead.
        /// </summary>
        public static void ApplyTelemetryTweak(bool disable)
        {
            try
            {
                // Disable telemetry policies in Policies registry paths
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true))
                {
                    key.SetValue("AllowTelemetry", disable ? 0 : 1, RegistryValueKind.DWord);
                }
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", true))
                {
                    key.SetValue("AllowTelemetry", disable ? 0 : 1, RegistryValueKind.DWord);
                }
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Policies\DataCollection", true))
                {
                    key.SetValue("AllowTelemetry", disable ? 0 : 1, RegistryValueKind.DWord);
                }

                Debug.WriteLine($"[RegistryTweaker] Telemetry policies set to {(disable ? "Disabled" : "Enabled")}.");

                // Disable the diagnostics service (DiagTrack) and WAP Push routing service (dmwappushservice)
                if (disable)
                {
                    RunCommand("sc.exe stop DiagTrack");
                    RunCommand("sc.exe config DiagTrack start= disabled");
                    RunCommand("sc.exe stop dmwappushservice");
                    RunCommand("sc.exe config dmwappushservice start= disabled");
                }
                else
                {
                    RunCommand("sc.exe config DiagTrack start= auto");
                    RunCommand("sc.exe start DiagTrack");
                    RunCommand("sc.exe config dmwappushservice start= demand");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RegistryTweaker] Telemetry tweak failed: {ex.Message}");
            }
        }

        private static void RunCommand(string command)
        {
            try
            {
                var parts = command.Split(' ', 2);
                var startInfo = new ProcessStartInfo
                {
                    FileName = parts[0],
                    Arguments = parts.Length > 1 ? parts[1] : "",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(startInfo)?.WaitForExit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RegistryTweaker] RunCommand {command} failed: {ex.Message}");
            }
        }
    }
}
