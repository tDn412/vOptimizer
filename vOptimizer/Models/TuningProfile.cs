using System;

namespace vOptimizer.Models
{
    /// <summary>
    /// Holds the configurations for a complete tuning profile, including hardware SMU, FIVR, and OS-level tweaks.
    /// </summary>
    public class TuningProfile
    {
        public string Name { get; set; } = "Default";

        // --- AMD Hardware SMU Settings ---
        public int AmdStapmTdp { get; set; } = 28;     // Watts (Sustained)
        public int AmdSlowTdp { get; set; } = 35;      // Watts (Slow Boost)
        public int AmdFastTdp { get; set; } = 42;      // Watts (Fast Boost)
        public int AmdTempLimit { get; set; } = 85;    // °C Target
        public int AmdCurveOptimizer { get; set; } = 0; // Negative offset (e.g., -15)

        // --- Intel Hardware FIVR Settings ---
        public int IntelCoreOffsetMv { get; set; } = 0;  // Negative Core offset (e.g., -80)
        public int IntelCacheOffsetMv { get; set; } = 0; // Negative Cache/Ring offset
        public int IntelGpuOffsetMv { get; set; } = 0;   // Negative iGPU offset
        public int IntelSaOffsetMv { get; set; } = 0;    // Negative System Agent offset

        // --- Windows OS Tweaks ---
        public bool RegistryTweaksEnabled { get; set; } = false;
        public bool MemoryPurgeEnabled { get; set; } = false;
        
        // --- Process Level Tweaks ---
        public bool ProcessPriorityEnabled { get; set; } = false; // Set game priority to High
        public bool CpuAffinityEnabled { get; set; } = false;      // Pin game to physical cores
    }
}
