using OpenCvSharp;
using System.Diagnostics;
using System;
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

        /// <summary>
        /// 此处待定
        /// 全局增益【GlobalGain】与图像模式【ImageMode】的组合
        /// </summary>
        public static readonly List<string> ImageReadoutModeSet = new()
        {
            "高动态",//gain=0,imageMode=2
            "高增益",//gain=1,imageMode=2
            "高增益高速",//gain=1,imageMode=3
            "高增益全局重置",//gain=1,imageMode=5
            "低增益",//gain=2,imageMode=2
            "低增益高速",//gain=2,imageMode=4
            "低增益全局重置"//gain=2,imageMode=5
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
        /// 获取相机型号名称
        /// </summary>
        /// <param name="modelName">相机型号</param>
        /// <returns>是否成功</returns>
        public static bool GetCameraModel(ref string modelName)
        {
            IntPtr pText = Marshal.AllocHGlobal(64);
            try
            {
                m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_CAMERA_MODEL;
                m_viCam.pText = pText;
                m_viCam.nTextSize = 64;

                if (AssertRet(TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam)))
                {
                    modelName = Marshal.PtrToStringAnsi(m_viCam.pText) ?? string.Empty;
                    return true;
                }
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(pText);
            }
        }

        /// <summary>
        /// 获取相机序列号
        /// </summary>
        /// <param name="serialNumber">序列号</param>
        /// <returns>是否成功</returns>
        public static bool GetCameraSerialNumber(ref string serialNumber)
        {
            IntPtr pText = Marshal.AllocHGlobal(64);
            try
            {
                m_regRW.nRegType = (int)TUREG_TYPE.TUREG_SN;
                m_regRW.nBufSize = 64;
                m_regRW.pBuf = pText;

                if (AssertRet(TUCamAPI.TUCAM_Reg_Read(m_opCam.hIdxTUCam, m_regRW)))
                {
                    serialNumber = Marshal.PtrToStringAnsi(m_regRW.pBuf) ?? string.Empty;
                    return true;
                }
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(pText);
            }
        }

        /// <summary>
        /// 获取相机 Vendor ID
        /// </summary>
        /// <param name="vendorId">VID值</param>
        /// <returns>是否成功</returns>
        public static bool GetVendorId(ref int vendorId)
        {
            m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_VENDOR;

            if (AssertRet(TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam)))
            {
                vendorId = m_viCam.nValue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取相机 Vendor ID (十六进制字符串格式)
        /// </summary>
        /// <param name="vendorIdHex">VID十六进制字符串 (如 "0x1234")</param>
        /// <returns>是否成功</returns>
        public static bool GetVendorIdHex(ref string vendorIdHex)
        {
            int vid = 0;
            if (GetVendorId(ref vid))
            {
                vendorIdHex = $"0x{vid:X4}";
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取相机 Product ID
        /// </summary>
        /// <param name="productId">PID值</param>
        /// <returns>是否成功</returns>
        public static bool GetProductId(ref int productId)
        {
            m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_PRODUCT;

            if (AssertRet(TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam)))
            {
                productId = m_viCam.nValue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取相机 Product ID (十六进制字符串格式)
        /// </summary>
        /// <param name="productIdHex">PID十六进制字符串 (如 "0x5678")</param>
        /// <returns>是否成功</returns>
        public static bool GetProductIdHex(ref string productIdHex)
        {
            int pid = 0;
            if (GetProductId(ref pid))
            {
                productIdHex = $"0x{pid:X4}";
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取相机通道数
        /// </summary>
        /// <param name="channels">通道数</param>
        /// <returns>是否成功</returns>
        public static bool GetCameraChannels(ref int channels)
        {
            m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_CAMERA_CHANNELS;

            if (AssertRet(TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam)))
            {
                channels = m_viCam.nValue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取 USB 总线类型值
        /// </summary>
        /// <param name="busType">总线类型值</param>
        /// <returns>是否成功</returns>
        public static bool GetBusType(ref int busType)
        {
            m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_BUS;

            if (AssertRet(TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam)))
            {
                busType = m_viCam.nValue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取 USB 类型 (2.0 或 3.0)
        /// </summary>
        /// <param name="usbType">USB类型字符串</param>
        /// <returns>是否成功</returns>
        public static bool GetUsbType(ref string usbType)
        {
            int busType = 0;
            if (GetBusType(ref busType))
            {
                usbType = (busType == 0x200 || busType == 0x210) ? "2.0" : "3.0";
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取固件版本值
        /// </summary>
        /// <param name="firmwareVersion">固件版本值</param>
        /// <returns>是否成功</returns>
        public static bool GetFirmwareVersion(ref int firmwareVersion)
        {
            m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_VERSION_FRMW;

            if (AssertRet(TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam)))
            {
                firmwareVersion = m_viCam.nValue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取固件版本 (十六进制字符串格式)
        /// </summary>
        /// <param name="firmwareVersionHex">固件版本十六进制字符串</param>
        /// <returns>是否成功</returns>
        public static bool GetFirmwareVersionHex(ref string firmwareVersionHex)
        {
            int fwVer = 0;
            if (GetFirmwareVersion(ref fwVer))
            {
                firmwareVersionHex = $"0x{fwVer:X}";
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取 API 版本
        /// </summary>
        /// <param name="apiVersion">API版本字符串</param>
        /// <returns>是否成功</returns>
        public static bool GetApiVersion(ref string apiVersion)
        {
            IntPtr pText = Marshal.AllocHGlobal(64);
            try
            {
                m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_VERSION_API;
                m_viCam.pText = pText;
                m_viCam.nTextSize = 64;

                if (AssertRet(TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam)))
                {
                    apiVersion = Marshal.PtrToStringAnsi(m_viCam.pText) ?? string.Empty;
                    return true;
                }
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(pText);
            }
        }

        /// <summary>
        /// 获取完整的相机信息字典
        /// </summary>
        /// <returns>包含所有相机信息的字典</returns>
        public static Dictionary<string, string> GetCameraInfoDictionary()
        {
            var info = new Dictionary<string, string>();

            string strValue = string.Empty;
            int intValue = 0;

            if (GetCameraModel(ref strValue))
                info["Camera Model"] = strValue;

            if (GetCameraSerialNumber(ref strValue))
                info["Serial Number"] = strValue;

            if (GetVendorIdHex(ref strValue))
                info["Vendor ID"] = strValue;

            if (GetProductIdHex(ref strValue))
                info["Product ID"] = strValue;

            if (GetCameraChannels(ref intValue))
                info["Channels"] = intValue.ToString();

            if (GetUsbType(ref strValue))
                info["USB Type"] = strValue;

            if (GetFirmwareVersionHex(ref strValue))
                info["Firmware Version"] = strValue;

            if (GetApiVersion(ref strValue))
                info["API Version"] = strValue;

            return info;
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
            }

            //尝试开启直方图统计
            //实测：需处于采集状态时才可正常开启，即成功设置TUCAM_Cap_Start后方可开启
            if (!SetHistogramEnable(true)) return false;

            return AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                (int)TUCAM_IDCAPA.TUIDC_ATLEVELS, mode));
        }

        /// <summary>
        /// 获取自动色阶模式
        /// 共0~3四种模式，0为手动，其余为自动
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
            var res = AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam, (int)TUCAM_IDCAPA.TUIDC_ATEXPOSURE_MODE, mode));
            Thread.Sleep(2000);//官方SDK标配，必须要有

            return res;
        }

        /// <summary>
        /// 获取自动曝光状态
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool GetAutoExposure(ref int value)
            => AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam, (int)TUCAM_IDCAPA.TUIDC_ATEXPOSURE, ref value));

        /// <summary>
        /// 获取自动曝光状态
        /// </summary>
        /// <param name="enable"></param>
        /// <returns></returns>
        public static bool GetAutoExposure(out bool enable)
        {
            int value = -1;
            var res = AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam, (int)TUCAM_IDCAPA.TUIDC_ATEXPOSURE, ref value));
            enable = value == 1;
            return res;
        }

        /// <summary>
        /// 获取帧率
        /// 精度 0.1
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool GetFrameRate(ref double value)
        {
            value = 0;
            var rec = AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,   (int)TUCAM_IDPROP.TUIDP_FRAME_RATE, ref value, 0));
            return rec;
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
        /// 无法设置
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
        /// 忽略
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

        private static Thread? _captureThread;
        private static bool _isCapturing = false;
        private static readonly object _captureLock = new object();
        private static bool _bufferAllocated = false;
        private static bool _captureStarted = false;

        // 帧接收事件
        public static event Action<Mat>? FrameReceived;

        /// <summary>
        /// 开始连续视频流采集
        /// </summary>
        /// <param name="mode">采集模式（默认连续流模式）</param>
        public static bool StartCapture(TUCAM_CAPTURE_MODES mode = TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE)
        {
            if (_isCapturing)
            {
                Console.WriteLine("[WARNING] Capture already running");
                return true;
            }

            lock (_captureLock)
            {
                // 初始化帧结构
                m_drawframe.pBuffer = IntPtr.Zero;
                m_drawframe.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_USUAl;
                m_drawframe.uiRsdSize = 1;

                // 获取并设置触发器
                if (!AssertRet(TUCamAPI.TUCAM_Cap_GetTrigger(m_opCam.hIdxTUCam, ref attrTgr)))
                    return false;

                attrTgr.nTgrMode = (int)mode;
                attrTgr.nFrames = 1;
                attrTgr.nBufFrames = 2;

                if (!AssertRet(TUCamAPI.TUCAM_Cap_SetTrigger(m_opCam.hIdxTUCam, attrTgr)))
                    return false;

                // 分配缓冲区
                if (!AssertRet(TUCamAPI.TUCAM_Buf_Alloc(m_opCam.hIdxTUCam, ref m_drawframe)))
                    return false;
                _bufferAllocated = true;

                // 开始采集
                if (!AssertRet(TUCamAPI.TUCAM_Cap_Start(m_opCam.hIdxTUCam, (uint)attrTgr.nTgrMode)))
                {
                    TUCamAPI.TUCAM_Buf_Release(m_opCam.hIdxTUCam);
                    _bufferAllocated = false;
                    return false;
                }
                _captureStarted = true;

                _isCapturing = true;

                // 启动采集线程
                _captureThread = new Thread(CaptureLoop)
                {
                    IsBackground = true,
                    Name = "CameraCapture"
                };
                _captureThread.Start();

                Console.WriteLine("[INFO] Continuous capture started");
                return true;
            }
        }

        /// <summary>
        /// 采集循环线程
        /// </summary>
        private static void CaptureLoop()
        {
            DisplayFrame display = new DisplayFrame();

            while (_isCapturing)
            {
                try
                {
                    TUCAM_FRAME frame = m_drawframe;

                    if (AssertRet(TUCamAPI.TUCAM_Buf_WaitForFrame(m_opCam.hIdxTUCam, ref frame, 1000)))
                    {
                        if (Frame2Bytes(ref display, frame))
                        {
                            display.ToMat(out Mat mat);
                            FrameReceived?.Invoke(mat);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Capture loop exception: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }

        /// <summary>
        /// 停止连续采集
        /// </summary>
        public static bool StopCapture()
        {
            if (!_isCapturing)
                return true;

            lock (_captureLock)
            {
                Console.WriteLine("[INFO] Stopping capture...");
                _isCapturing = false;

                // 等待采集线程结束
                if (_captureThread != null && _captureThread.IsAlive)
                {
                    if (!_captureThread.Join(5000))
                    {
                        Console.WriteLine("[ERROR] Capture thread did not stop in time");
                        return false;
                    }
                }

                bool success = true;

                // 中止等待
                if (!AssertRet(TUCamAPI.TUCAM_Buf_AbortWait(m_opCam.hIdxTUCam), true, true))
                {
                    Console.WriteLine("[WARNING] TUCAM_Buf_AbortWait failed");
                    success = false;
                }

                // 停止采集
                if (_captureStarted)
                {
                    if (!AssertRet(TUCamAPI.TUCAM_Cap_Stop(m_opCam.hIdxTUCam), true, true))
                    {
                        Console.WriteLine("[WARNING] TUCAM_Cap_Stop failed");
                        success = false;
                    }
                    _captureStarted = false;
                }

                // 释放缓冲区
                if (_bufferAllocated)
                {
                    if (!AssertRet(TUCamAPI.TUCAM_Buf_Release(m_opCam.hIdxTUCam), true, true))
                    {
                        Console.WriteLine("[WARNING] TUCAM_Buf_Release failed");
                        success = false;
                    }
                    _bufferAllocated = false;
                }

                _captureThread = null;
                Console.WriteLine($"[INFO] Capture stopped: {(success ? "Success" : "With warnings")}");
                return success;
            }
        }

        /// <summary>
        /// 单张抓图（自动处理视频流状态）
        /// </summary>
        /// <param name="mat">输出的Mat图像</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>是否成功</returns>
        public static bool Capture(out Mat mat, int timeoutMs = 10000)
        {
            mat = new Mat();

            // 如果正在连续采集，直接获取一帧（不加锁，避免阻塞视频流）
            if (_isCapturing)
            {
                try
                {
                    TUCAM_FRAME tempFrame = m_drawframe;

                    if (!AssertRet(TUCamAPI.TUCAM_Buf_WaitForFrame(m_opCam.hIdxTUCam, ref tempFrame, timeoutMs)))
                    {
                        Console.WriteLine("[ERROR] Failed to wait for frame in streaming mode");
                        return false;
                    }

                    DisplayFrame display = new DisplayFrame();
                    if (!Frame2Bytes(ref display, tempFrame))
                    {
                        Console.WriteLine("[ERROR] Failed to convert frame to bytes");
                        return false;
                    }

                    display.ToMat(out mat);
                    Console.WriteLine($"[INFO] Single frame captured from stream: {tempFrame.usWidth}x{tempFrame.usHeight}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Capture exception in streaming mode: {ex.Message}");
                    return false;
                }
            }

            // 视频流未运行，临时启动采集
            lock (_captureLock)
            {
                bool needCleanup = false;
                TUCAM_FRAME tempFrame = default;

                try
                {
                    // 初始化帧结构
                    tempFrame.pBuffer = IntPtr.Zero;
                    tempFrame.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_USUAl;
                    tempFrame.uiRsdSize = 1;

                    // 设置触发器
                    TUCAM_TRIGGER_ATTR tempTrigger = default;
                    if (!AssertRet(TUCamAPI.TUCAM_Cap_GetTrigger(m_opCam.hIdxTUCam, ref tempTrigger)))
                        return false;

                    tempTrigger.nTgrMode = (int)TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE;
                    tempTrigger.nFrames = 1;
                    tempTrigger.nBufFrames = 2;

                    if (!AssertRet(TUCamAPI.TUCAM_Cap_SetTrigger(m_opCam.hIdxTUCam, tempTrigger)))
                        return false;

                    // 分配缓冲区
                    if (!AssertRet(TUCamAPI.TUCAM_Buf_Alloc(m_opCam.hIdxTUCam, ref tempFrame)))
                    {
                        Console.WriteLine("[ERROR] Failed to allocate buffer");
                        return false;
                    }
                    needCleanup = true;

                    // 开始采集
                    if (!AssertRet(TUCamAPI.TUCAM_Cap_Start(m_opCam.hIdxTUCam, (uint)TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE)))
                    {
                        Console.WriteLine("[ERROR] Failed to start capture");
                        return false;
                    }

                    // 等待一帧
                    if (!AssertRet(TUCamAPI.TUCAM_Buf_WaitForFrame(m_opCam.hIdxTUCam, ref tempFrame, timeoutMs)))
                    {
                        Console.WriteLine("[ERROR] Failed to wait for frame in single capture mode");
                        return false;
                    }

                    // 转换为Mat
                    DisplayFrame display = new DisplayFrame();
                    if (!Frame2Bytes(ref display, tempFrame))
                    {
                        Console.WriteLine("[ERROR] Failed to convert frame to bytes");
                        return false;
                    }

                    display.ToMat(out mat);
                    Console.WriteLine($"[INFO] Single frame captured (standalone): {tempFrame.usWidth}x{tempFrame.usHeight}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Capture exception: {ex.Message}");
                    return false;
                }
                finally
                {
                    // 清理临时采集资源
                    if (needCleanup)
                    {
                        try
                        {
                            TUCamAPI.TUCAM_Buf_AbortWait(m_opCam.hIdxTUCam);
                            TUCamAPI.TUCAM_Cap_Stop(m_opCam.hIdxTUCam);
                            TUCamAPI.TUCAM_Buf_Release(m_opCam.hIdxTUCam);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WARNING] Cleanup exception: {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 单张抓图
        /// 异步版本
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>抓取的Mat图像，失败返回null</returns>
        public static async Task<Mat?> CaptureAsync(int timeoutMs = 10000)
        {
            return await Task.Run(() =>
            {
                if (Capture(out Mat mat, timeoutMs))
                {
                    return mat;
                }
                return null;
            });
        }

        /// <summary>
        /// 等待并获取一帧图像（原有方法保持兼容）
        /// </summary>
        /// <param name="frame">帧数据结构</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        public static bool WaitForFrame(ref TUCAM_FRAME frame, int timeoutMs = 10000)
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

        private static bool Frame2Bytes(ref DisplayFrame display, TUCAM_FRAME frame)
        {
            try
            {
                if (frame.pBuffer == IntPtr.Zero) return false;

                int width = frame.usWidth;
                int height = frame.usHeight;
                int stride = (int)frame.uiWidthStep;

                var size = (int)(frame.uiImgSize + frame.usHeader);
                var raw = new byte[size];
                var actualRaw = new byte[frame.uiImgSize];

                Marshal.Copy(frame.pBuffer, raw, 0, size);
                Buffer.BlockCopy(raw, frame.usHeader, actualRaw, 0, (int)frame.uiImgSize);

                display.Height = height;
                display.Width = width;
                display.Stride = stride;
                display.FrameObject = actualRaw;
                display.Depth = frame.ucDepth;
                display.Channels = frame.ucChannels;

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Frame2Bytes: {e.Message}");
                return false;
            }
        }

        #endregion

        #region 图像保存

        /// <summary>
        /// 保存当前帧为图片文件
        /// </summary>
        /// <param name="path">保存路径（SDK只支持\\分隔符）</param>
        /// <param name="formatId">格式ID (0=RAW, 1=TIF, 2=PNG, 3=JPG, 4=BMP)</param>
        public static bool SaveCurrentFrame(TUCAM_FRAME frame, string path, int formatId = 1)
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
        public static bool SetCalculateRoi(TUCAM_IDCROI calcType, bool enable, int width, int height, int hOffset = 0, int vOffset = 0)
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
        public static bool QuickSetupImageEnhancement(int imageMode = 1, int globalGain = 0, bool histogramEnable = true, int autoLevels = 3)
        {
            bool success = true;

            success &= SetImageMode(imageMode);
            success &= SetGlobalGain(globalGain);
            success &= SetHistogramEnable(histogramEnable);

            if (histogramEnable)
            {
                success &= SetAutoLevels(autoLevels);
            }

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
        public static bool QuickCaptureSingleFrame(string savePath = "", int format = 1, int timeoutMs = 10000)
        {
            TUCAM_FRAME frame = default;
            frame.pBuffer = IntPtr.Zero;
            frame.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_USUAl;
            frame.uiRsdSize = 1;

            try
            {
                // 分配缓冲区
                if (!AssertRet(TUCamAPI.TUCAM_Buf_Alloc(m_opCam.hIdxTUCam, ref frame)))
                {
                    Console.WriteLine("[ERROR] Failed to allocate buffer");
                    return false;
                }

                // 开始采集
                if (!AssertRet(TUCamAPI.TUCAM_Cap_Start(m_opCam.hIdxTUCam,
                    (uint)TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE)))
                {
                    Console.WriteLine("[ERROR] Failed to start capture");
                    TUCamAPI.TUCAM_Buf_Release(m_opCam.hIdxTUCam);
                    return false;
                }

                // 启用直方图（如果需要自动色阶）
                TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam, (int)TUCAM_IDCAPA.TUIDC_HISTC, 1);

                // 等待一帧
                if (!AssertRet(TUCamAPI.TUCAM_Buf_WaitForFrame(m_opCam.hIdxTUCam, ref frame, timeoutMs)))
                {
                    Console.WriteLine("[ERROR] Failed to wait for frame");
                    return false;
                }

                Console.WriteLine($"[INFO] Frame captured: {frame.usWidth}x{frame.usHeight},depth={frame.ucDepth}bit, index={frame.uiIndex} ");

                // 保存（如果指定了路径）
                if (!string.IsNullOrEmpty(savePath))
                {
                    if (!SaveCurrentFrame(frame, savePath, format))
                    {
                        Console.WriteLine("[ERROR] Failed to save frame");
                        return false;
                    }
                }

                var display = new DisplayFrame();
                Frame2Bytes(ref display, frame);
                display.ToMat(out Mat matImg);
                matImg.SaveImage(savePath+"_########.tif");
                return true;
            }
            finally
            {
                // 停止采集
                TUCamAPI.TUCAM_Buf_AbortWait(m_opCam.hIdxTUCam);
                TUCamAPI.TUCAM_Cap_Stop(m_opCam.hIdxTUCam);
                TUCamAPI.TUCAM_Buf_Release(m_opCam.hIdxTUCam);
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

        /// <summary>
        /// 快速设置：温度控制
        /// </summary>
        /// <param name="targetTempCelsius">目标温度（摄氏度，范围-50到50）</param>
        /// <param name="fanGear">风扇档位</param>
        public static bool QuickSetupTemperature(double targetTempCelsius = -20, int fanGear = 1)
        {
            bool success = true;

            // 转换温度：实际温度 = SDK值 - 50
            double sdkTemp = targetTempCelsius + 50;

            if (sdkTemp < 0 || sdkTemp > 100)
            {
                Console.WriteLine($"[ERROR] Temperature out of range (-50 to 50°C)");
                return false;
            }

            success &= SetTemperature(sdkTemp);
            success &= SetFanGear(fanGear);

            if (success)
            {
                Console.WriteLine($"[INFO] Temperature control setup: Target={targetTempCelsius}°C, Fan={FanGear[fanGear]}");
            }

            return success;
        }

        /// <summary>
        /// 快速设置：图像处理参数
        /// </summary>
        public static bool QuickSetupImageProcessing(  double blackLevel = 100, double noiseLevel = 1,  double gamma = 100,  double contrast = 128)
        {
            bool success = true;

            success &= SetBlackLevel(blackLevel);
            success &= SetNoiseLevel(noiseLevel);
            success &= SetGamma(gamma);
            success &= SetContrast(contrast);

            if (success)
            {
                Console.WriteLine("[INFO] Image processing setup completed");
            }

            return success;
        }

        /// <summary>
        /// 快速设置：自动曝光
        /// </summary>
        public static bool QuickSetupAutoExposure(  bool enable = true,  int mode = 0,   double brightness = 128)
        {
            bool success = true;

            success &= SetAutoExposure(enable);

            if (enable)
            {
                success &= SetAutoExposureMode(mode);
                if (mode == 0) // 居中模式才需要设置亮度
                {
                    success &= SetBrightness(brightness);
                }
            }

            if (success)
            {
                Console.WriteLine($"[INFO] Auto exposure setup: Enabled={enable}, Mode={mode}, Brightness={brightness}");
            }

            return success;
        }

        #region Valid

        /// <summary>
        /// 验证并打印所有基础设置
        /// </summary>
        public static void ValidateBasicSettings()
        {
            Console.WriteLine("\n=== Validating Basic Settings ===");

            // 分辨率
            int res = 0;
            if (GetResolution(ref res))
            {
                Console.WriteLine($"✓ Resolution: {Resolutions[res]} (ID={res})");
            }

            // 水平翻转
            if (GetHorizontal(out bool hFlip))
            {
                Console.WriteLine($"✓ Horizontal Flip: {(hFlip ? "Enabled" : "Disabled")}");
            }

            // 垂直翻转
            if (GetVertical(out bool vFlip))
            {
                Console.WriteLine($"✓ Vertical Flip: {(vFlip ? "Enabled" : "Disabled")}");
            }

            // 风扇档位
            int fan = 0;
            if (GetFanGear(ref fan))
            {
                Console.WriteLine($"✓ Fan Gear: {FanGear[fan]} (ID={fan})");
            }

            // 曝光时间
            double exposure = 0;
            if (GetExposure(ref exposure))
            {
                Console.WriteLine($"✓ Exposure Time: {exposure} μs ({exposure / 1000.0:F2} ms)");
            }

            // LED状态（只能设置，无法获取）
            Console.WriteLine("  LED: Set only (no get method)");
        }

        /// <summary>
        /// 验证并打印所有图像增强设置
        /// </summary>
        public static void ValidateImageEnhancementSettings()
        {
            Console.WriteLine("\n=== Validating Image Enhancement Settings ===");

            // 图像模式
            int imgMode = 0;
            if (GetImageMode(ref imgMode))
            {
                Console.WriteLine($"✓ Image Mode: {ImageMode[imgMode - 1]} (ID={imgMode})");
            }

            // 全局增益
            double gain = 0;
            if (GetGlobalGain(ref gain))
            {
                int gainId = (int)gain;
                if (gainId >= 0 && gainId < GlobalGain.Count)
                {
                    Console.WriteLine($"✓ Global Gain: {GlobalGain[gainId]} (ID={gainId})");
                }
            }

            // 直方图统计
            bool histEnabled = GetHistogramEnable();
            Console.WriteLine($"✓ Histogram: {(histEnabled ? "Enabled" : "Disabled")}");

            // 自动色阶
            int autoLevel = 0;
            if (GetAutoLevels(ref autoLevel))
            {
                Console.WriteLine($"✓ Auto Levels: {Levels[autoLevel]} (ID={autoLevel})");
            }

            // 左右色阶
            if (GetLeftLevels(out double left) && GetRightLevels(out double right))
            {
                Console.WriteLine($"✓ Levels Range: Left={left}, Right={right}");
            }
        }

        /// <summary>
        /// 验证并打印所有图像处理参数
        /// </summary>
        public static void ValidateImageProcessingSettings()
        {
            Console.WriteLine("\n=== Validating Image Processing Settings ===");

            // 黑电平
            double blackLevel = 0;
            if (GetBlackLevel(ref blackLevel))
            {
                Console.WriteLine($"✓ Black Level: {blackLevel}");
            }

            // 亮度
            double brightness = 0;
            if (GetBrightness(ref brightness))
            {
                Console.WriteLine($"✓ Brightness: {brightness}");
            }

            // 降噪等级
            double noise = 0;
            if (GetNoiseLevel(ref noise))
            {
                Console.WriteLine($"✓ Noise Level: {noise}");
            }

            // 伽马
            if (GetGamma(out double gamma))
            {
                Console.WriteLine($"✓ Gamma: {gamma}");
            }

            // 对比度
            if (GetContrast(out double contrast))
            {
                Console.WriteLine($"✓ Contrast: {contrast}");
            }
        }

        /// <summary>
        /// 验证并打印温度设置
        /// </summary>
        public static void ValidateTemperatureSettings()
        {
            Console.WriteLine("\n=== Validating Temperature Settings ===");

            double tempTarget = 0;
            if (GetTemperature(ref tempTarget))
            {
                double actualTemp = tempTarget - 50;
                Console.WriteLine($"✓ Target Temperature: {actualTemp}°C (Raw={tempTarget})");
            }

            double currentTemp = GetCurrentTemperature();
            Console.WriteLine($"✓ Current Temperature: {currentTemp}°C");
        }

        /// <summary>
        /// 验证并打印ROI设置
        /// </summary>
        public static void ValidateROISettings()
        {
            Console.WriteLine("\n=== Validating ROI Settings ===");

            TUCAM_ROI_ATTR roi = default;
            if (GetRoi(ref roi))
            {
                if (roi.bEnable)
                {
                    Console.WriteLine($"✓ ROI: Enabled");
                    Console.WriteLine($"  Size: {roi.nWidth}x{roi.nHeight}");
                    Console.WriteLine($"  Offset: ({roi.nHOffset}, {roi.nVOffset})");
                }
                else
                {
                    Console.WriteLine($"✓ ROI: Disabled (Full Frame)");
                }
            }
        }

        /// <summary>
        /// 验证并打印触发器设置
        /// </summary>
        public static void ValidateTriggerSettings()
        {
            Console.WriteLine("\n=== Validating Trigger Settings ===");

            TUCAM_TRIGGER_ATTR trigger = default;
            if (GetTrigger(ref trigger))
            {
                Console.WriteLine($"✓ Trigger Mode: {(TUCAM_CAPTURE_MODES)trigger.nTgrMode}");
                Console.WriteLine($"  Frames: {trigger.nFrames}");
                Console.WriteLine($"  Buffer Frames: {trigger.nBufFrames}");
                Console.WriteLine($"  Edge Mode: {trigger.nEdgeMode}");
                Console.WriteLine($"  Exposure Mode: {trigger.nExpMode}");
            }
        }

        /// <summary>
        /// 验证并打印滚动扫描设置
        /// </summary>
        public static void ValidateRollingScanSettings()
        {
            Console.WriteLine("\n=== Validating Rolling Scan Settings ===");

            int mode = 0;
            if (GetRollingScanMode(ref mode))
            {
                Console.WriteLine($"✓ Rolling Scan Mode: {RollingScanMode[mode]} (ID={mode})");
            }
        }

        /// <summary>
        /// 验证所有设置
        /// </summary>
        public static void ValidateAllSettings()
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("COMPLETE CAMERA SETTINGS VALIDATION");
            Console.WriteLine(new string('=', 60));

            ValidateBasicSettings();
            ValidateImageEnhancementSettings();
            ValidateImageProcessingSettings();
            ValidateTemperatureSettings();
            ValidateROISettings();
            ValidateTriggerSettings();
            ValidateRollingScanSettings();

            Console.WriteLine("\n" + new string('=', 60));
        }

        #endregion

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
                Console.WriteLine($"Minimum  : {attr.dbValMin} ms");
                Console.WriteLine($"Maximum  : {attr.dbValMax} ms");
                Console.WriteLine($"Step     : {attr.dbValStep} ms");
                Console.WriteLine($"Default  : {attr.dbValDft} ms");
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
        public static bool QuickSetupBasic(int resolution = 0, bool autoExposure = true, double exposureUs = 1000, int fanGear = 1)
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

    }

    public class DisplayFrame
    {
        public byte[] FrameObject { get; set; } = Array.Empty<byte>();

        public int Height { get; set; } = 0;

        public int Width { get; set; } = 0;

        public int Stride { get; set; } = 0;

        public byte Depth { get; set; } = 0;

        public byte Channels { get; set; } = 0;

        public Mat? Image { get; set; }

        public void ToMat(out Mat mat)
        {
            // 假设 FrameObject 是一个 byte[] 数组
            var data = (byte[])FrameObject;

            // 1. 创建 MatType
            int type = (int)MatType.MakeType(Depth, Channels);

            // 2. 使用 GCHandle 固定托管数组的内存
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);

            try
            {
                // 3. 获取指向内存块的稳定指针 (IntPtr)
                IntPtr ptr = handle.AddrOfPinnedObject();

                // 4. 【已修改】使用推荐的静态方法 Mat.FromPixelData 创建 Mat 对象
                // 这个方法同样不会复制数据，而是直接引用您提供的内存
                mat = Mat.FromPixelData(Height, Width, type, ptr);
            }
            finally
            {
                // 5. 无论成功与否，都必须释放 GCHandle，否则会造成内存泄漏
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }

        }
    }
}
