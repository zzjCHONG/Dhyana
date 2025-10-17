using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dhyana400BSI;
using OpenCvSharp.WpfExtensions;
using Simscop.Spindisk.Core.Interfaces;
using System.Diagnostics;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dhyana400BSIWpf
{
    public partial class TUCamViewModel : ObservableObject
    {
        private readonly ICameraBackup _camera;
        private readonly System.Timers.Timer? _timer;

        public TUCamViewModel()
        {
            _camera = new DhyanaCamera();
            _timer = new System.Timers.Timer(300);
            _timer.Elapsed += OnTimerElapsed!;

            _camera.FrameReceived += img =>
            {
                if (img == null || img.Empty())
                    return;

                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        Display = BitmapFrame.Create(img.ToBitmapSource());
                        img?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("OnCaptureChanged Error: " + ex.Message);
                    }
                });
            };
        }

        void InitSetting()
        {
            ExposureRangeMax = _camera.ExposureRange.Max;
            ExposureRangeMin = _camera.ExposureRange.Min;
            LevelRangeMax = _camera.LevelRange.Max;
            LevelRangeMin = _camera.LevelRange.Min;//该部分应在某个设定后对应获取

            RoiList = _camera.ROIList;
            ResolutionsList = _camera.ResolutionsList;
            GainList = _camera.GainList;
            ImageModeList = _camera.ImageModesList;
            CompositeModeList = _camera.CompositeModeList;

            IsFlipHorizontal = _camera.IsFlipHorizontally;
            IsFlipVertical = _camera.IsFlipVertially;
            IsAutoExposure = _camera.IsAutoExposure;
            IsAutoLevel = _camera.IsAutoLevel;

            ResolutionIndex = 0;
            GainIndex = 0;
            ImageModeIndex = 1;
            CompositeModeIndex = 0;
            IsFlipHorizontal = false;
            IsFlipVertical = false;
            IsAutoExposure = false;
            IsAutoLevel = false;

            // 初始化 ROI 选择（选择第一个，通常是最大ROI）
            if (RoiList != null && RoiList.Count > 0)
            {
                SelectedROI = RoiList[0];
                Debug.WriteLine($"初始化ROI选择: {SelectedROI}");
            }
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Exposure = _camera!.Exposure;
            Gamma = _camera!.Gamma;
            Contrast = _camera!.Contrast;
            Brightness = _camera!.Brightness;
            LeftLevel = _camera.CurrentLevel.Left;
            RightLevel = _camera.CurrentLevel.Right;

            FrameRate = _camera!.FrameRate;

            //// Debug 输出所有参数
            //Debug.WriteLine(
            //    $"[Camera Info] " +
            //    $"Exposure={Exposure}, " +
            //    $"Gamma={Gamma}, " +
            //    $"Contrast={Contrast}, " +
            //    $"Brightness={Brightness}, " +
            //    $"Level=({LeftLevel}, {RightLevel}), " +
            //    $"FrameRate={FrameRate}");
        }

        [ObservableProperty]
        public double _exposure;

        [ObservableProperty]
        public double _exposureRangeMax;

        [ObservableProperty]
        public double _exposureRangeMin;

        [ObservableProperty]
        public bool _isAutoExposure = false;

        [ObservableProperty]
        public double _brightness = 0;

        [ObservableProperty]
        public double _gamma = 1;

        [ObservableProperty]
        public double _contrast = 0;

        [ObservableProperty]
        public double _frameRate = 0;

        [ObservableProperty]
        public bool _isFlipHorizontal = false;

        [ObservableProperty]
        public bool _isFlipVertical = false;

        [ObservableProperty]
        private int _leftLevel = 0;

        [ObservableProperty]
        private int _rightLevel = 0;

        [ObservableProperty]
        public double _LevelRangeMax;

        [ObservableProperty]
        public double _LevelRangeMin;

        [ObservableProperty]
        public bool _isAutoLevel = false;

        [ObservableProperty]
        private bool _isStartAcquisition = false;

        [ObservableProperty]
        private List<string> _imageModeList = new();

        [ObservableProperty]
        private List<string> _gainList = new();

        [ObservableProperty]
        private List<string> _roiList = new();

        [ObservableProperty]
        private List<string> _resolutionsList = new();

        [ObservableProperty]
        private List<string> _compositeModeList = new();

        [ObservableProperty]
        private int resolutionIndex;

        [ObservableProperty]
        private int _imageModeIndex;

        [ObservableProperty]
        private int _gainIndex;

        [ObservableProperty]
        private string _selectedROI = string.Empty;

        [ObservableProperty]
        private int _compositeModeIndex;

        [ObservableProperty]
        private ImageSource? _display;

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private bool _propControlIsEnable=true;

        [RelayCommand]
        async Task Init()
        {
            await Task.Run(() =>
            {
                IsConnected = _camera.Init();
                if (IsConnected) _timer!.Start();
                Debug.WriteLine("Init_" + IsConnected);
                InitSetting();
            });
        }

        [RelayCommand]
        void RestoretoInitialSettings()
        {
            IsFlipHorizontal = false;
            IsFlipVertical = false;
            Brightness = 90;
            Contrast = 128;
            Gamma = 100;
            Exposure = 10;
            LeftLevel = 0;
            RightLevel = 65535;

            ResolutionIndex = 0;
            CompositeModeIndex = 0;
        }

        [RelayCommand]
        async Task CaptureAsync()
        {
            await _camera.CaptureAsync();
        }

        partial void OnIsStartAcquisitionChanged(bool value)
        {
            if (value)
            {
                _camera.StartCapture();
                _timer!.Start();
            }
            else
            {
                _camera?.StopCapture();
                _timer!.Stop();
            }
        }

        partial void OnIsAutoLevelChanged(bool value)
        {
            _camera.IsAutoLevel = value;
        }

        partial void OnIsAutoExposureChanged(bool value)
        {
            _camera.IsAutoExposure = value;
        }

        partial void OnExposureChanged(double value)
        {
            if (!IsAutoExposure)
                _camera.Exposure = value;
        }

        partial void OnGammaChanged(double value)
        {
            _camera.Gamma = value;
        }

        partial void OnBrightnessChanged(double value)
        {
            if (IsAutoExposure)
                _camera.Brightness = value;
        }

        partial void OnContrastChanged(double value)
        {
            _camera.Contrast = value;
        }

        partial void OnIsFlipHorizontalChanged(bool value)
        {
            _camera.IsFlipHorizontally = value;
        }

        partial void OnIsFlipVerticalChanged(bool value)
        {
            _camera.IsFlipVertially = value;
        }

        partial void OnImageModeIndexChanged(int value)
        {
            _camera.SetImageMode(value);
        }

        partial void OnGainIndexChanged(int value)
        {
            _camera.Gain = (ushort)value;
        }

        partial void OnLeftLevelChanged(int value)
        {
            if (!IsAutoLevel)
                _camera.CurrentLevel = (value, RightLevel);
        }

        partial void OnRightLevelChanged(int value)
        {
            if (!IsAutoLevel)
                _camera.CurrentLevel = (LeftLevel, value);
        }

        async partial void OnCompositeModeIndexChanged(int value)
        {
            PropControlIsEnable = false;

            await Task.Run(() =>
            {
                _camera.SetCompositeMode(value);
            });

            LevelRangeMax = _camera.LevelRange.Max;
            LevelRangeMin = _camera.LevelRange.Min;

            PropControlIsEnable = true;
        }

        async partial void OnResolutionIndexChanged(int value)
        {
            PropControlIsEnable = false;

            await Task.Run(() =>
            {
                _camera.SetResolution(value);
            });

            PropControlIsEnable = true;
        }

    }
}
