using OpenCvSharp;
using Simscop.Spindisk.Core.Constants;
using Simscop.Spindisk.Core.Interfaces;

namespace Dhyana400BSI
{
    public class DhyanaCamera : ICameraBackup
    {
        public event Action<Mat>? FrameReceived;

        public Dictionary<InfoEnum, string> InfoDirectory
        {
            get
            {
                string apiVersion = string.Empty;
                Dhyana.GetApiVersion(ref apiVersion);

                string apiModel = string.Empty;
                Dhyana.GetCameraModel(ref apiModel);

                string SerialNumber = string.Empty;
                Dhyana.GetCameraSerialNumber(ref SerialNumber);

                int Firmware = -1;
                Dhyana.GetFirmwareVersion(ref Firmware);

                return new()
                {
                    {InfoEnum.Model,apiModel},
                    {InfoEnum.SerialNumber,SerialNumber},
                    {InfoEnum.Version,apiVersion},
                    {InfoEnum.FirmwareVersion,$"V_{Firmware}"},
                };

            }
        }

        public (double Min, double Max) ExposureRange
        {
            //曝光时间的范围、步进与分辨率、最小曝光时间有关，通过接口获取范围
            //单位：毫秒
            get
            {
                Dhyana.GetExposureAttr(out var attr);
                return (attr.dbValMin, attr.dbValMax);
            }
        }

        public double Exposure
        {
            get
            {
                double exposureUs = 0;
                if (Dhyana.GetExposure(ref exposureUs))
                {
                    return exposureUs;
                }
                return 0;
            }
            set
            {
                Dhyana.SetExposure(value);
            }
        }

        public double Gamma
        {
            get
            {
                if (Dhyana.GetGamma(out double gamma))
                {
                    // SDK范围 0-255，转换为 -1 到 1
                    return (gamma - 127.5) / 127.5;
                }
                return 0;
            }
            set
            {
                // 转换：-1到1 映射到 0-255
                double sdkValue = (value + 1.0) * 127.5;
                sdkValue = Math.Clamp(sdkValue, 0, 255);
                Dhyana.SetGamma(sdkValue);
            }
        }

        public double Contrast
        {
            get
            {
                if (Dhyana.GetContrast(out double contrast))
                {
                    // SDK范围 0-255，转换为 -1 到 1
                    return (contrast - 127.5) / 127.5;
                }
                return 0;
            }
            set
            {
                // 转换：-1到1 映射到 0-255
                double sdkValue = (value + 1.0) * 127.5;
                sdkValue = Math.Clamp(sdkValue, 0, 255);
                Dhyana.SetContrast(sdkValue);
            }
        }

        public double Brightness
        {
            get
            {
                double brightness = 0;
                if (Dhyana.GetBrightness(ref brightness))
                {
                    // SDK范围 20-255，转换为 -1 到 1
                    return (brightness - 137.5) / 117.5;
                }
                return 0;
            }
            set
            {
                // 转换：-1到1 映射到 20-255
                double sdkValue = (value * 117.5) + 137.5;
                sdkValue = Math.Clamp(sdkValue, 20, 255);
                Dhyana.SetBrightness(sdkValue);
            }
        }

        public ushort Gain
        {
            //实际为0-5共六种模式
            //需同imagemode共同使用
            get
            {
                double gain = 0;
                if (Dhyana.GetGlobalGain(ref gain))
                {
                    return (ushort)gain;
                }
                return 0;
            }
            set
            {
                if (value < Dhyana.GlobalGain.Count)
                {
                    Dhyana.SetGlobalGain(value);
                }
            }
        }

        public (ushort Min, ushort Max) GainRange
        {
            get
            {
                // 全局增益范围 0-5
                return (0, (ushort)(Dhyana.GlobalGain.Count - 1));
            }
        }

        public bool IsAutoLevel
        {
            get
            {
                int mode = 0;
                if (Dhyana.GetAutoLevels(ref mode))
                {
                    return mode > 0;
                }
                return false;
            }
            set
            {
                // 3 = 自动左右色阶,2 = 自动右色阶，1 = 自动左色阶， 0 = 禁用
                Dhyana.SetAutoLevels(value ? 3 : 0);//默认设置自动左右色阶
            }
        }

        public bool IsAutoExposure
        {
            get
            {
                if (!Dhyana.GetAutoExposure(out var enable)) return false;
                return enable;
            }
            set
            {
                Dhyana.SetAutoExposure(value);
            }
        }

        public double FrameRate
        {
            get
            {
                double frameRate = 0;
                if (!Dhyana.GetFrameRate(ref frameRate)) return 0;
                return frameRate;
            }
        }

        public bool IsFlipHorizontally
        {
            get
            {
                Dhyana.GetHorizontal(out bool enabled);
                return enabled;
            }
            set
            {
                Dhyana.SetHorizontal(value);
            }
        }

        public bool IsFlipVertially
        {
            get
            {
                Dhyana.GetVertical(out bool enabled);
                return enabled;
            }
            set
            {
                Dhyana.SetVertical(value);
            }
        }

        public (double Left, double Right) CurrentLevel
        {
            get
            {
                Dhyana.GetLeftLevels(out var left);
                Dhyana.GetRightLevels(out var right);
                return (left, right);
            }
            set
            {
                double left = Math.Clamp(value.Left, LevelRange.Left, LevelRange.Right);
                double right = Math.Clamp(value.Right, LevelRange.Left, LevelRange.Right);

                Dhyana.SetLeftLevels(left);
                Dhyana.SetRightLevels(right);
            }
        }

        public Size ImageSize
        {
            get
            {
                TUCAM_ROI_ATTR roi = default;
                if (Dhyana.GetRoi(ref roi))
                {
                    if (roi.bEnable)
                    {
                        return new Size(roi.nWidth, roi.nHeight);
                    }
                }

                // 根据分辨率模式返回
                int resId = 0;
                if (Dhyana.GetResolution(ref resId))
                {
                    return resId switch
                    {
                        0 => new Size(2048, 2048),  // Normal
                        1 => new Size(2048, 2048),  // Enhance
                        2 => new Size(1024, 1024),  // 2x2 Bin
                        3 => new Size(512, 512),    // 4x4 Bin
                        _ => new Size(2048, 2048)
                    };
                }

                return new Size(2048, 2048);
            }
        }

        public bool SetResolution(int resolution)
        {
            if (resolution < 0 || resolution >= Dhyana.Resolutions.Count)
            {
                Console.WriteLine($"[ERROR] Invalid resolution index: {resolution}");
                return false;
            }

            return Dhyana.SetResolution(resolution);
        }

        public List<string> ResolutionsList => Dhyana.Resolutions;

        public bool SetImageMode(int imageMode)
        {
            if (imageMode < 0 || imageMode >= Dhyana.ImageMode.Count)
            {
                Console.WriteLine($"[ERROR] Invalid image mode index: {imageMode}");
                return false;
            }

            // Dhyana SDK 的 ImageMode 是从 1 开始的
            return Dhyana.SetImageMode(imageMode + 1);
        }

        public List<string> ImageModesList => Dhyana.ImageMode;

        public void SetROI(int width, int height, int offsetX, int offsetY)
        {

            // 调整为4的倍数
            width = (width >> 2) << 2;
            height = (height >> 2) << 2;
            offsetX = (offsetX >> 2) << 2;
            offsetY = (offsetY >> 2) << 2;

            if (!Dhyana.SetRoi(width, height, offsetX, offsetY))
            {
                Console.WriteLine("[ERROR] Failed to set ROI");
            }
            else
            {
                // 更新 ROI 列表
                UpdateROIsList();
            }

        }

        public List<string> ROIList
        {
            get => _roiList;
        }

        public bool DisableROI()
        {
            var result = Dhyana.DisableRoi();
            if (result) UpdateROIsList();
            return result;
        }

        #region 

        private int _imageDepth = 16;//默认获得16UC1图像

        public (double Left, double Right) LevelRange
        {
            get
            {
                // 根据图像深度返回范围
                int maxValue = _imageDepth == 8 ? 255 : 65535;
                return (1, maxValue);
            }
        }

        public int ImageDetph
        {
            get => _imageDepth;
            set
            {
                // Dhyana 支持 8/11/12/16 bit
                // 简化为 8 或 16
                _imageDepth = (value <= 8) ? 8 : 16;
            }
        }

        private readonly List<string> _roiList = new();
        /// <summary>
        /// 更新 ROI 列表信息
        /// </summary>
        private void UpdateROIsList()
        {
            _roiList.Clear();

            TUCAM_ROI_ATTR roi = default;
            if (Dhyana.GetRoi(ref roi))
            {
                if (roi.bEnable)
                {
                    _roiList.Add($"Enabled: {roi.nWidth}x{roi.nHeight}");
                    _roiList.Add($"Position: ({roi.nHOffset}, {roi.nVOffset})");
                    _roiList.Add($"Range: X[{roi.nHOffset}-{roi.nHOffset + roi.nWidth}], Y[{roi.nVOffset}-{roi.nVOffset + roi.nHeight}]");
                }
                else
                {
                    _roiList.Add("ROI: Disabled (Full Frame)");

                    // 获取当前分辨率作为全画幅尺寸
                    int resId = 0;
                    if (Dhyana.GetResolution(ref resId))
                    {
                        var size = resId switch
                        {
                            0 => "2048x2048",
                            1 => "2048x2048",
                            2 => "1024x1024",
                            3 => "512x512",
                            _ => "Unknown"
                        };
                        _roiList.Add($"Full Frame Size: {size}");
                    }
                }
            }
        }
        #endregion

        #region 硬件无法实现
        public event Action<bool>? OnDisConnectState;

        public int ClockwiseRotation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int PseudoColor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public (double Min, double Max) TintRange => throw new NotImplementedException();

        public double Tint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public (double Min, double Max) TemperatureRange => throw new NotImplementedException();

        public double Temperature { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool AutoWhiteBlanceOnce()
        {
            throw new NotImplementedException();
        }

        public (uint Width, uint Height) Resolution { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public List<(uint Width, uint Height)> Resolutions { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        #endregion

        public bool Capture(out Mat? img)
        {
            throw new NotImplementedException();
        }

        public Task<Mat?> CaptureAsync()
        {
            throw new NotImplementedException();
        }

        public bool Init() => Dhyana.InitializeSdk() && Dhyana.InitializeCamera();

        public bool SaveImage(string path)
        {
            throw new NotImplementedException();
        }

        public bool StartCapture() => Dhyana.StartCapture();

        public bool StopCapture() => Dhyana.StopCapture();

    }
}
