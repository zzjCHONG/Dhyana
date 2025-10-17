using OpenCvSharp;
using Simscop.Spindisk.Core.Constants;
using Simscop.Spindisk.Core.Interfaces;
using System.Diagnostics;

namespace Dhyana400BSI
{
    public class DhyanaCamera : ICameraBackup
    {
        public bool Init()
        {
            var res = Dhyana.InitializeSdk() && Dhyana.InitializeCamera();

            InitDefaultSetting();
            return res;
        }

        void InitDefaultSetting()
        {
            int currentResolution = 0;
            Dhyana.GetResolution(ref currentResolution);
            UpdateROIOptionsForResolution(currentResolution);

            Dhyana.SetNoiseLevel(0);
            Dhyana.SetFanGear(2);
            Dhyana.SetLedEnable(true);
        }

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
                    return gamma;
                    //// SDK范围 0-255，转换为 -1 到 1
                    //return (gamma - 127.5) / 127.5;
                }
                return 0;
            }
            set
            {
                //// 转换：-1到1 映射到 0-255
                //double sdkValue = (value + 1.0) * 127.5;
                //sdkValue = Math.Clamp(sdkValue, 0, 255);
                double sdkValue = Math.Clamp(value, 0, 255);
                Dhyana.SetGamma(sdkValue);
            }
        }

        public double Contrast
        {
            get
            {
                if (Dhyana.GetContrast(out double contrast))
                {
                    return contrast;
                    //// SDK范围 0-255，转换为 -1 到 1
                    //return (contrast - 127.5) / 127.5;
                }
                return 0;
            }
            set
            {
                //// 转换：-1到1 映射到 0-255
                //double sdkValue = (value + 1.0) * 127.5;
                //sdkValue = Math.Clamp(sdkValue, 0, 255);

                double sdkValue = Math.Clamp(value, 0, 255);
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
                    return brightness;
                    //// SDK范围 20-255，转换为 -1 到 1
                    //return (brightness - 137.5) / 117.5;
                }
                return 0;
            }
            set
            {
                //// 转换：-1到1 映射到 20-255
                //double sdkValue = (value * 117.5) + 137.5;
                //sdkValue = Math.Clamp(sdkValue, 20, 255);


                double sdkValue = Math.Clamp(value, 20, 255);
                Dhyana.SetBrightness(sdkValue);
            }
        }

        public List<string> GainList => Dhyana.GlobalGain;

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

        public (int Left, int Right) CurrentLevel
        {
            get
            {
                Dhyana.GetLeftLevels(out var left);
                Dhyana.GetRightLevels(out var right);
                return (left, right);
            }
            set
            {
                int left = Math.Clamp(value.Left, LevelRange.Min, LevelRange.Max);
                int right = Math.Clamp(value.Right, LevelRange.Min, LevelRange.Max);

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

            if (!StopCapture()) return false;

            var res = Dhyana.SetResolution(resolution);

            if (res)
            {
                // 分辨率改变后，更新可用的 ROI 选项
                UpdateROIOptionsForResolution(resolution);
                Console.WriteLine($"[INFO] Resolution changed to index {resolution}");
            }

            if (!StartCapture()) return false;

            return res;
        }

        public List<string> ResolutionsList => Dhyana.Resolutions;

        public bool SetImageMode(int imageMode)
        {
            if (imageMode < 0 || imageMode >= Dhyana.ImageMode.Count)
            {
                Console.WriteLine($"[ERROR] Invalid image mode index: {imageMode}");
                return false;
            }

            return Dhyana.SetImageMode(imageMode+1);
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
                Console.WriteLine($"[INFO] ROI set successfully: {width}x{height} at ({offsetX}, {offsetY})");

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
            if (result)
            {
                Console.WriteLine("[INFO] ROI disabled, using full frame");
                UpdateROIsList();
            }
            else
            {
                Console.WriteLine("[ERROR] Failed to disable ROI");
            }
            return result;
        }

        /// <summary>
        /// 根据分辨率更新可用的 ROI 选项列表
        /// </summary>
        private void UpdateROIOptionsForResolution(int resolutionIndex)
        {
            _roiList.Clear();

            // 根据分辨率获取最大尺寸
            var (maxWidth, maxHeight) = resolutionIndex switch
            {
                0 => (2048, 2048), // 2048x2048 标准
                1 => (2048, 2048), // 2048x2048 增强
                2 => (1024, 1024), // 1024x1024 2x2Bin
                3 => (512, 512),   // 512x512 4x4Bin
                _ => (2048, 2048)  // 默认
            };

            // 根据最大尺寸生成 ROI 选项
            if (maxWidth >= 2048)
            {
                _roiList.Add("2048 x 2048");
                _roiList.Add("1024 x 1024");
                _roiList.Add("512 x 512");
                _roiList.Add("512 x 128");
                _roiList.Add("256 x 256");
                _roiList.Add("128 x 128");
            }
            else if (maxWidth >= 1024)
            {
                _roiList.Add("1024 x 1024");
                _roiList.Add("512 x 512");
                _roiList.Add("512 x 128");
                _roiList.Add("256 x 256");
                _roiList.Add("128 x 128");
            }
            else if (maxWidth >= 512)
            {
                _roiList.Add("512 x 512");
                _roiList.Add("256 x 256");
                _roiList.Add("128 x 128");
                _roiList.Add("64 x 64");
            }

            // 总是添加自定义选项
            _roiList.Add("自定义ROI");

            // 添加全画幅选项（禁用 ROI）
            _roiList.Add($"全画幅 ({maxWidth}x{maxHeight})");

            Console.WriteLine($"[INFO] ROI options updated for resolution {resolutionIndex}: {string.Join(", ", _roiList)}");
        }

        public List<string> CompositeModeList => Dhyana.CompositeModeList;

        public bool SetCompositeMode(int imageMode)
        {
            //需要停止采集后再打开

            if (imageMode < 0 || imageMode >= Dhyana.CompositeModeList.Count)
            {
                Console.WriteLine($"[ERROR] Invalid CompositeMode index: {imageMode}");
                return false;
            }
            if (!StopCapture()) return false;
      
          
            bool success = true;
            switch (imageMode)
            {
                case 0://高动态-16bit
                    success &= Dhyana.SetGlobalGain(0);
                    success &= Dhyana.SetImageMode(2);
                    break;
                case 1://高增益-11bit
                    success &= Dhyana.SetGlobalGain(1);
                    success &= Dhyana.SetImageMode(2);
                    break;
                case 2://高增益高速-12bit
                    success &= Dhyana.SetGlobalGain(1);
                    success &= Dhyana.SetImageMode(3);
                    break;
                case 3://高增益全局重置-12bit
                    success &= Dhyana.SetGlobalGain(1);
                    success &= Dhyana.SetImageMode(5);
                    break;
                case 4://低增益-11bit
                    success &= Dhyana.SetGlobalGain(2);
                    success &= Dhyana.SetImageMode(2);
                    break;
                case 5://低增益高速-12bit
                    success &= Dhyana.SetGlobalGain(2);
                    success &= Dhyana.SetImageMode(4);
                    break;
                case 6://低增益全局重置-12bit
                    success &= Dhyana.SetGlobalGain(2);
                    success &= Dhyana.SetImageMode(5);
                    break;
                default:
                    break;
            }

            if (!StartCapture()) return false;
            return success;
        }

        #region 

        public (int Min, int Max) LevelRange
        {
            get
            {
                int maxValue = ImageDetph switch
                {
                    8 => 255,       // 8bit
                    11 => 2047,      // 11bit
                    12 => 4095,      // 12bit
                    16 => 65535,     // 16bit
                    _ => 255        // 默认值（防止异常）
                };

                return (0, maxValue);
            }
        }


        public int ImageDetph
        {
            get
            {
                int bits = 0;
                Dhyana.GetCameraBits(ref bits);
                return bits;
            }
            set
            {
                //// Dhyana 支持 8/11/12/16 bit
                //// 简化为 8 或 16
                //_imageDepth = (value <= 8) ? 8 : 16;
                throw new NotImplementedException();
            }
        }

        private readonly List<string> _roiList = new();

        /// <summary>
        /// 更新 ROI 状态信息（原有方法，保持用于显示当前 ROI 状态）
        /// </summary>
        private static void UpdateROIsList()
        {
            // 这个方法保持原样，用于显示当前 ROI 的详细状态
            // 但不再用于更新可选 ROI 列表

            TUCAM_ROI_ATTR roi = default;
            if (Dhyana.GetRoi(ref roi))
            {
                if (roi.bEnable)
                {
                    Console.WriteLine($"[INFO] Current ROI: {roi.nWidth}x{roi.nHeight} at ({roi.nHOffset}, {roi.nVOffset})");
                }
                else
                {
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
                        Console.WriteLine($"[INFO] ROI disabled, using full frame: {size}");
                    }
                }
            }
        }

        #endregion

        #region 当前硬件无法实现

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

        private Thread? _captureThread;
        private CancellationTokenSource? _cts;
        private readonly object _lockObj = new();
        private Mat? _latestFrame;
        private bool _isCapturing = false;
        public event Action<Mat>? FrameReceived;

        /// <summary>
        /// 启动图像采集（连续模式）
        /// </summary>
        public bool StartCapture()
        {
            if (_isCapturing)
            {
                Console.WriteLine("[WARNING] Capture already started");
                return true;
            }

            if (!Dhyana.StartCapture())
            {
                Console.WriteLine("[ERROR] Failed to start camera capture");
                return false;
            }

            _isCapturing = true;
            _cts = new CancellationTokenSource();
            _captureThread = new Thread(() => CaptureLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "CameraCaptureThread",
            };
            _captureThread.Start();

            Console.WriteLine("[INFO] Continuous capture thread started");
            return true;
        }

        /// <summary>
        /// 停止图像采集
        /// </summary>
        public bool StopCapture()
        {
            if (!_isCapturing)
            {
                Console.WriteLine("[INFO] Capture not running");
                return true;
            }

            Console.WriteLine("[INFO] Stopping capture...");

            _isCapturing = false;

            _cts?.Cancel();

            if (_captureThread != null)
            {
                if (!_captureThread.Join(5000))
                {
                    Console.WriteLine("[WARNING] Capture thread did not stop within timeout");
                }
                else
                {
                    Console.WriteLine("[INFO] Capture thread stopped gracefully");
                }
            }

            bool result = Dhyana.StopCapture();

            _cts?.Dispose();
            _cts = null;
            _captureThread = null;

            lock (_lockObj)
            {
                _latestFrame?.Dispose();
                _latestFrame = null;
            }

            if (result)
            {
                Console.WriteLine("[INFO] Capture stopped successfully");
            }
            else
            {
                Console.WriteLine("[ERROR] Failed to stop capture");
            }

            return result;
        }

        /// <summary>
        /// 将11bit或12bit图像转换为标准16bit（左移补齐高位）
        /// </summary>
        private Mat? NormalizeTo16Bit(Mat source, int bitDepth)
        {
            if (source == null || source.Empty())
                return null;

            // 如果已经是16bit且数据已标准化，直接返回
            if (bitDepth == 16)
                return source.Clone();

            // 计算需要左移的位数
            int leftShift = 16 - bitDepth;

            if (leftShift <= 0)
                return source.Clone();

            Mat output = new Mat();

            // 左移补齐到16bit
            // 11bit: 左移5位 (0-2047 -> 0-65504)
            // 12bit: 左移4位 (0-4095 -> 0-65520)
            source.ConvertTo(output, MatType.CV_16UC1);
            output *= (1 << leftShift);

            return output;
        }

        /// <summary>
        /// 连续采集循环（后台线程）
        /// </summary>
        private void CaptureLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isCapturing)
            {
                Mat? mat = null;
                Mat? normalized = null;

                try
                {
                    int bitDepth = 0;
                    Dhyana.GetCameraBits(ref bitDepth);// 11, 12, 或 16

                    if (Dhyana.Capture(out mat) && mat != null && !mat.Empty())
                    {
                        normalized = NormalizeTo16Bit(mat, bitDepth);

                        if (normalized != null && !normalized.Empty())
                        {
                            lock (_lockObj)
                            {
                                _latestFrame?.Dispose();
                                _latestFrame = normalized.Clone();
                            }

                            try
                            {
                                FrameReceived?.Invoke(normalized.Clone());
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] FrameReceived event handler exception: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Capture loop exception: {ex.Message}");
                    Thread.Sleep(100);
                }
                finally
                {
                    mat?.Dispose();
                    normalized?.Dispose();
                }

                Thread.Sleep(10);
            }
            Console.WriteLine("[INFO] Capture loop exited");
        }

        /// <summary>
        /// 获取单帧图像（同步方法）
        /// 如果已启动连续采集，返回最新帧；否则执行单次采集
        /// </summary>
        public bool Capture(out Mat? img)
        {
            img = null;

            try
            {
                if (_isCapturing)
                {
                    // 连续采集模式：返回最新帧的副本
                    lock (_lockObj)
                    {
                        if (_latestFrame != null && !_latestFrame.Empty())
                        {
                            img = _latestFrame.Clone();
                            return true;
                        }
                    }
                    Console.WriteLine("[WARNING] No frame available yet");
                    return false;
                }
                else
                {
                    // 单次采集模式：需要先启动后采集再停止
                    if (!Dhyana.StartCapture(TUCAM_CAPTURE_MODES.TUCCM_TRIGGER_SOFTWARE))
                    {
                        Console.WriteLine("[ERROR] Failed to start single capture");
                        return false;
                    }

                    int bitDepth = 0;
                    Dhyana.GetCameraBits(ref bitDepth);
                    bool success = Dhyana.Capture(out Mat mat);
                    Dhyana.StopCapture();

                    if (success && mat != null && !mat.Empty())
                    {
                        // 转换为标准16bit
                        img = NormalizeTo16Bit(mat, bitDepth);
                        mat.Dispose();
                        return img != null;
                    }

                    Console.WriteLine("[ERROR] Failed to capture single frame");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Capture exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 保存当前图像到指定路径
        /// </summary>
        public bool SaveImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("[ERROR] Invalid save path");
                return false;
            }

            try
            {
                Mat? imageToSave = null;

                if (_isCapturing)
                {
                    // 连续采集模式：保存最新帧
                    lock (_lockObj)
                    {
                        if (_latestFrame != null && !_latestFrame.Empty())
                        {
                            imageToSave = _latestFrame.Clone();
                        }
                    }
                }
                else
                {
                    // 单次采集模式：采集一帧后保存
                    if (!Capture(out imageToSave) || imageToSave == null)
                    {
                        Console.WriteLine("[ERROR] Failed to capture image for saving");
                        return false;
                    }
                }

                if (imageToSave == null || imageToSave.Empty())
                {
                    Console.WriteLine("[ERROR] No image to save");
                    return false;
                }

                // 确保目录存在
                string? directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // 保存图像
                bool success = Cv2.ImWrite(path, imageToSave);
                imageToSave.Dispose();

                if (success)
                {
                    Console.WriteLine($"[INFO] Image saved to: {path}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Failed to save image to: {path}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SaveImage exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 异步获取单帧图像
        /// </summary>
        public async Task<Mat?> CaptureAsync()
        {
            return await Task.Run(() =>
            {
                if (Capture(out Mat? mat))
                {
                    return mat;
                }
                return null;
            });
        }

    }
}
