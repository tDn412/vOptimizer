using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using vOptimizer.Tweaks;

namespace vOptimizer.Core
{
    /// <summary>
    /// Monitors Windows process creations using WMI event watchers and optimizes game processes dynamically.
    /// </summary>
    public class ProcessMonitor
    {
        private ManagementEventWatcher? _watcher;
        private readonly List<string> _targetGames;
        private readonly bool _isEsportsMode;

        private static readonly string[] BackgroundApps = {
            "chrome", "msedge", "firefox", "discord", "steamwebhelper", "epicgameslauncher", "galaxyclient"
        };

        public event Action<string, Process>? GameDetected;

        public ProcessMonitor(List<string> targetGames, bool isEsportsMode)
        {
            _targetGames = targetGames ?? new List<string>();
            _isEsportsMode = isEsportsMode;
        }

        /// <summary>
        /// Starts listening for process creation events using WMI.
        /// </summary>
        public void Start()
        {
            try
            {
                Stop(); // Ensure any previous watcher is closed

                // Watcher query that fires every 1 second when a new process is created
                string query = "SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'";
                
                _watcher = new ManagementEventWatcher(new WqlEventQuery(query));
                _watcher.EventArrived += OnProcessStarted;
                _watcher.Start();

                Debug.WriteLine("[ProcessMonitor] WMI Process Watcher started successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessMonitor] Failed to start WMI watcher: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops the WMI event watcher.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_watcher != null)
                {
                    _watcher.Stop();
                    _watcher.Dispose();
                    _watcher = null;
                }
            }
            catch { }
        }

        private void OnProcessStarted(object sender, EventArrivedEventArgs e)
        {
            try
            {
                // Get the Win32_Process object created
                ManagementBaseObject targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                string processName = targetInstance["Name"]?.ToString() ?? "";
                string processIdStr = targetInstance["ProcessId"]?.ToString() ?? "0";
                
                int pid = int.Parse(processIdStr);
                if (pid <= 0) return;

                // Check if the process name matches any of our target games (case-insensitive)
                bool isMatch = _targetGames.Any(g => processName.Contains(g, StringComparison.OrdinalIgnoreCase));

                if (isMatch)
                {
                    Debug.WriteLine($"[ProcessMonitor] Target process detected: {processName} (PID: {pid})");
                    
                    // Retrieve active process handle and run optimization
                    Process gameProcess = Process.GetProcessById(pid);
                    
                    OptimizeGameProcess(gameProcess);
                    
                    GameDetected?.Invoke(processName, gameProcess);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessMonitor] Error analyzing started process: {ex.Message}");
            }
        }

        /// <summary>
        /// Elevates the process priority, pins threads to physical cores, and configures active OS performance profiles.
        /// </summary>
        private void OptimizeGameProcess(Process process)
        {
            try
            {
                if (process == null || process.HasExited) return;

                // 1. Elevate process priority to High
                if (process.PriorityClass != ProcessPriorityClass.High)
                {
                    process.PriorityClass = ProcessPriorityClass.High;
                    Debug.WriteLine($"[ProcessMonitor] Elevated priority of {process.ProcessName} to High.");
                }

                // 2. CPU Affinity Core Pinning (Bypassing SMT)
                if (_isEsportsMode)
                {
                    int logicalCount = Environment.ProcessorCount;
                    if (logicalCount > 1)
                    {
                        ulong mask = 0;
                        // Select only even logical processors (0, 2, 4, 6, 8, etc.) which correspond
                        // to physical primary CPU cores in modern SMT architectures (like Zen 3/4/5)
                        for (int i = 0; i < logicalCount; i += 2)
                        {
                            mask |= (1UL << i);
                        }

                        process.ProcessorAffinity = (IntPtr)mask;
                        Debug.WriteLine($"[ProcessMonitor] Pinning {process.ProcessName} to physical cores (Affinity mask: {mask:X}).");
                    }
                }

                // 3. Apply active performance power plan (unpark cores, EPP=0)
                PowerPlanManager.SetGamingPowerProfile(true);

                // 4. Request dynamic 0.5ms timer resolution
                TimerResolution.SetHighResolution(true);

                // 5. Demote background apps (Chrome, Discord, etc.)
                DemoteBackgroundProcesses(true);

                // 6. Listen to process exits to automatically revert changes
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    Debug.WriteLine($"[ProcessMonitor] Game process {process.ProcessName} exited. Cleaning up tweaks...");
                    
                    // Revert background app priorities
                    DemoteBackgroundProcesses(false);

                    // Revert power plans (restore core parking & original EPP)
                    PowerPlanManager.SetGamingPowerProfile(false);

                    // Release 0.5ms timer resolution
                    TimerResolution.SetHighResolution(false);
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessMonitor] Could not optimize process {process.ProcessName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Demotes heavy background apps to BelowNormal priority to prevent CPU spikes, or restores them to Normal.
        /// </summary>
        private void DemoteBackgroundProcesses(bool demote)
        {
            foreach (string app in BackgroundApps)
            {
                try
                {
                    Process[] processes = Process.GetProcessesByName(app);
                    foreach (Process p in processes)
                    {
                        try
                        {
                            if (p.HasExited) continue;
                            if (demote)
                            {
                                if (p.PriorityClass == ProcessPriorityClass.Normal)
                                {
                                    p.PriorityClass = ProcessPriorityClass.BelowNormal;
                                    Debug.WriteLine($"[ProcessMonitor] Demoted background process {p.ProcessName} (PID: {p.Id}) to BelowNormal.");
                                }
                            }
                            else
                            {
                                if (p.PriorityClass == ProcessPriorityClass.BelowNormal)
                                {
                                    p.PriorityClass = ProcessPriorityClass.Normal;
                                    Debug.WriteLine($"[ProcessMonitor] Restored background process {p.ProcessName} (PID: {p.Id}) to Normal.");
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
    }
}
