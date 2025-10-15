using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dhyana400BSI
{
    /// <summary>
    /// Dhyana 400BSI V3 相机SDK封装类
    /// 提供相机初始化、参数设置、图像采集等功能
    /// </summary>
    public static class Dhyana
    {
        #region 私有字段

        private static TUCAM_INIT m_itApi = new();      // SDK初始化结构
        private static TUCAM_OPEN m_opCam;              // 相机打开结构
        private static TUCAM_VALUE_INFO m_viCam;        // 相机信息结构
        private static TUCAM_REG_RW m_regRW;            // 寄存器读写结构
        private static TUCAM_FRAME m_drawframe;         // 帧数据结构
        private static TUCAM_TRIGGER_ATTR attrTgr;      // 触发器属性
        private static TUCAM_ROI_ATTR _cameraRoiAttr;   // ROI属性
        private static TUCAM_REC_SAVE m_rs;             // 录像保存结构
        private static TUCAM_FILE_SAVE m_fs;            // 文件保存结构

        #endregion

        #region 常量定义

        /// <summary>
        /// 支持的分辨率模式，索引对应TUIDC_RESOLUTION的值
        /// </summary>
        public static readonly List<string> Resolutions = new()
        {
            "2048x2048(Normal)",    // 0: 正常模式
            "2048x2048(Enhance)",   // 1: 增强模式
            "1024x1024(2x2Bin)",    // 2: 2x2装箱
            "512x512(4x4Bin)"       // 3: 4x4装箱
        };

        /// <summary>
        /// 风扇转速档位
        /// </summary>
        public static readonly List<string> FanGear = new()
        {
            "High",    // 0: 高速
            "Medium",  // 1: 中速
            "Low",     // 2: 低速
            "Off"      // 3: 关闭
        };

        /// <summary>
        /// 自动色阶模式
        /// </summary>
        public static readonly List<string> Levels = new()
        {
            "Disable Auto Levels",  // 0: 禁用自动色阶
            "Auto Left Levels",     // 1: 自动左色阶
           "Auto Right Levels",    // 2: 自动右色阶
           "Auto Levels",          // 3: 全自动色阶
        };

        /// <summary>
        /// 图像模式（增益相关）
        /// </summary>
        public static readonly List<string> ImageMode = new()
        {
            "CMS",           // 1: 高灵敏度模式 (12Bit)
            "HDR",           // 2: 高动态范围模式 (16Bit)
            "HighSpeedHG",   // 3: 高速高增益模式
            "HighSpeedLG",   // 4: 高速低增益模式
            "GlobalReset"    // 5: 全局复位模式
    };

        /// <summary>
        /// 全局增益模式
        /// 说明：
        /// - HDR模式: 16位，Mode=2, Gain=0
        /// - High gain: 11位，Mode=2/3/5, Gain=1
        /// - Low gain: 11位，Mode=2/4/5, Gain=2
        /// - Raw后缀表示原始数据模式
        /// </summary>
        public static readonly List<string> GlobalGain = new()
        {
            "HDR",              // 0: HDR模式
            "High gain",        // 1: 高增益
            "Low gain",         // 2: 低增益
            "HDR - Raw",        // 3: HDR原始数据
            "High gain - Raw",  // 4: 高增益原始数据
            "Low gain - Raw"    // 5: 低增益原始数据
    };

        /// <summary>
        /// 滚动扫描模式
        /// </summary>
        public static readonly List<string> RollingScanMode = new()
        {
            "Off",        // 0: 关闭
            "线路延时",    // 1: 线路延时模式
            "缝隙高度"     // 2: 缝隙高度模式
        };

        /// <summary>
        /// 图片保存格式
        /// </summary>
        public static readonly List<string> PictureType = new()
        {
            "RAW", "TIF", "PNG", "JPG", "BMP"
        };

        /// <summary>
        /// 保存格式枚举列表
        /// </summary>
        public static readonly List<TUIMG_FORMATS> SaveFormatList = new()
        {
            TUIMG_FORMATS.TUFMT_RAW,
            TUIMG_FORMATS.TUFMT_TIF,
            TUIMG_FORMATS.TUFMT_PNG,
            TUIMG_FORMATS.TUFMT_JPG,
            TUIMG_FORMATS.TUFMT_BMP,
        };

        #endregion

        #region 错误检查和状态断言

        /// <summary>
        /// 断言API调用返回值，并检查初始化和连接状态
        /// </summary>
        /// <param name="ret">API返回值</param>
        /// <param name="assertInit">是否断言SDK已初始化</param>
        /// <param name="assertConnect">是否断言相机已连接</param>
        /// <returns>是否成功</returns>
        private static bool AssertRet(TUCAMRET ret, bool assertInit = true, bool assertConnect = true)
        {
            StackTrace st = new(true);

            // 检查初始化状态
            if (assertInit && !IsInitialized())
            {
                Console.WriteLine($"[ERROR] [{st?.GetFrame(1)?.GetMethod()?.Name}] SDK not initialized");
                return false;
            }

            // 检查连接状态
            if (assertConnect && !IsConnected())
            {
                Console.WriteLine($"[ERROR] [{st?.GetFrame(1)?.GetMethod()?.Name}] Camera not connected");
                return false;
            }

            // 检查返回值
            if (ret != TUCAMRET.TUCAMRET_SUCCESS)
            {
                Console.WriteLine($"[ERROR] [{st?.GetFrame(1)?.GetMethod()?.Name}] {ret}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查SDK是否已初始化
        /// 通过检查相机数量判断，TUCAM_Api_Init后获得camcount
        /// </summary>
        private static bool IsInitialized()
        {
            if (m_itApi.uiCamCount == 0)
            {
                Console.WriteLine("[WARNING] No camera found or SDK not initialized");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 检查相机是否已连接
        /// 检查相机句柄，TUCAM_Dev_Open提供，TUCAM_Dev_Close释放
        /// </summary>
        private static bool IsConnected()
        {
            if (m_opCam.hIdxTUCam == IntPtr.Zero)//|| m_opCam.uiIdxOpen == 0//此处无需考虑uiIdxOpen==0为未连接的情况【实际可能并不是】
            {
                Console.WriteLine("[WARNING] Camera not connected");
                return false;
            }
            return true;
        }

        #endregion

        #region 核心功能 - SDK和相机初始化

        /// <summary>
        /// 初始化SDK
        /// 必须在所有操作之前调用
        /// </summary>
        /// <returns>是否成功</returns>
        public static bool InitializeSdk()
        {
            // 设置配置文件路径
            IntPtr strPath = Marshal.StringToHGlobalAnsi(Environment.CurrentDirectory);

            m_itApi.uiCamCount = 0;
            m_itApi.pstrConfigPath = strPath;

            // 初始化API
            if (!AssertRet(TUCamAPI.TUCAM_Api_Init(ref m_itApi), assertInit: false, assertConnect: false))
            {
                Marshal.FreeHGlobal(strPath);
                return false;
            }

            // 检查是否找到相机
            if (m_itApi.uiCamCount == 0)
            {
                Console.WriteLine("[ERROR] No camera found after SDK initialization");
                Marshal.FreeHGlobal(strPath);
                return false;
            }

            Console.WriteLine($"[INFO] SDK initialized successfully. Found {m_itApi.uiCamCount} camera(s)");
            return true;
        }

        /// <summary>
        /// 反初始化SDK
        /// 释放所有资源，程序退出前调用
        /// </summary>
        public static bool UninitializeSdk()
        {
            bool result = AssertRet(TUCamAPI.TUCAM_Api_Uninit(), assertInit: false, assertConnect: false);
            if (result)
            {
                Console.WriteLine("[INFO] SDK uninitialized successfully");
            }
            return result;
        }

        /// <summary>
        /// 打开指定的相机
        /// </summary>
        /// <param name="cameraId">相机索引，从0开始</param>
        /// <returns>是否成功</returns>
        public static bool InitializeCamera(uint cameraId = 0)
        {
            m_opCam.uiIdxOpen = cameraId;
            bool result = AssertRet(TUCamAPI.TUCAM_Dev_Open(ref m_opCam), assertInit: true, assertConnect: false);

            if (result)
            {
                Console.WriteLine($"[INFO] Camera {cameraId} opened successfully");

                // 打开后可以打印相机信息
                PrintCameraInfo();
            }

            return result;
        }

        /// <summary>
        /// 关闭相机
        /// </summary>
        public static bool UninitializeCamera()
        {
            bool result = AssertRet(TUCamAPI.TUCAM_Dev_Close(m_opCam.hIdxTUCam), assertInit: false, assertConnect: false);
            if (result)
            {
                Console.WriteLine("[INFO] Camera closed successfully");
                m_opCam.hIdxTUCam = IntPtr.Zero;
                m_opCam.uiIdxOpen = 0;
            }
            return result;
        }

        #endregion

        #region 相机信息获取

        /// <summary>
        /// 打印相机详细信息
        /// 包括型号、序列号、VID/PID、USB类型、固件版本等
        /// </summary>
        public static void PrintCameraInfo()
        {
            if (!IsConnected()) return;

            string strVal;
            string strText;
            IntPtr pText = Marshal.AllocHGlobal(64);

            try
            {
                // 相机型号
                m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_CAMERA_MODEL;
                m_viCam.pText = pText;
                m_viCam.nTextSize = 64;
                if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
                {
                    strText = Marshal.PtrToStringAnsi(m_viCam.pText);
                    Console.WriteLine($"Camera Name     : {strText}");
                }

                // 相机序列号
                m_regRW.nRegType = (int)TUREG_TYPE.TUREG_SN;
                m_regRW.nBufSize = 64;
                m_regRW.pBuf = pText;
                if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Reg_Read(m_opCam.hIdxTUCam, m_regRW))
                {
                    strText = Marshal.PtrToStringAnsi(m_regRW.pBuf);
                    Console.WriteLine($"Camera SN       : {strText}");
                }

                // Vendor ID
                m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_VENDOR;
                if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
                {
                    strVal = String.Format("{0:X4}", m_viCam.nValue);
                    Console.WriteLine($"Camera VID      : 0x{strVal}");
                }

                // Product ID
                m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_PRODUCT;
                if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
                {
                    strVal = String.Format("{0:X4}", m_viCam.nValue);
                    Console.WriteLine($"Camera PID      : 0x{strVal}");
                }

                // 通道数
                m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_CAMERA_CHANNELS;
                if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
                {
                    Console.WriteLine($"Camera Channels : {m_viCam.nValue}");
                }

                // USB类型
                m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_BUS;
                if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
                {
                    string usbType = (m_viCam.nValue == 0x200 || m_viCam.nValue == 0x210) ? "2.0" : "3.0";
                    Console.WriteLine($"USB Type        : {usbType}");
                }

                // 固件版本
                m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_VERSION_FRMW;
                if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
                {
                    strVal = String.Format("{0:X}", m_viCam.nValue);
                    Console.WriteLine($"Firmware Version: 0x{strVal}");
                }

                // API版本
                m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_VERSION_API;
                m_viCam.pText = pText;
                m_viCam.nTextSize = 64;
                if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
                {
                    strText = Marshal.PtrToStringAnsi(m_viCam.pText);
                    Console.WriteLine($"API Version     : {strText}");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pText);
            }
        }

        /// <summary>
        /// 获取相机当前温度
        /// </summary>
        /// <returns>温度值（范围-50℃到50℃）</returns>
        public static double GetCurrentTemperature()
        {
            double value = 0;
            if (GetTemperature(ref value))
            {
                // SDK返回的是0-100的值，实际温度为-50到50
                return value - 50;
            }
            return 0;
        }

        #endregion

        #region Capability控制 - 相机能力参数设置

        /// <summary>
        /// 设置分辨率
        /// </summary>
        /// <param name="resId">分辨率ID (0-3)，参考Resolutions列表</param>
        public static bool SetResolution(int resId)
        {
            if (resId < 0 || resId >= Resolutions.Count)
            {
                Console.WriteLine($"[ERROR] Invalid resolution ID: {resId}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_RESOLUTION, resId));
        }

        /// <summary>
        /// 获取当前分辨率设置
        /// </summary>
        public static bool GetResolution(ref int resId)
        {
            return AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_RESOLUTION, ref resId));
        }

        /// <summary>
        /// 设置水平翻转
        /// </summary>
        /// <param name="enable">是否启用水平翻转</param>
        public static bool SetHorizontal(bool enable)
        {
            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_HORIZONTAL, enable ? 1 : 0));
        }

        /// <summary>
        /// 获取水平翻转状态
        /// </summary>
        public static bool GetHorizontal(out bool enabled)
        {
            int val = 0;
            enabled = false;
            bool result = AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_HORIZONTAL, ref val));
            enabled = (val == 1);
            return result;
        }

        /// <summary>
        /// 设置垂直翻转
        /// </summary>
        /// <param name="enable">是否启用垂直翻转</param>
        public static bool SetVertical(bool enable)
        {
            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_VERTICAL, enable ? 1 : 0));
        }

        /// <summary>
        /// 获取垂直翻转状态
        /// </summary>
        public static bool GetVertical(out bool enabled)
        {
            int val = 0;
            enabled = false;
            bool result = AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_VERTICAL, ref val));
            enabled = (val == 1);
            return result;
        }

        /// <summary>
        /// 设置风扇转速档位
        /// </summary>
        /// <param name="gear">档位 (0=高速, 1=中速, 2=低速, 3=关闭)</param>
        public static bool SetFanGear(int gear)
        {
            if (gear < 0 || gear >= FanGear.Count)
            {
                Console.WriteLine($"[ERROR] Invalid fan gear: {gear}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_FAN_GEAR, gear));
        }

        /// <summary>
        /// 获取风扇转速档位
        /// </summary>
        public static bool GetFanGear(ref int gear)
        {
            return AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_FAN_GEAR, ref gear));
        }

        /// <summary>
        /// 设置直方图统计
        /// 注意：必须先启用直方图统计才能使用自动色阶功能
        /// 必须开始采集才可以设置成功,即TUCamAPI.TUCAM_Cap_Start
        /// </summary>
        /// <param name="enable">是否启用</param>
        public static bool SetHistogramEnable(bool enable)
        {
            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_HISTC, enable ? 1 : 0));
        }

        /// <summary>
        /// 获取直方图统计状态
        /// </summary>
        public static bool GetHistogramEnable()
        {
            int val = 0;
            bool result = AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_HISTC, ref val));
            return result && (val == 1);
        }

        /// <summary>
        /// 设置自动色阶模式
        /// 前提：必须先启用直方图统计
        /// </summary>
        /// <param name="mode">模式 (0=禁用, 1=自动左, 2=自动右, 3=全自动)</param>
        public static bool SetAutoLevels(int mode)
        {
            if (mode < 0 || mode >= Levels.Count)
            {
                Console.WriteLine($"[ERROR] Invalid auto levels mode: {mode}");
                return false;
            }

            // 检查直方图是否启用
            if (mode > 0 && !GetHistogramEnable())
            {
                Console.WriteLine("[WARNING] Histogram must be enabled before setting auto levels");
                SetHistogramEnable(true);
            }

            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_ATLEVELS, mode));
        }

        /// <summary>
        /// 获取自动色阶模式
        /// </summary>
        public static bool GetAutoLevels(ref int mode)
        {
            return AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_ATLEVELS, ref mode));
        }

        /// <summary>
        /// 设置图像模式（与增益配合使用）
        /// </summary>
        /// <param name="mode">模式 (1=CMS, 2=HDR, 3=高速HG, 4=高速LG, 5=全局复位)</param>
        public static bool SetImageMode(int mode)
        {
            if (mode < 1 || mode > ImageMode.Count)
            {
                Console.WriteLine($"[ERROR] Invalid image mode: {mode}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_IMGMODESELECT, mode));
        }

        /// <summary>
        /// 获取图像模式
        /// </summary>
        public static bool GetImageMode(ref int mode)
        {
            return AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_IMGMODESELECT, ref mode));
        }

        /// <summary>
        /// 设置LED指示灯状态
        /// </summary>
        public static bool SetLedEnable(bool enable)
        {
            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_ENABLELED, enable ? 1 : 0));
        }

        /// <summary>
        /// 设置时间戳功能
        /// </summary>
        public static bool SetTimestampEnable(bool enable)
        {
            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_ENABLETIMESTAMP, enable ? 1 : 0));
        }

        /// <summary>
        /// 设置触发输出功能
        /// </summary>
        public static bool SetTriggerOutEnable(bool enable)
        {
            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_ENABLETRIOUT, enable ? 1 : 0));
        }

        /// <summary>
        /// 设置滚动扫描模式
        /// </summary>
        /// <param name="mode">模式 (0=关闭, 1=线路延时, 2=缝隙高度)</param>
        public static bool SetRollingScanMode(int mode)
        {
            if (mode < 0 || mode >= RollingScanMode.Count)
            {
                Console.WriteLine($"[ERROR] Invalid rolling scan mode: {mode}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_ROLLINGSCANMODE, mode));
        }

        /// <summary>
        /// 获取滚动扫描模式
        /// </summary>
        public static bool GetRollingScanMode(ref int mode)
        {
            return AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_ROLLINGSCANMODE, ref mode));
        }

        /// <summary>
        /// 设置自动曝光
        /// </summary>
        /// <param name="enable">是否启用自动曝光</param>
        public static bool SetAutoExposure(bool enable)
        {
            var res = AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_ATEXPOSURE, enable ? 1 : 0));
            Thread.Sleep(2000);//官方SDK标配，必须要有
            return res;
        }

        /// <summary>
        /// 设置自动曝光模式
        /// </summary>
        /// <param name="mode">模式 (0=居中曝光, 1=居右曝光)</param>
        public static bool SetAutoExposureMode(int mode)
        {
            if (mode < 0 || mode > 1)
            {
                Console.WriteLine($"[ERROR] Invalid auto exposure mode: {mode}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_ATEXPOSURE_MODE, mode));
        }

        #endregion

        #region Property控制 - 相机属性参数设置

        /// <summary>
        /// 设置全局增益模式
        /// 需要配合SetImageMode使用
        /// </summary>
        /// <param name="gain">增益 (0=HDR, 1=高增益, 2=低增益, 3-5=对应Raw模式)</param>
        public static bool SetGlobalGain(int gain)
        {
            if (gain < 0 || gain >= GlobalGain.Count)
            {
                Console.WriteLine($"[ERROR] Invalid global gain: {gain}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_GLOBALGAIN, gain, 0));
        }

        /// <summary>
        /// 获取全局增益
        /// </summary>
        public static bool GetGlobalGain(ref double gain)
        {
            return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_GLOBALGAIN, ref gain, 0));
        }

        /// <summary>
        /// 设置曝光时间（毫秒）
        /// 范围和步进需要通过GetExposureAttr获取
        /// </summary>
        /// <param name="exposureUs">曝光时间（毫秒）</param>
        public static bool SetExposure(double exposureUs)
        {
            return AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_EXPOSURETM, exposureUs, 0));
        }

        /// <summary>
        /// 获取当前曝光时间
        /// </summary>
        /// <param name="exposureUs">曝光时间（微秒）</param>
        public static bool GetExposure(ref double exposureUs)
        {
            return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_EXPOSURETM, ref exposureUs, 0));
        }

        /// <summary>
        /// 设置自动曝光目标亮度
        /// 范围：20-255，步进：1
        /// 前提：启用自动曝光且模式为居中自动曝光
        /// </summary>
        /// <param name="brightness">目标亮度值</param>
        public static bool SetBrightness(double brightness)
        {
            if (brightness < 20 || brightness > 255)
            {
                Console.WriteLine($"[ERROR] Brightness out of range (20-255): {brightness}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_BRIGHTNESS, brightness, 0));
        }

        /// <summary>
        /// 获取自动曝光目标亮度
        /// </summary>
        public static bool GetBrightness(ref double brightness)
        {
            return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_BRIGHTNESS, ref brightness, 0));
        }

        /// <summary>
        /// 设置黑电平值
        /// 范围：1-8191，步进：1
        /// 用于调整图像最暗部分的偏移量
        /// </summary>
        /// <param name="level">黑电平值</param>
        public static bool SetBlackLevel(double level)
        {
            if (level < 1 || level > 8191)
            {
                Console.WriteLine($"[ERROR] Black level out of range (1-8191): {level}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_BLACKLEVEL, level, 0));
        }

        /// <summary>
        /// 获取黑电平值
        /// </summary>
        public static bool GetBlackLevel(ref double level)
        {
            return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_BLACKLEVEL, ref level, 0));
        }

        /// <summary>
        /// 设置相机目标温度
        /// 范围：0-100（对应实际温度-50℃到50℃）
        /// </summary>
        /// <param name="temperature">温度值（0-100）</param>
        public static bool SetTemperature(double temperature)
        {
            if (temperature < 0 || temperature > 100)
            {
                Console.WriteLine($"[ERROR] Temperature out of range (0-100): {temperature}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_TEMPERATURE, temperature, 0));
        }

        /// <summary>
        /// 获取相机温度设置
        /// </summary>
        public static bool GetTemperature(ref double temperature)
        {
            return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_TEMPERATURE, ref temperature, 0));
        }

        /// <summary>
        /// 设置降噪等级
        /// 范围：0-3，数值越大降噪强度越大
        /// </summary>
        /// <param name="level">降噪等级</param>
        public static bool SetNoiseLevel(double level)
        {
            if (level < 0 || level > 3)
            {
                Console.WriteLine($"[ERROR] Noise level out of range (0-3): {level}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_NOISELEVEL, level, 0));
        }

        /// <summary>
        /// 获取降噪等级
        /// </summary>
        public static bool GetNoiseLevel(ref double level)
        {
            return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_NOISELEVEL, ref level, 0));
        }

        /// <summary>
        /// 设置伽马值
        /// 范围：0-255
        /// 用于调整图像的中间调亮度
        /// </summary>
        /// <param name="gamma">伽马值</param>
        public static bool SetGamma(double gamma)
        {
            if (gamma < 0 || gamma > 255)
            {
                Console.WriteLine($"[ERROR] Gamma out of range (0-255): {gamma}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_GAMMA, gamma, 0));
        }

        /// <summary>
        /// 获取伽马值
        /// </summary>
        public static bool GetGamma(out double gamma)
        {
            gamma = 0;
            return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_GAMMA, ref gamma, 0));
        }

        /// <summary>
        /// 设置对比度
        /// 范围：0-255
        /// </summary>
        /// <param name="contrast">对比度值</param>
        public static bool SetContrast(double contrast)
        {
            if (contrast < 0 || contrast > 255)
            {
                Console.WriteLine($"[ERROR] Contrast out of range (0-255): {contrast}");
                return false;
            }
            return AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_CONTRAST, contrast, 0));
        }

        /// <summary>
        /// 获取对比度
        /// </summary>
        public static bool GetContrast(out double contrast)
        {
            contrast = 0;
            return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_CONTRAST, ref contrast, 0));
        }

        /// <summary>
        /// 设置左色阶（最小亮度映射）
        /// 8位模式：1-255
        /// 16位模式：1-65535
        /// </summary>
        /// <param name="level">左色阶值</param>
        public static bool SetLeftLevels(double level)
        {
            return AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_LFTLEVELS, level, 0));
        }

        /// <summary>
        /// 获取左色阶
        /// </summary>
        public static bool GetLeftLevels(out double level)
        {
            level = 0;
            return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_LFTLEVELS, ref level, 0));
        }

        /// <summary>
        /// 设置右色阶（最大亮度映射）
        /// 8位模式：1-255
        /// 16位模式：1-65535
        /// </summary>
        /// <param name="level">右色阶值</param>
        public static bool SetRightLevels(double level)
        {
            return AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_RGTLEVELS, level, 0));
        }

        /// <summary>
        /// 获取右色阶
        /// </summary>
        public static bool GetRightLevels(out double level)
        {
            level = 0;
            return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDPROP.TUIDP_RGTLEVELS, ref level, 0));
        }

        #endregion

        #region 属性参数范围获取

        /// <summary>
        /// 获取属性的详细信息（范围、步进、默认值）
        /// </summary>
        private static bool GetPropAttr(out TUCAM_PROP_ATTR attr, TUCAM_IDPROP prop)
        {
            attr = default;
            attr.nIdxChn = 0;
            attr.idProp = (int)prop;
            return AssertRet(TUCamAPI.TUCAM_Prop_GetAttr(m_opCam.hIdxTUCam, ref attr));
        }

        /// <summary>
        /// 获取曝光时间的属性参数（最小值、最大值、步进、默认值）
        /// </summary>
        public static bool GetExposureAttr(out TUCAM_PROP_ATTR attr)
        {
            return GetPropAttr(out attr, TUCAM_IDPROP.TUIDP_EXPOSURETM);
        }

        /// <summary>
        /// 获取亮度属性参数
        /// </summary>
        public static bool GetBrightnessAttr(out TUCAM_PROP_ATTR attr)
        {
            return GetPropAttr(out attr, TUCAM_IDPROP.TUIDP_BRIGHTNESS);
        }

        /// <summary>
        /// 获取黑电平属性参数
        /// </summary>
        public static bool GetBlackLevelAttr(out TUCAM_PROP_ATTR attr)
        {
            return GetPropAttr(out attr, TUCAM_IDPROP.TUIDP_BLACKLEVEL);
        }

        #endregion

        #region ROI（感兴趣区域）设置

        /// <summary>
        /// 设置ROI区域
        /// 注意：这是SDK层面的设置，会改变相机实际输出的数据范围
        /// 所有参数必须是4的倍数
        /// </summary>
        /// <param name="width">宽度（必须是4的倍数）</param>
        /// <param name="height">高度（必须是4的倍数）</param>
        /// <param name="hOffset">水平偏移（必须是4的倍数）</param>
        /// <param name="vOffset">垂直偏移（必须是4的倍数）</param>
        public static bool SetRoi(int width, int height, int hOffset = 0, int vOffset = 0)
        {
            // 确保参数是4的倍数（右移2位再左移2位）
            _cameraRoiAttr.bEnable = true;
            _cameraRoiAttr.nHOffset = (hOffset >> 2) << 2;
            _cameraRoiAttr.nVOffset = (vOffset >> 2) << 2;
            _cameraRoiAttr.nWidth = (width >> 2) << 2;
            _cameraRoiAttr.nHeight = (height >> 2) << 2;

            bool result = AssertRet(TUCamAPI.TUCAM_Cap_SetROI(m_opCam.hIdxTUCam, _cameraRoiAttr));

            if (result)
            {
                // 验证设置是否成功
                result = GetRoi(ref _cameraRoiAttr);
                if (result)
                {
                    Console.WriteLine($"[INFO] ROI set: {_cameraRoiAttr.nWidth}x{_cameraRoiAttr.nHeight} " +
                                    $"at ({_cameraRoiAttr.nHOffset}, {_cameraRoiAttr.nVOffset})");
                }
            }

            return result;
        }

        /// <summary>
        /// 禁用ROI，恢复全画幅输出
        /// </summary>
        public static bool DisableRoi()
        {
            _cameraRoiAttr.bEnable = false;
            bool result = AssertRet(TUCamAPI.TUCAM_Cap_SetROI(m_opCam.hIdxTUCam, _cameraRoiAttr));

            if (result)
            {
                result = GetRoi(ref _cameraRoiAttr);
                if (result)
                {
                    Console.WriteLine("[INFO] ROI disabled, full frame output restored");
                }
            }

            return result;
        }

        /// <summary>
        /// 获取当前ROI设置
        /// </summary>
        public static bool GetRoi(ref TUCAM_ROI_ATTR attr)
        {
            return AssertRet(TUCamAPI.TUCAM_Cap_GetROI(m_opCam.hIdxTUCam, ref attr));
        }

        #endregion

        #region 触发器设置

        /// <summary>
        /// 设置触发模式
        /// </summary>
        /// <param name="mode">触发模式</param>
        public static bool SetTriggerMode(TUCAM_CAPTURE_MODES mode)
        {
            attrTgr.nTgrMode = (int)mode;
            return AssertRet(TUCamAPI.TUCAM_Cap_SetTrigger(m_opCam.hIdxTUCam, attrTgr));
        }

        /// <summary>
        /// 获取触发器设置
        /// </summary>
        public static bool GetTrigger(ref TUCAM_TRIGGER_ATTR trigger)
        {
            return AssertRet(TUCamAPI.TUCAM_Cap_GetTrigger(m_opCam.hIdxTUCam, ref trigger));
        }

        /// <summary>
        /// 执行软件触发
        /// 仅在软件触发模式下有效
        /// </summary>
        public static bool DoSoftwareTrigger()
        {
            return AssertRet(TUCamAPI.TUCAM_Cap_DoSoftwareTrigger(m_opCam.hIdxTUCam));
        }

        #endregion

        #region 图像采集控制

        /// <summary>
        /// 开始图像采集
        /// </summary>
        /// <param name="mode">采集模式（0=连续流模式，1=软件触发）</param>
        public static bool StartCapture(TUCAM_CAPTURE_MODES mode = TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE)
        {
            // 初始化帧结构
            m_drawframe.pBuffer = IntPtr.Zero;
            m_drawframe.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_USUAl;
            m_drawframe.uiRsdSize = 1;

            // 获取当前触发设置
            if (!AssertRet(TUCamAPI.TUCAM_Cap_GetTrigger(m_opCam.hIdxTUCam, ref attrTgr)))
                return false;

            // 设置触发模式
            attrTgr.nTgrMode = (int)mode;

            if (mode == TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE)
            {
                // 连续模式必须设置为1帧
                attrTgr.nFrames = 1;
                attrTgr.nBufFrames = 2;
            }

            if (!AssertRet(TUCamAPI.TUCAM_Cap_SetTrigger(m_opCam.hIdxTUCam, attrTgr)))
                return false;

            // 分配缓冲区
            if (!AssertRet(TUCamAPI.TUCAM_Buf_Alloc(m_opCam.hIdxTUCam, ref m_drawframe)))
                return false;

            // 开始采集
            bool result = AssertRet(TUCamAPI.TUCAM_Cap_Start(m_opCam.hIdxTUCam, (uint)attrTgr.nTgrMode));

            if (result)
            {
                Console.WriteLine($"[INFO] Capture started in {mode} mode");
            }

            return result;
        }

        /// <summary>
        /// 停止图像采集
        /// </summary>
        public static bool StopCapture()
        {
            // 中止等待
            TUCamAPI.TUCAM_Buf_AbortWait(m_opCam.hIdxTUCam);

            // 停止采集
            bool result = AssertRet(TUCamAPI.TUCAM_Cap_Stop(m_opCam.hIdxTUCam));

            // 释放缓冲区
            TUCamAPI.TUCAM_Buf_Release(m_opCam.hIdxTUCam);

            if (result)
            {
                Console.WriteLine("[INFO] Capture stopped");
            }

            return result;
        }

        /// <summary>
        /// 等待并获取一帧图像
        /// </summary>
        /// <param name="frame">帧数据结构</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        public static bool WaitForFrame(ref TUCAM_FRAME frame, int timeoutMs = 1000)
        {
            return AssertRet(TUCamAPI.TUCAM_Buf_WaitForFrame(m_opCam.hIdxTUCam, ref frame, timeoutMs));
        }

        /// <summary>
        /// 复制当前帧数据
        /// </summary>
        public static bool CopyFrame(ref TUCAM_FRAME frame)
        {
            return AssertRet(TUCamAPI.TUCAM_Buf_CopyFrame(m_opCam.hIdxTUCam, ref frame));
        }

        #endregion

        #region 图像保存

        /// <summary>
        /// 保存当前帧为图片文件
        /// </summary>
        /// <param name="path">保存路径（SDK只支持\\分隔符）</param>
        /// <param name="formatId">格式ID (0=RAW, 1=TIF, 2=PNG, 3=JPG, 4=BMP)</param>
        public static bool SaveCurrentFrame(TUCAM_FRAME frame,string path, int formatId = 1)
        {
            if (formatId < 0 || formatId >= SaveFormatList.Count)
            {
                Console.WriteLine($"[ERROR] Invalid format ID: {formatId}");
                return false;
            }

            // SDK要求路径使用\\分隔符
            path = path.Replace("/", "\\");

            TUCAM_FILE_SAVE fSave;
            fSave.pstrSavePath = Marshal.StringToHGlobalAnsi(path);
            fSave.pFrame = Marshal.AllocHGlobal(Marshal.SizeOf(frame));
            fSave.nSaveFmt = (int)SaveFormatList[formatId];

            try
            {
                // RAW格式特殊处理
                if (formatId == 0)
                {
                    frame.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_RAW;
                }
                else
                {
                    frame.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_USUAl;
                }

                // 复制当前帧
                if (!AssertRet(TUCamAPI.TUCAM_Buf_CopyFrame(m_opCam.hIdxTUCam, ref frame)))
                {
                    return false;
                }

                // 更新帧指针
                Marshal.StructureToPtr(frame, fSave.pFrame, true);

                // 保存图像
                bool result = AssertRet(TUCamAPI.TUCAM_File_SaveImage(m_opCam.hIdxTUCam, fSave));

                if (result)
                {
                    Console.WriteLine($"[INFO] Image saved: {path}");
                }

                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(fSave.pstrSavePath);
                Marshal.FreeHGlobal(fSave.pFrame);
            }
        }

        #endregion

        #region 视频录制

        /// <summary>
        /// 录制视频
        /// </summary>
        /// <param name="path">保存路径</param>
        /// <param name="frameCount">录制帧数</param>
        /// <param name="fps">帧率</param>
        /// <param name="timeoutMs">每帧超时时间（毫秒）</param>
        public static bool RecordVideo(string path, int frameCount = 50, float fps = 25f, int timeoutMs = 1000)
        {
            path = path.Replace("/", "\\");

            TUCAM_FRAME frame = default;
            frame.pBuffer = IntPtr.Zero;
            frame.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_USUAl;
            frame.uiRsdSize = 1;

            TUCAM_REC_SAVE rec = default;
            rec.fFps = fps;
            rec.nCodec = 0;
            rec.pstrSavePath = Marshal.StringToHGlobalAnsi(path);

            try
            {
                // 分配缓冲区
                if (!AssertRet(TUCamAPI.TUCAM_Buf_Alloc(m_opCam.hIdxTUCam, ref frame)))
                    return false;

                // 开始采集
                if (!AssertRet(TUCamAPI.TUCAM_Cap_Start(m_opCam.hIdxTUCam,
                    (uint)TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE)))
                {
                    TUCamAPI.TUCAM_Buf_Release(m_opCam.hIdxTUCam);
                    return false;
                }

                // 开始录制
                if (!AssertRet(TUCamAPI.TUCAM_Rec_Start(m_opCam.hIdxTUCam, rec)))
                {
                    StopCapture();
                    return false;
                }

                Console.WriteLine($"[INFO] Recording video: {frameCount} frames at {fps} fps");

                // 采集并添加帧
                for (int i = 0; i < frameCount; i++)
                {
                    if (AssertRet(TUCamAPI.TUCAM_Buf_WaitForFrame(m_opCam.hIdxTUCam, ref frame, timeoutMs)))
                    {
                        TUCamAPI.TUCAM_Rec_AppendFrame(m_opCam.hIdxTUCam, ref frame);

                        if ((i + 1) % 10 == 0)
                        {
                            Console.WriteLine($"[INFO] Recorded {i + 1}/{frameCount} frames");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] Failed to capture frame {i + 1}");
                    }
                }

                // 停止录制
                TUCamAPI.TUCAM_Rec_Stop(m_opCam.hIdxTUCam);
                Console.WriteLine($"[INFO] Video saved: {path}");

                // 停止采集
                StopCapture();

                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(rec.pstrSavePath);
            }
        }

        #endregion

        #region 装箱（Binning）设置

        /// <summary>
        /// 设置装箱模式
        /// </summary>
        /// <param name="enable">是否启用</param>
        /// <param name="mode">装箱模式</param>
        /// <param name="width">装箱宽度</param>
        /// <param name="height">装箱高度</param>
        public static bool SetBinning(bool enable, int mode = 0, int width = 2, int height = 2)
        {
            TUCAM_BIN_ATTR binAttr = default;
            binAttr.bEnable = enable;
            binAttr.nMode = mode;
            binAttr.nWidth = width;
            binAttr.nHeight = height;

            bool result = AssertRet(TUCamAPI.TUCAM_Cap_SetBIN(m_opCam.hIdxTUCam, binAttr));

            if (result && enable)
            {
                Console.WriteLine($"[INFO] Binning enabled: {width}x{height}, mode={mode}");
            }
            else if (result)
            {
                Console.WriteLine("[INFO] Binning disabled");
            }

            return result;
        }

        /// <summary>
        /// 获取装箱设置
        /// </summary>
        public static bool GetBinning(ref TUCAM_BIN_ATTR binAttr)
        {
            return AssertRet(TUCamAPI.TUCAM_Cap_GetBIN(m_opCam.hIdxTUCam, ref binAttr));
        }

        #endregion

        #region 高级功能

        /// <summary>
        /// 设置计算ROI（用于自动白平衡、自动对焦等）
        /// </summary>
        public static bool SetCalculateRoi(TUCAM_IDCROI calcType, bool enable,
            int width, int height, int hOffset = 0, int vOffset = 0)
        {
            TUCAM_CALC_ROI_ATTR calcRoi = default;
            calcRoi.bEnable = enable;
            calcRoi.idCalc = (int)calcType;
            calcRoi.nHOffset = (hOffset >> 2) << 2;
            calcRoi.nVOffset = (vOffset >> 2) << 2;
            calcRoi.nWidth = (width >> 2) << 2;
            calcRoi.nHeight = (height >> 2) << 2;

            return AssertRet(TUCamAPI.TUCAM_Calc_SetROI(m_opCam.hIdxTUCam, calcRoi));
        }

        /// <summary>
        /// 获取指定位置的灰度值
        /// </summary>
        public static bool GetGrayValue(int x, int y, ref short value)
        {
            return AssertRet(TUCamAPI.TUCAM_Get_GrayValue(m_opCam.hIdxTUCam, x, y, ref value));
        }

        #endregion

        #region 便捷方法

        /// <summary>
        /// 快速设置：图像增强
        /// </summary>
        public static bool QuickSetupImageEnhancement(int imageMode = 1, int globalGain = 0,   bool histogramEnable = true, int autoLevels = 3)
        {
            bool success = true;

            success &= SetImageMode(imageMode);
            success &= SetGlobalGain(globalGain);
            //success &= SetHistogramEnable(histogramEnable);

            //if (histogramEnable)
            //{
            //    success &= SetAutoLevels(autoLevels);
            //}

            if (success)
            {
                Console.WriteLine("[INFO] Image enhancement setup completed");
            }

            return success;
        }

        /// <summary>
        /// 快速单帧采集
        /// </summary>
        /// <param name="savePath">保存路径（可选）</param>
        /// <param name="format">保存格式（1=TIF）</param>
        /// <param name="timeoutMs">超时时间</param>
        public static bool QuickCaptureSingleFrame(string savePath = null, int format = 1, int timeoutMs = 10000)
        {
            // 确保之前的资源已经清理
            try
            {
                // 如果有残留的缓冲区，先释放
                TUCamAPI.TUCAM_Buf_Release(m_opCam.hIdxTUCam);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清理残留缓冲区时出错: {ex.Message}");
            }

            // 开始采集
            if (!StartCapture(TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE))
                return false;

            try
            {
                var res = TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam, (int)TUCAM_IDCAPA.TUIDC_HISTC, 1);
                Console.WriteLine("TUIDC_HISTC_" + res);

                // 等待一帧
                if (!WaitForFrame(ref m_drawframe, timeoutMs))
                {
                    Console.WriteLine("[ERROR] Failed to capture frame");
                    return false;
                }

                Console.WriteLine($"[INFO] Frame captured: {m_drawframe.usWidth}x{m_drawframe.usHeight}, " +
                                $"depth={m_drawframe.ucDepth}bit, index={m_drawframe.uiIndex}");

                // 保存（如果指定了路径）
                if (!string.IsNullOrEmpty(savePath))
                {
                    return SaveCurrentFrame(m_drawframe, savePath, format);
                }

                return true;
            }
            finally
            {
                StopCapture();
            }
        }

        /// <summary>
        /// 获取完整的相机状态信息
        /// </summary>
        public static string GetCameraStatus()
        {
            if (!IsConnected())
                return "Camera not connected";

            var status = new System.Text.StringBuilder();
            status.AppendLine("=== Camera Status ===");

            // 分辨率
            int res = 0;
            if (GetResolution(ref res))
                status.AppendLine($"Resolution: {Resolutions[res]}");

            // 曝光
            double exposure = 0;
            if (GetExposure(ref exposure))
                status.AppendLine($"Exposure: {exposure} ms");

            // 增益
            double gain = 0;
            if (GetGlobalGain(ref gain))
                status.AppendLine($"Global Gain: {GlobalGain[(int)gain]}");

            // 温度
            double temp = 0;
            if (GetTemperature(ref temp))
                status.AppendLine($"Temperature: {temp - 50}°C (target)");

            // 风扇
            int fan = 0;
            if (GetFanGear(ref fan))
                status.AppendLine($"Fan Gear: {FanGear[fan]}");

            // 图像模式
            int mode = 0;
            if (GetImageMode(ref mode))
                status.AppendLine($"Image Mode: {ImageMode[mode - 1]}");

            // ROI
            TUCAM_ROI_ATTR roi = default;
            if (GetRoi(ref roi))
            {
                if (roi.bEnable)
                    status.AppendLine($"ROI: {roi.nWidth}x{roi.nHeight} at ({roi.nHOffset},{roi.nVOffset})");
                else
                    status.AppendLine("ROI: Disabled (Full Frame)");
            }

            // 色阶
            double left = 0, right = 0;
            if (GetLeftLevels(out left) && GetRightLevels(out right))
                status.AppendLine($"Levels: Left={left}, Right={right}");

            // 降噪
            double noise = 0;
            if (GetNoiseLevel(ref noise))
                status.AppendLine($"Noise Level: {noise}");

            return status.ToString();
        }

        #endregion

        #region 辅助工具方法

        /// <summary>
        /// 打印曝光时间的有效范围
        /// </summary>
        public static void PrintExposureRange()
        {
            if (GetExposureAttr(out TUCAM_PROP_ATTR attr))
            {
                Console.WriteLine("=== Exposure Time Range ===");
                Console.WriteLine($"Minimum  : {attr.dbValMin} μs");
                Console.WriteLine($"Maximum  : {attr.dbValMax} μs");
                Console.WriteLine($"Step     : {attr.dbValStep} μs");
                Console.WriteLine($"Default  : {attr.dbValDft} μs");
            }
        }

        /// <summary>
        /// 验证参数是否为4的倍数（ROI要求）
        /// </summary>
        private static bool IsMultipleOf4(int value)
        {
            return (value & 0x3) == 0;
        }

        /// <summary>
        /// 调整值为4的倍数
        /// </summary>
        private static int AdjustToMultipleOf4(int value)
        {
            return (value >> 2) << 2;
        }

        /// <summary>
        /// 设置：基础配置
        /// </summary>
        /// <param name="resolution"></param>
        /// <param name="autoExposure"></param>
        /// <param name="exposureUs"></param>
        /// <param name="fanGear"></param>
        /// <returns></returns>
        public static bool QuickSetupBasic(int resolution = 0, bool autoExposure = true,  double exposureUs = 1000, int fanGear = 1)
        {
            bool success = true;

            success &= SetResolution(resolution);
            success &= SetFanGear(fanGear);
            success &= SetAutoExposure(autoExposure);

            if (!autoExposure)
            {
                success &= SetExposure(exposureUs);
            }

            if (success)
            {
                Console.WriteLine("[INFO] Basic setup completed");
            }

            return success;
        }

        #endregion

        #region 使用示例

        /// <summary>
        /// 完整的使用示例
        /// </summary>
        public static void UsageExample()
        {
            Console.WriteLine("=== Dhyana 400BSI Camera Usage Example ===\n");

            // 1. 初始化SDK
            if (!InitializeSdk())
            {
                Console.WriteLine("Failed to initialize SDK");
                return;
            }

            // 2. 打开相机
            if (!InitializeCamera(0))
            {
                Console.WriteLine("Failed to open camera");
                UninitializeSdk();
                return;
            }

            // 3. 基础配置
            Console.WriteLine("\n--- Setting up camera ---");
            QuickSetupBasic(
                resolution: 0,        // 2048x2048 Normal=0
                autoExposure: false,
                exposureUs: 60,    // 50ms= 50000
                fanGear: 2            // Medium=1
            );

            // 4. 图像增强设置
            QuickSetupImageEnhancement(
                imageMode: 2,         // HDR
                globalGain: 0       // High gain

                //histogramEnable: true,
                //autoLevels: 3         // 全自动色阶
            );

            // 5. 设置ROI（可选）
            // SetRoi(1024, 1024, 512, 512);

            // 6. 打印当前状态
            Console.WriteLine("\n" + GetCameraStatus());

            // 7. 采集单帧
            Console.WriteLine("\n--- Capturing single frame ---");
            var file = @"C:\Users\Administrator\Desktop\tucamtest\" + $"{DateTime.Now.ToString("HH-mm-ss-fff")}.tif";
            QuickCaptureSingleFrame(file, format: 1);//"./test_image.tif"

            //// 8. 录制视频（可选）
            //// Console.WriteLine("\n--- Recording video ---");
            //// RecordVideo("./test_video.avi", frameCount: 100, fps: 25f);

            // 9. 连续采集示例
            Console.WriteLine("\n--- Continuous capture (10 frames) ---");
            if (StartCapture(TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE))
            {
                var res = TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam, (int)TUCAM_IDCAPA.TUIDC_HISTC, 1);
                Console.WriteLine("TUIDC_HISTC_" + res);

                for (int i = 0; i < 3; i++)
                {
                    if (WaitForFrame(ref m_drawframe, 10000))
                    {
                        Console.WriteLine($"Frame {i + 1}: {m_drawframe.usWidth}x{m_drawframe.usHeight},index={m_drawframe.uiIndex}");
                    }
                }

                StopCapture();
            }

            // 10. 清理资源
            Console.WriteLine("\n--- Cleaning up ---");
            UninitializeCamera();
            UninitializeSdk();

            Console.WriteLine("\n=== Example completed ===");
        }

        #endregion

    }
}
