namespace Dhyana400BSI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("DHYANA 400BSI CAMERA - COMPREHENSIVE USAGE EXAMPLE");
            Console.WriteLine(new string('=', 70));

            // ============ 第一部分：初始化 ============
            Console.WriteLine("\n### PART 1: Initialization ###");

            if (!Dhyana.InitializeSdk())
            {
                Console.WriteLine("[FAILED] SDK initialization failed");
                return;
            }

            if (!Dhyana.InitializeCamera(0))
            {
                Console.WriteLine("[FAILED] Camera initialization failed");
                Dhyana.UninitializeSdk();
                return;
            }

            // ============ 第二部分：基础设置 ============
            Console.WriteLine("\n### PART 2: Basic Settings ###");

            Console.WriteLine("\n--- Setting basic parameters ---");
            Dhyana.QuickSetupBasic(
                resolution: 0,          // 2048x2048 Normal
                autoExposure: false,    // 手动曝光
                exposureUs: 10,      // 50ms
                fanGear: 2              // Medium speed
            );

            // 验证基础设置
            Dhyana.ValidateBasicSettings();

            // ============ 第三部分：图像增强设置 ============
            Console.WriteLine("\n### PART 3: Image Enhancement Settings ###");

            Console.WriteLine("\n--- Setting image enhancement parameters ---");
            Dhyana.QuickSetupImageEnhancement(
                imageMode: 2,           // 图像模式
                globalGain: 0,          // 增益模式
                histogramEnable: true,  // 启用直方图
                autoLevels: 3           // 全自动色阶
            );

            // 验证图像增强设置
            Dhyana.ValidateImageEnhancementSettings();

            //// ============ 第四部分：图像处理参数 ============
            //Console.WriteLine("\n### PART 4: Image Processing Parameters ###");

            //Console.WriteLine("\n--- Setting image processing parameters ---");
            //Dhyana.SetBlackLevel(100);
            //Dhyana.SetBrightness(55);
            //Dhyana.SetNoiseLevel(1);
            //Dhyana.SetGamma(80);
            //Dhyana.SetContrast(125);//继续拓展

            // 验证图像处理设置
            Dhyana.ValidateImageProcessingSettings();

            // ============ 第五部分：温度设置 ============
            Console.WriteLine("\n### PART 5: Temperature Control ###");

            Console.WriteLine("\n--- Setting target temperature ---");
            Dhyana.SetTemperature(30);  // 目标温度 -20°C (30-50=-20)

            // 验证温度设置
            Dhyana.ValidateTemperatureSettings();

            // ============ 第六部分：ROI设置（可选）============
            Console.WriteLine("\n### PART 6: ROI Settings (Optional) ###");

            Console.WriteLine("\n--- Testing ROI ---");
            Dhyana.SetRoi(1024, 1024, 512, 512);
            Dhyana.ValidateROISettings();

            Console.WriteLine("\n--- Disabling ROI ---");
            Dhyana.DisableRoi();
            Dhyana.ValidateROISettings();

            // ============ 第七部分：完整状态检查 ============
            Console.WriteLine("\n### PART 7: Complete Status Check ###");
            Console.WriteLine(Dhyana.GetCameraStatus());

            Dhyana.PrintExposureRange();

            // ============ 第八部分：单帧采集 ============
            Console.WriteLine("\n### PART 8: Single Frame Capture ###");

            string savePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "tucam_test",
                $"single_frame_{DateTime.Now:HHmmss_fff}"
            );

            // 确保目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

            Console.WriteLine($"\n--- Capturing single frame to: {savePath} ---");
            if (Dhyana.QuickCaptureSingleFrame(savePath, format: 1, timeoutMs: 10000))
            {
                Console.WriteLine("[SUCCESS] Single frame captured and saved");
            }
            else
            {
                Console.WriteLine("[FAILED] Single frame capture failed");
            }

            //// ============ 第九部分：连续采集 ============
            //Console.WriteLine("\n### PART 9: Continuous Capture (10 frames) ###");

            //if (Dhyana.StartCapture(TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE))
            //{
            //    // 启用直方图
            //    Dhyana.SetHistogramEnable(true);

            //    Console.WriteLine("\n--- Capturing frames ---");
            //    for (int i = 0; i < 10; i++)
            //    {
            //        TUCAM_FRAME frame = m_drawframe;
            //        if (Dhyana.WaitForFrame(ref frame, 10000))
            //        {
            //            Console.WriteLine($"[{i + 1}/10] Frame captured: " +
            //                $"{frame.usWidth}x{frame.usHeight}, " +
            //                $"depth={frame.ucDepth}bit, " +
            //                $"index={frame.uiIndex}");

            //            // 每3帧保存一次
            //            if ((i + 1) % 3 == 0)
            //            {
            //                string multiSavePath = Path.Combine(
            //                    Path.GetDirectoryName(savePath),
            //                    $"continuous_frame_{i + 1:D3}_{DateTime.Now:fff}.tif"
            //                );
            //                Dhyana.SaveCurrentFrame(frame, multiSavePath, 1);
            //                Console.WriteLine($"    → Saved to: {Path.GetFileName(multiSavePath)}");
            //            }
            //        }
            //        else
            //        {
            //            Console.WriteLine($"[{i + 1}/10] [WARNING] Failed to capture frame");
            //        }

            //        Thread.Sleep(100); // 短暂延迟
            //    }

            //    Dhyana.StopCapture();
            //    Console.WriteLine("[SUCCESS] Continuous capture completed");
            //}
            //else
            //{
            //    Console.WriteLine("[FAILED] Failed to start continuous capture");
            //}

            // ============ 第十部分：完整验证 ============
            Console.WriteLine("\n### PART 10: Complete Settings Validation ###");
            Dhyana.ValidateAllSettings();

            // ============ 第十一部分：清理 ============
            Console.WriteLine("\n### PART 11: Cleanup ###");
            Console.WriteLine("\n--- Closing camera and releasing resources ---");
            Dhyana.UninitializeCamera();
            Dhyana.UninitializeSdk();

            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("EXAMPLE COMPLETED SUCCESSFULLY");
            Console.WriteLine(new string('=', 70));
            //Console.WriteLine($"\nImages saved to: {Path.GetDirectoryName(savePath)}");

            Console.ReadLine();
    }
    }
}
