using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using vOptimizer.Core;
using vOptimizer.Tweaks;
using LibreHardwareMonitor.Hardware;

namespace vOptimizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Wpf.Ui.Controls.UiWindow
    {
        private readonly MainViewModel _viewModel;
        private readonly DispatcherTimer _sensorTimer;
        private readonly bool _isIntel;
        private readonly Computer _computer;
        private int _sensorTickCount = 0;
        private int _ramCleanTickCount = 0;

        public MainWindow()
        {
            InitializeComponent();
            
            // Detect CPU Brand (Intel vs AMD)
            _isIntel = DetectCpuBrand();

            _viewModel = new MainViewModel(_isIntel);
            DataContext = _viewModel;

            // Initialize WinRing0 driver
            InitializeHardwareDriver();

            // Initialize LibreHardwareMonitor CPU sensor monitor
            _computer = new Computer { IsCpuEnabled = true };
            try
            {
                _computer.Open();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareMonitor] Error opening computer sensors: {ex.Message}");
            }

            // Hook property changed event to watch for Eco Mode changes
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Set up 2-second background timer for temperature/power monitoring and memory updating
            _sensorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _sensorTimer.Tick += SensorTimer_Tick;
            _sensorTimer.Start();

            // Run initial scan of junk files
            UpdateJunkSizeAsync();
        }

        private bool DetectCpuBrand()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString() ?? "";
                        if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CPU Detect] Error: {ex.Message}");
            }
            return false; // Default to AMD/APU if cannot detect
        }

        private void InitializeHardwareDriver()
        {
            try
            {
                if (_isIntel)
                {
                    bool initOk = WinRing0.InitializeOls();
                    if (initOk)
                    {
                        Debug.WriteLine("[Driver] WinRing0 initialized successfully for Intel FIVR.");
                    }
                    else
                    {
                        MessageBox.Show("Không thể khởi động driver WinRing0. Vui lòng chạy ứng dụng dưới quyền Administrator và đảm bảo WinRing0x64.dll có sẵn.", 
                            "Cảnh Báo Driver", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Driver] Init exception: {ex.Message}");
            }
        }

        private void SensorTimer_Tick(object? sender, EventArgs e)
        {
            // Update CPU temp and power on UI in background if benchmark is not active
            if (!_viewModel.IsBenchmarking)
            {
                double tempSum = 0;
                double power = 0;
                int tempCount = 0;

                try
                {
                    foreach (var hardware in _computer.Hardware)
                    {
                        hardware.Update();
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Core"))
                            {
                                tempSum += sensor.Value ?? 0;
                                tempCount++;
                            }
                            else if (sensor.SensorType == SensorType.Power && sensor.Name.Contains("Package"))
                            {
                                power = sensor.Value ?? 0;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Sensors] Poll exception: {ex.Message}");
                }

                double avgTemp = tempCount > 0 ? (tempSum / tempCount) : 45.0;
                double finalPower = power > 0 ? power : 15.0;

                _viewModel.CpuTempNum = (int)Math.Round(avgTemp);
                _viewModel.CpuPowerNum = Math.Round(finalPower, 1);
            }

            // Read RAM usage stats
            var ramStats = MemoryOptimizer.GetRamStats();
            _viewModel.RamUsagePercent = ramStats.UsagePercent;
            _viewModel.StandbyMemoryText = $"{ramStats.StandbyGb:0.00} GB";

            // Run junk scan asynchronously every 20 seconds (10 ticks)
            _sensorTickCount++;
            if (_sensorTickCount % 10 == 1)
            {
                UpdateJunkSizeAsync();
            }

            // Run automatic memory standby purge every 5 minutes (150 ticks) if enabled
            if (_viewModel.IsRamCleanAuto)
            {
                _ramCleanTickCount++;
                if (_ramCleanTickCount >= 150)
                {
                    _ramCleanTickCount = 0;
                    Task.Run(async () =>
                    {
                        await MemoryOptimizer.PurgeSystemMemoryAsync();
                        Dispatcher.Invoke(() =>
                        {
                            _viewModel.RamStatusText = "RAM Standby: Tự động dọn thành công";
                        });
                    });
                }
            }
        }

        private async void UpdateJunkSizeAsync()
        {
            double sizeGb = await MemoryOptimizer.GetJunkSizeGbAsync();
            _viewModel.JunkFilesText = $"{sizeGb:0.00} GB";
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsEcoMode))
            {
                ApplyEcoMode(_viewModel.IsEcoMode);
            }
        }

        private void ApplyEcoMode(bool isEcoEnabled)
        {
            if (isEcoEnabled)
            {
                // Eco Mode: Disable Mica transparency and set window background to solid flat dark gray
                this.WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType.None;
                this.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#141414"));
            }
            else
            {
                // Premium Mode: Enable Mica backdrop and clear background color
                this.WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType.Mica;
                this.Background = System.Windows.Media.Brushes.Transparent;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // Clean up resources
            if (_isIntel)
            {
                WinRing0.DeinitializeOls();
            }
            try
            {
                _computer.Close();
            }
            catch {}
        }
    }

    /// <summary>
    /// ViewModel holding data bindings for MainWindow.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly bool _isIntel;
        private bool _isBenchmarking = false;
        
        // Sensor values
        private string _cpuTempText = "Nhiệt độ: -- °C";
        private string _cpuPowerText = "Công suất: -- W";
        private string _activeGameMode = "Chế độ: Balanced";
        private string _activeProcessText = "Không phát hiện game đang chạy";
        private string _tweakStatusText = "OS Tweaks: Tắt";
        private string _ramStatusText = "RAM Standby: Sẵn sàng dọn";

        // Sliders & text inputs
        private int _intelCoreOffset = -80;
        private int _intelCacheOffset = -50;
        private int _amdCoOffset = -20;
        private string _amdTdpText = "35";

        // Tweak selection check states
        private bool _isTelemetryDisabled = true;
        private bool _isNetworkOptimized = true;
        private bool _isRamCleanAuto = true;
        private bool _isMsiEnabled = true;
        private bool _isEcoMode = false;

        // Custom stats for monospace displays
        private int _cpuTempNum = 45;
        private double _cpuPowerNum = 15.0;
        private int _ramUsagePercent = 50;
        private string _junkFilesText = "Quét...";
        private string _standbyMemoryText = "Quét...";

        // Benchmark progress and status
        private Visibility _progressCardVisibility = Visibility.Collapsed;
        private string _benchmarkStatusText = "Sẵn sàng...";

        // Comparison statistics
        private string _stockScoreText = "-- GOPs";
        private string _optScoreText = "-- GOPs";
        private string _diffScoreText = "--%";
        private string _stockTempText = "-- °C";
        private string _optTempText = "-- °C";
        private string _diffTempText = "--%";
        private string _stockPowerText = "-- W";
        private string _optPowerText = "-- W";
        private string _diffPowerText = "--%";
        private string _stockEfficiencyText = "--";
        private string _optEfficiencyText = "--";
        private string _diffEfficiencyText = "--%";

        public bool IsBenchmarking
        {
            get => _isBenchmarking;
            set { _isBenchmarking = value; OnPropertyChanged(); }
        }

        public MainViewModel(bool isIntel)
        {
            _isIntel = isIntel;
            ApplyHardwareCommand = new RelayCommand(ExecuteApplyHardware);
            RunAutoOptBenchmarkCommand = new RelayCommand(ExecuteAutoOptBenchmark, () => !IsBenchmarking);
            RunOptimizeCommand = new RelayCommand(ExecuteOptimize, () => !IsBenchmarking);
        }

        // --- Commands ---
        public ICommand ApplyHardwareCommand { get; }
        public ICommand RunAutoOptBenchmarkCommand { get; }
        public ICommand RunOptimizeCommand { get; }

        private void ExecuteApplyHardware()
        {
            try
            {
                if (_isIntel)
                {
                    // Intel Undervolting via FIVR
                    bool coreOk = IntelFivrTuner.ApplyUndervolt(IntelFivrTuner.PlaneCpuCore, IntelCoreOffset);
                    bool cacheOk = IntelFivrTuner.ApplyUndervolt(IntelFivrTuner.PlaneCpuCache, IntelCacheOffset);
                    
                    if (coreOk && cacheOk)
                    {
                        MessageBox.Show($"Đã áp dụng hạ thế Intel thành công:\nCore Offset: {IntelCoreOffset} mV\nCache Offset: {IntelCacheOffset} mV", 
                            "Áp Dụng Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Áp dụng thất bại. Vui lòng đảm bảo driver WinRing0 được tải đúng và CPU hỗ trợ FIVR undervolting.", 
                            "Lỗi Phần Cứng", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // AMD Undervolting via RyzenAdj
                    int tdp = int.TryParse(AmdTdpText, out int parsedTdp) ? parsedTdp : 35;
                    
                    Task.Run(async () =>
                    {
                        bool success = await AmdTuner.ApplySettingsAsync(tdp, tdp + 5, tdp + 10, 85, AmdCoOffset);
                        
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (success)
                            {
                                MessageBox.Show($"Đã áp dụng cấu hình AMD thành công:\nCurve Optimizer: {AmdCoOffset}\nTDP: {tdp} W", 
                                    "Áp Dụng Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show("Không thể thực thi RyzenAdj. Vui lòng kiểm tra xem ryzenadj.exe có nằm cùng thư mục ứng dụng hay không.", 
                                    "Lỗi Thực Thi", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Đã xảy ra lỗi: {ex.Message}", "Lỗi Lập Trình", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExecuteOptimize()
        {
            IsBenchmarking = true;
            BenchmarkStatusText = "Đang chuẩn bị tối ưu hệ thống...";

            try
            {
                // Create a system restore point first for safety
                BenchmarkStatusText = "Đang tạo điểm khôi phục hệ thống (System Restore)...";
                await Task.Run(() => RestorePoint.Create("vOptimizer_OneClickTweak"));

                // Apply telemetry settings
                BenchmarkStatusText = "Đang áp dụng cấu hình vô hiệu hóa Telemetry...";
                RegistryTweaker.ApplyTelemetryTweak(IsTelemetryDisabled);

                // Apply network settings
                BenchmarkStatusText = "Đang tối ưu hóa cấu hình băng thông mạng...";
                RegistryTweaker.ApplyLatencyTweaks(IsNetworkOptimized);

                // Apply MSI Mode settings
                BenchmarkStatusText = "Đang tối ưu hóa MSI (Message Signaled Interrupts) Mode...";
                MsiOptimizer.OptimizeMsiMode(IsMsiEnabled);

                // Apply Power plan tweaks
                BenchmarkStatusText = "Đang tắt CPU Core Parking & tối ưu EPP index...";
                PowerPlanManager.SetGamingPowerProfile(true);

                // Purge RAM Cache and background working sets
                BenchmarkStatusText = "Đang giải phóng bộ nhớ RAM Standby & Working Set...";
                await MemoryOptimizer.PurgeSystemMemoryAsync();

                // Clean Junk files
                BenchmarkStatusText = "Đang dọn dẹp tập tin rác hệ thống...";
                await MemoryOptimizer.CleanJunkAsync();

                // Enable 0.5ms Timer Resolution
                BenchmarkStatusText = "Đang thiết lập độ phân giải Timer 0.5ms...";
                TimerResolution.SetResolution(5000, true, out _);

                // Complete state update
                TweakStatusText = "OS Tweaks: ĐÃ BẬT (TCP NoDelay, 100% CPU, MSI Mode, 0.5ms)";
                RamStatusText = "RAM Standby: Đã dọn dẹp & Tự động chạy";

                // Re-scan junk files
                double sizeGb = await MemoryOptimizer.GetJunkSizeGbAsync();
                JunkFilesText = $"{sizeGb:0.00} GB";

                MessageBox.Show("Đã hoàn thành tối ưu hệ thống một chạm thành công!\n- Đã tạo điểm khôi phục vOptimizer_OneClickTweak.\n- Đã dọn dẹp RAM & tập tin rác.\n- Đã tắt Core Parking & tối ưu độ trễ ngắt MSI.",
                    "Tối Ưu Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi trong quá trình tối ưu: {ex.Message}", "Lỗi Tối Ưu", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBenchmarking = false;
                BenchmarkStatusText = "Sẵn sàng...";
            }
        }

        private async void ExecuteAutoOptBenchmark()
        {
            IsBenchmarking = true;
            ProgressCardVisibility = Visibility.Visible;

            try
            {
                BenchmarkEngine engine = new BenchmarkEngine();

                // Step 1: Run Stock Benchmark (15 seconds)
                BenchmarkStatusText = "Đang đo lường hiệu năng gốc (Stock)... [15s]";
                var stockResult = await engine.RunBenchmarkAsync(15);

                StockScoreText = $"{stockResult.ScoreGops} GOPs";
                StockTempText = $"{stockResult.PeakTemp} °C";
                StockPowerText = $"{stockResult.AveragePower} W";
                StockEfficiencyText = $"{Math.Round(stockResult.ScoreGops / stockResult.AveragePower, 3)}";

                // Step 2: Apply Optimization Configuration
                BenchmarkStatusText = "Đang tạo điểm khôi phục hệ thống an toàn...";
                
                // Create Windows Restore Point first
                RestorePoint.Create("vOptimizer_PreOptimization");

                BenchmarkStatusText = "Đang kích hoạt Tối Ưu Hóa & Hàng Rào Độ Trễ...";
                
                // Hardware undervolt & power limits
                if (_isIntel)
                {
                    IntelFivrTuner.ApplyUndervolt(IntelFivrTuner.PlaneCpuCore, IntelCoreOffset);
                    IntelFivrTuner.ApplyUndervolt(IntelFivrTuner.PlaneCpuCache, IntelCacheOffset);
                }
                else
                {
                    int tdp = int.TryParse(AmdTdpText, out int parsedTdp) ? parsedTdp : 35;
                    await AmdTuner.ApplySettingsAsync(tdp, tdp + 5, tdp + 10, 85, AmdCoOffset);
                }

                // OS level Tweaks
                RegistryTweaker.ApplyTelemetryTweak(IsTelemetryDisabled);
                RegistryTweaker.ApplyLatencyTweaks(true);
                MsiOptimizer.OptimizeMsiMode(true); // Force MSI Mode on GPUs and Network Cards
                TweakStatusText = "OS Tweaks: ĐÃ BẬT (TCP NoDelay, 100% CPU, MSI Mode)";

                // Memory Purging
                await MemoryOptimizer.PurgeSystemMemoryAsync();
                RamStatusText = "RAM Standby: Đã dọn dẹp trống";

                // Step 3: Run Optimized Benchmark (15 seconds)
                BenchmarkStatusText = "Đang đo lường hiệu năng đã Tối Ưu... [15s]";
                var optResult = await engine.RunBenchmarkAsync(15);

                OptScoreText = $"{optResult.ScoreGops} GOPs";
                OptTempText = $"{optResult.PeakTemp} °C";
                OptPowerText = $"{optResult.AveragePower} W";
                
                double optEff = optResult.ScoreGops / optResult.AveragePower;
                OptEfficiencyText = $"{Math.Round(optEff, 3)}";

                // Step 4: Calculate Differences
                double diffScore = ((optResult.ScoreGops - stockResult.ScoreGops) / stockResult.ScoreGops) * 100;
                double diffTemp = ((stockResult.PeakTemp - optResult.PeakTemp) / stockResult.PeakTemp) * 100; // negative temperature change is green
                double diffPower = ((optResult.AveragePower - stockResult.AveragePower) / stockResult.AveragePower) * 100;
                
                double stockEff = stockResult.ScoreGops / stockResult.AveragePower;
                double diffEff = ((optEff - stockEff) / stockEff) * 100;

                DiffScoreText = $"{Math.Round(diffScore, 1):+0.0;-0.0;0}%";
                DiffTempText = $"{Math.Round(-diffTemp, 1):+0.0;-0.0;0}% (Giảm nhiệt)";
                DiffPowerText = $"{Math.Round(diffPower, 1):+0.0;-0.0;0}%";
                DiffEfficiencyText = $"{Math.Round(diffEff, 1):+0.0;-0.0;0}%";

                BenchmarkStatusText = "Đã hoàn thành tối ưu hóa và đối sánh!";
                MessageBox.Show("Quá trình Tự Động Tối Ưu Hóa & Đối Sánh hoàn tất! Hãy xem bảng so sánh bên phải để biết chi tiết cải thiện hiệu năng và nhiệt độ.", 
                    "Hoàn Thành", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi trong quá trình chạy: {ex.Message}", "Lỗi Benchmark", MessageBoxButton.OK, MessageBoxImage.Error);
                BenchmarkStatusText = "Lỗi trong quá trình benchmark.";
            }
            finally
            {
                IsBenchmarking = false;
                ProgressCardVisibility = Visibility.Collapsed;
            }
        }

        // --- Data Binding Properties ---
        public string CpuTempText { get => _cpuTempText; set { _cpuTempText = value; OnPropertyChanged(); } }
        public string CpuPowerText { get => _cpuPowerText; set { _cpuPowerText = value; OnPropertyChanged(); } }
        public string ActiveGameMode { get => _activeGameMode; set { _activeGameMode = value; OnPropertyChanged(); } }
        public string ActiveProcessText { get => _activeProcessText; set { _activeProcessText = value; OnPropertyChanged(); } }
        public string TweakStatusText { get => _tweakStatusText; set { _tweakStatusText = value; OnPropertyChanged(); } }
        public string RamStatusText { get => _ramStatusText; set { _ramStatusText = value; OnPropertyChanged(); } }

        public int IntelCoreOffset { get => _intelCoreOffset; set { _intelCoreOffset = value; OnPropertyChanged(); } }
        public int IntelCacheOffset { get => _intelCacheOffset; set { _intelCacheOffset = value; OnPropertyChanged(); } }
        public int AmdCoOffset { get => _amdCoOffset; set { _amdCoOffset = value; OnPropertyChanged(); } }
        public string AmdTdpText { get => _amdTdpText; set { _amdTdpText = value; OnPropertyChanged(); } }

        // Quick Tweaks selection bindings
        public bool IsTelemetryDisabled { get => _isTelemetryDisabled; set { _isTelemetryDisabled = value; OnPropertyChanged(); } }
        public bool IsNetworkOptimized { get => _isNetworkOptimized; set { _isNetworkOptimized = value; OnPropertyChanged(); } }
        public bool IsRamCleanAuto { get => _isRamCleanAuto; set { _isRamCleanAuto = value; OnPropertyChanged(); } }
        public bool IsMsiEnabled { get => _isMsiEnabled; set { _isMsiEnabled = value; OnPropertyChanged(); } }
        public bool IsEcoMode { get => _isEcoMode; set { _isEcoMode = value; OnPropertyChanged(); } }

        // Raw numbers for Consolas/Monospace elements
        public int CpuTempNum { get => _cpuTempNum; set { _cpuTempNum = value; OnPropertyChanged(); } }
        public double CpuPowerNum { get => _cpuPowerNum; set { _cpuPowerNum = value; OnPropertyChanged(); } }
        public int RamUsagePercent { get => _ramUsagePercent; set { _ramUsagePercent = value; OnPropertyChanged(); } }
        public string JunkFilesText { get => _junkFilesText; set { _junkFilesText = value; OnPropertyChanged(); } }
        public string StandbyMemoryText { get => _standbyMemoryText; set { _standbyMemoryText = value; OnPropertyChanged(); } }

        public Visibility ProgressCardVisibility { get => _progressCardVisibility; set { _progressCardVisibility = value; OnPropertyChanged(); } }
        public string BenchmarkStatusText { get => _benchmarkStatusText; set { _benchmarkStatusText = value; OnPropertyChanged(); } }

        public string StockScoreText { get => _stockScoreText; set { _stockScoreText = value; OnPropertyChanged(); } }
        public string OptScoreText { get => _optScoreText; set { _optScoreText = value; OnPropertyChanged(); } }
        public string DiffScoreText { get => _diffScoreText; set { _diffScoreText = value; OnPropertyChanged(); } }
        public string StockTempText { get => _stockTempText; set { _stockTempText = value; OnPropertyChanged(); } }
        public string OptTempText { get => _optTempText; set { _optTempText = value; OnPropertyChanged(); } }
        public string DiffTempText { get => _diffTempText; set { _diffTempText = value; OnPropertyChanged(); } }
        public string StockPowerText { get => _stockPowerText; set { _stockPowerText = value; OnPropertyChanged(); } }
        public string OptPowerText { get => _optPowerText; set { _optPowerText = value; OnPropertyChanged(); } }
        public string DiffPowerText { get => _diffPowerText; set { _diffPowerText = value; OnPropertyChanged(); } }
        public string StockEfficiencyText { get => _stockEfficiencyText; set { _stockEfficiencyText = value; OnPropertyChanged(); } }
        public string OptEfficiencyText { get => _optEfficiencyText; set { _optEfficiencyText = value; OnPropertyChanged(); } }
        public string DiffEfficiencyText { get => _diffEfficiencyText; set { _diffEfficiencyText = value; OnPropertyChanged(); } }

        // --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Simple command helper for MVVM.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
