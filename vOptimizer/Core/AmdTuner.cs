using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace vOptimizer.Core
{
    /// <summary>
    /// Interfaces with RyzenAdj to tune AMD Ryzen CPU/APU settings such as TDP, temperatures, and Curve Optimizer.
    /// </summary>
    public static class AmdTuner
    {
        private const string RyzenAdjExe = "ryzenadj.exe";

        /// <summary>
        /// Applies the specified tuning parameters to the AMD Ryzen processor.
        /// </summary>
        /// <param name="stapmWatts">Sustained power limit (STAPM) in Watts</param>
        /// <param name="slowWatts">Slow power limit in Watts</param>
        /// <param name="fastWatts">Fast power limit in Watts</param>
        /// <param name="tempLimit">Target temperature ceiling in °C</param>
        /// <param name="curveOptimizer">Negative Curve Optimizer offset (e.g., -20 for -20 CO)</param>
        /// <param name="gfxClkMhz">GPU clock limit in MHz (optional, 0 to ignore)</param>
        public static async Task<bool> ApplySettingsAsync(int stapmWatts, int slowWatts, int fastWatts, int tempLimit, int curveOptimizer, int gfxClkMhz = 0)
        {
            try
            {
                // Convert Curve Optimizer negative offset (two's complement in hex for SMU)
                // -20 translates to 0x100000 - 20 = 0xFFFFEC
                uint coValue = 0;
                if (curveOptimizer != 0)
                {
                    coValue = 0x100000 - (uint)Math.Abs(curveOptimizer);
                }

                // Format RyzenAdj arguments
                string arguments = $"--stapm-limit={stapmWatts * 1000} " +
                                   $"--slow-limit={slowWatts * 1000} " +
                                   $"--fast-limit={fastWatts * 1000} " +
                                   $"--tctl-temp={tempLimit} ";

                if (coValue > 0)
                {
                    arguments += $"--set-coall={coValue} ";
                }

                if (gfxClkMhz > 0)
                {
                    arguments += $"--gfx-clk={gfxClkMhz} ";
                }

                return await ExecuteRyzenAdjAsync(arguments);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Executes RyzenAdj CLI in a hidden process with administrator rights.
        /// </summary>
        private static Task<bool> ExecuteRyzenAdjAsync(string arguments)
        {
            return Task.Run(() =>
            {
                try
                {
                    string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RyzenAdjExe);
                    
                    // If ryzenadj.exe is not directly in the base directory, check one folder up (for debug setups)
                    if (!File.Exists(exePath))
                    {
                        exePath = RyzenAdjExe; // Fallback to system PATH
                    }

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        Verb = "runas" // Request elevated privileges
                    };

                    using (Process? process = Process.Start(psi))
                    {
                        if (process == null) return false;

                        process.WaitForExit(5000); // Wait up to 5 seconds
                        
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();
                        
                        Debug.WriteLine($"[AmdTuner] Output: {output}");
                        if (!string.IsNullOrEmpty(error))
                        {
                            Debug.WriteLine($"[AmdTuner] Error: {error}");
                        }

                        return process.ExitCode == 0;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AmdTuner] Exception: {ex.Message}");
                    return false;
                }
            });
        }
    }
}
