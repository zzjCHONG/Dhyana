using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dhyana400BSI;
using System.Windows.Media;

namespace Dhyana400BSIWpf
{
    public partial class TUCamViewModel : ObservableObject
    {
        private readonly DhyanaCamera _dhyanaCamera;

        public TUCamViewModel()
        {
            _dhyanaCamera = new DhyanaCamera();
        }

        [ObservableProperty]
        public double _exposure;

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
        private int leftLevel = 0;

        [ObservableProperty]
        private int rightLevel = 65535;

        [ObservableProperty]
        public bool _isAutoLevel = false;

        [ObservableProperty]
        private bool _isStartAcquisition = false;

        [ObservableProperty]
        private List<string> _imageModeandGlobalGainList = new();

        [ObservableProperty]
        private List<string> _roiList = new();

        [ObservableProperty]
        private List<string> _resolutionsList = new();

        [ObservableProperty]
        private int resolutionIndex;

        [ObservableProperty]
        private int _imageModeandGlobalGainIndex;

        [ObservableProperty]
        private int _roiIndex;

        [ObservableProperty]
        private ImageSource? _display;

        [RelayCommand]
        async Task Init()
        {
            await Task.Run(() =>
            {
                _dhyanaCamera.Init();
            });
        }

        [RelayCommand]
        void RestoretoInitialSettings()
        {

        }

        [RelayCommand]
        async Task CaptureAsync()
        {

        }

        [RelayCommand]
        void SetAutoLevel()
        {

        }

        [RelayCommand]
        void SetAutoexposure()
        {

        }

    }
}
