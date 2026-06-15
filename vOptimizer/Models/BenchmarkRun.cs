using System;

namespace vOptimizer.Models
{
    /// <summary>
    /// Represents the results of a single benchmark execution, stored in history.
    /// </summary>
    public class BenchmarkRun
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string ProfileName { get; set; } = "Stock";
        
        // Performance scores
        public double ScoreGops { get; set; }
        
        // Hardware thermals/power
        public double PeakTemp { get; set; }
        public double AverageTemp { get; set; }
        public double AveragePower { get; set; }
        public double TempRiseRate { get; set; }
        
        // Applied configuration at run time
        public int StapmTdp { get; set; }
        public int CurveOptimizerOffset { get; set; }
        public int IntelVoltageOffset { get; set; }
        public bool RegistryTweaksEnabled { get; set; }
        public bool MemoryPurgeEnabled { get; set; }
    }
}
