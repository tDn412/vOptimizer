using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;

namespace vOptimizer.Core
{
    /// <summary>
    /// Executes clean, multi-threaded CPU stress calculations to evaluate and score system performance.
    /// Monitors temperature and power draw via LibreHardwareMonitor.
    /// </summary>
    public class BenchmarkEngine
    {
        private bool _isRunning = false;
        private readonly Computer _computer;

        public struct BenchmarkResult
        {
            public double ScoreGops { get; set; }      // Giga-Operations Per Second
            public double AverageTemp { get; set; }    // Average temperature under load (°C)
            public double PeakTemp { get; set; }       // Peak temperature under load (°C)
            public double AveragePower { get; set; }   // Average CPU power package under load (W)
            public double TempRiseRate { get; set; }   // Temperature rise rate (°C/sec)
        }

        public BenchmarkEngine()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = false // CPU is the primary benchmark focus
            };
        }

        /// <summary>
        /// Runs the built-in benchmark asynchronously for the specified duration.
        /// </summary>
        public async Task<BenchmarkResult> RunBenchmarkAsync(int durationSeconds)
        {
            if (_isRunning) throw new InvalidOperationException("Benchmark is already running.");
            _isRunning = true;

            long totalOperations = 0;
            CancellationTokenSource cts = new CancellationTokenSource();
            
            try
            {
                _computer.Open();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BenchmarkEngine] Failed to open hardware monitor: {ex.Message}");
            }

            double initialTemp = ReadCpuTemperature();
            Stopwatch sw = Stopwatch.StartNew();

            // 1. Launch computational load across all CPU cores
            int threadCount = Environment.ProcessorCount;
            Task[] stressTasks = new Task[threadCount];

            for (int t = 0; t < threadCount; t++)
            {
                stressTasks[t] = Task.Run(() =>
                {
                    long localOps = 0;
                    double x = 1.5;
                    while (!cts.Token.IsCancellationRequested)
                    {
                        // Perform intensive floating-point and integer math
                        x = Math.Sqrt(x) * 1.0000001;
                        x = Math.Sin(x) + 0.0000001;
                        localOps += 20; // 20 estimated operations per cycle
                    }
                    Interlocked.Add(ref totalOperations, localOps);
                });
            }

            // 2. Poll hardware sensors every 500ms during the test
            double tempSum = 0;
            double powerSum = 0;
            double peakTemp = 0;
            int samples = 0;

            while (sw.Elapsed.TotalSeconds < durationSeconds)
            {
                await Task.Delay(500);

                double temp = ReadCpuTemperature();
                double power = ReadCpuPower();

                tempSum += temp;
                powerSum += power;
                if (temp > peakTemp) peakTemp = temp;
                samples++;
            }

            // Stop the load
            cts.Cancel();
            await Task.WhenAll(stressTasks);
            sw.Stop();
            
            try
            {
                _computer.Close();
            }
            catch { }

            _isRunning = false;

            // Compute metrics
            double duration = sw.Elapsed.TotalSeconds;
            double avgTemp = samples > 0 ? tempSum / samples : 45.0;
            double avgPower = samples > 0 ? powerSum / samples : 15.0;
            double gops = (totalOperations / duration) / 1000000000.0;
            double riseRate = (peakTemp - initialTemp) / duration;

            return new BenchmarkResult
            {
                ScoreGops = Math.Round(gops, 2),
                AverageTemp = Math.Round(avgTemp, 1),
                PeakTemp = Math.Round(peakTemp, 1),
                AveragePower = Math.Round(avgPower, 1),
                TempRiseRate = Math.Round(riseRate, 2)
            };
        }

        private double ReadCpuTemperature()
        {
            try
            {
                double tempSum = 0;
                int count = 0;

                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Core"))
                        {
                            tempSum += sensor.Value ?? 0;
                            count++;
                        }
                    }
                }

                if (count > 0) return tempSum / count;
            }
            catch { }
            return 45.0; // default idle fallback
        }

        private double ReadCpuPower()
        {
            try
            {
                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("Package"))
                        {
                            return sensor.Value ?? 0;
                        }
                    }
                }
            }
            catch { }
            return 15.0; // default fallback
        }
    }
}
