using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dhyana400BSI;

//Dhyana400BSIV3

public static class DhyanaBackup
{
    #region assert

    //TODO 
    // 判断是否连接相机
    // 判断是否初始化

    private static bool AssertRet(TUCAMRET ret, bool assertInit = true, bool assertConnect = true)
    {
        StackTrace st = new(true);

        if (assertInit && !IsInitialized()) return false;

        if (assertConnect && !IsConnected()) return false;

        if (ret != TUCAMRET.TUCAMRET_SUCCESS)
        {
            Console.WriteLine($"[ERROR] [{st?.GetFrame(1)?.GetMethod()?.Name}] {ret}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// check uiCamCount
    /// 初始化获得
    /// TUCAM_Api_Init后获得camcount，TUCAM_Api_Uninit后uiCamCount不变
    /// </summary>
    /// <returns></returns>
    private static bool IsInitialized()
    {
        if (m_itApi.uiCamCount == 0)
        {
            Console.WriteLine("No camera found");
            return false;
        }

        return true;
    }

    /// <summary>
    /// check hIdxTUCam
    /// 检查相机句柄，TUCAM_Dev_Open 函数提供
    /// TUCAM_Dev_Release 函数释放句柄
    /// 开启相机时，m_opCam.uiIdxOpen==1，关闭时为0
    /// </summary>
    /// <returns></returns>
    private static bool IsConnected()
    {
        if (m_opCam.hIdxTUCam == IntPtr.Zero||m_opCam.uiIdxOpen==0)
        {
            Console.WriteLine("No camera connected");
            return false;
        }

        return true;
    }


    #endregion

    #region core

    private static TUCAM_INIT m_itApi = new();
    private static TUCAM_OPEN m_opCam;

    public static bool InitializeSdk()
    {
        IntPtr strPath = Marshal.StringToHGlobalAnsi(Environment.CurrentDirectory);

        m_itApi.uiCamCount = 0;
        m_itApi.pstrConfigPath = strPath;

        if (!AssertRet(TUCamAPI.TUCAM_Api_Init(ref m_itApi), assertConnect: false)) return false;

        if (m_itApi.uiCamCount == 0)
        {
            Console.WriteLine("No camera found");
            return false;
        }

        return true;
    }

    public static bool UninitializeSdk()
        => AssertRet(TUCamAPI.TUCAM_Api_Uninit(), false, false);

    public static bool InitializeCamera(uint cameraId)
    {
        m_opCam.uiIdxOpen = cameraId;
        return AssertRet(TUCamAPI.TUCAM_Dev_Open(ref m_opCam), true, false);
    }

    public static bool UnInitializeCamera()
        => AssertRet(TUCamAPI.TUCAM_Dev_Close(m_opCam.hIdxTUCam), false, false);

    #endregion

    #region Info

    //Camera Name    : Dhyana 400BSI V3
    //Camera SN      : RBSD14725034
    //Camera  VID     : 0x5453
    //Camera PID     : 0xE419
    //Camera Channels: 1
    //USB Type    : 3.0
    //Version Firmware: 0x0
    //Version API     : 2.0.7.0

    public static TUCAM_VALUE_INFO m_viCam; // Value info object
    public static TUCAM_REG_RW m_regRW;     // Register read/write

    static void PrintCameraInfo()
    {
        string strVal;
        string strText;
        IntPtr pText = Marshal.AllocHGlobal(64);

        m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_CAMERA_MODEL;
        if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
        {
            strText = Marshal.PtrToStringAnsi(m_viCam.pText);

            Console.WriteLine("Camera  Name    : {0}", strText);
        }

        m_regRW.nRegType = (int)TUREG_TYPE.TUREG_SN;
        m_regRW.nBufSize = 64;
        m_regRW.pBuf = pText;

        if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Reg_Read(m_opCam.hIdxTUCam, m_regRW))
        {
            strText = Marshal.PtrToStringAnsi(m_regRW.pBuf);
            Console.WriteLine("Camera  SN      : {0}", strText);
        }

        m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_VENDOR;
        if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
        {
            strVal = String.Format("{0:X000}", m_viCam.nValue);
            Console.WriteLine("Camera  VID     : 0x{0}", strVal);
        }

        m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_PRODUCT;
        if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
        {
            strVal = String.Format("{0:X000}", m_viCam.nValue);
            Console.WriteLine("Camera  PID     : 0x{0}", strVal);
        }

        m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_CAMERA_CHANNELS;
        if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
        {
            Console.WriteLine("Camera  Channels: {0}", m_viCam.nValue);
        }

        m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_BUS;
        if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
        {
            if (0x200 == m_viCam.nValue || 0x210 == m_viCam.nValue)
            {
                Console.WriteLine("USB     Type    : {0}", "2.0");
            }
            else
            {
                Console.WriteLine("USB     Type    : {0}", "3.0");
            }
        }

        m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_VERSION_FRMW;
        if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
        {
            strVal = String.Format("{0:X000}", m_viCam.nValue);
            Console.WriteLine("Version Firmware: 0x{0}", strVal);
        }

        m_viCam.nID = (int)TUCAM_IDINFO.TUIDI_VERSION_API;
        if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Dev_GetInfo(m_opCam.hIdxTUCam, ref m_viCam))
        {
            strText = Marshal.PtrToStringAnsi(m_viCam.pText);
            Console.WriteLine("Version API     : {0}", strText);
        }

        Marshal.Release(pText);
    }

    #endregion

    #region Trigger

    /**
     * TODO
     * 触发器常用功能
     */

    #endregion

    #region Capa

    //位深-固定16bit

    /// <summary>
    /// 支持的分辨率，对应0-3
    /// </summary>
    public static readonly List<string> Resolutions = new()
    {
        "2048x2048(Normal)", "2048x2048(Enhance)", "1024x1024(2x2Bin)", "512x512(4x4Bin)"
    };

    /// <summary>
    /// 设置分辨率
    /// </summary>
    /// <param name="resId"></param>
    /// <returns></returns>
    public static bool SetResolution(int resId)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_RESOLUTION, resId));

    /// <summary>
    /// 获取分辨率
    /// </summary>
    /// <param name="resId"></param>
    /// <returns></returns>
    public static bool GetResolution(ref int resId)
        => AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_RESOLUTION, ref resId));

    /// <summary>
    /// 设置是否水平翻转
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetHorizontal(bool value)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_HORIZONTAL, value ? 1 : 0));

    /// <summary>
    /// 获取是否水平翻转
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetHorizontal(out bool value)
    {
        int val = 0;
        value = false;

        bool rec = AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_HORIZONTAL, ref val));

        value = val == 0;
        return rec;
    }

    /// <summary>
    /// 设置是否垂直翻转
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetVertical(bool value)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_VERTICAL, value ? 1 : 0));

    /// <summary>
    /// 获取是否垂直翻转
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetVertical(out bool value)
    {
        int val = 0;
        value = false;

        bool rec = AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_VERTICAL, ref val));

        value = val == 0;
        return rec;
    }

    /// <summary>
    /// 风扇模式，对应0-3
    /// </summary>
    public static List<string> FanGear = new()
    {
        "High", "Medium", "Low", "Off"
    };

    /// <summary>
    /// 设置风扇转速
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetFanGear(int value)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_FAN_GEAR, value));

    /// <summary>
    /// 获取风扇转速
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetFanGear(ref int value)
        => AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_FAN_GEAR, ref value));


    /// <summary>
    /// 是否开启直方图数据统计
    /// 只有开启了才能够设置自动色阶
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetHistc(bool value)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                       (int)TUCAM_IDCAPA.TUIDC_HISTC, value ? 1 : 0));

    /// <summary>
    /// 获取直方图数据统计是否开启
    /// </summary>
    /// <returns></returns>
    public static bool GetHistc()
    {
        int val = 0;
        bool rec = AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
                       (int)TUCAM_IDCAPA.TUIDC_HISTC, ref val));
        return rec && val == 1;
    }

    /// <summary>
    /// 自动色阶模式，对应0-3
    /// </summary>
    public static readonly List<string> Levels = new()
    {
        "Disable Auto Levels",
        "Auto Left Levels",
        "Auto Right Levels",
        "Auto Levels",
    };

    /// <summary>
    /// 设置自动色阶设置
    /// 需要先开启直方图统计
    /// </summary>
    /// <param name="value">
    /// 0 - "Disable Auto Levels"
    /// 1 - "Auto Left Levels"
    /// 2 - "Auto Right Levels"
    /// 3 - "Auto Levels"
    /// </param>
    /// <returns></returns>
    public static bool SetAutolevels(int value = 3)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_ATLEVELS, value));

    /// <summary>
    /// 获取自动色阶模式
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetAutolevels(ref int value)
        => AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_ATLEVELS, ref value));

    /// <summary>
    /// 自动曝光模式 对应1-5
    /// </summary>
    public static readonly List<string> ImageMode = new List<string>()
    {
        //"12Bit","CMS", "HDR", "HighSpeedHG", "HighSpeedLG"
        "CMS", "HDR", "HighSpeedHG", "HighSpeedLG", "GlobalReset"
        // "High Sensitivity","High Dynamic","High Speed HG","High Speed LG","Global Reset"
    };

    /// <summary>
    /// 设置图片模式，这个和增益模式无关
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetImageMode(int value)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_IMGMODESELECT, value));

    public static bool GetImageMode(ref int value)
        => AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_IMGMODESELECT, ref value));

    public static bool SetLedEnable(bool value)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
                       (int)TUCAM_IDCAPA.TUIDC_ENABLELED, value ? 1 : 0));

    public static bool SetTimestampEnable(bool value)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_ENABLETIMESTAMP, value ? 1 : 0));

    public static bool SetTriggerOutEnable(bool value)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_ENABLETRIOUT, value ? 1 : 0));

    public static readonly List<string> RollingScanMode = new List<string>()
    {
        "Off","线路延时","缝隙高度"
    };

    public static bool SetRollingScanMode(int value)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_ROLLINGSCANMODE, value));

    public static bool GetRollingScanMode(ref int value)
        => AssertRet(TUCamAPI.TUCAM_Capa_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_ROLLINGSCANMODE, ref value));

    // TODO TUIDC_ROLLINGSCANLTD TUIDC_ROLLINGSCANSLIT TUIDC_ROLLINGSCANDIR TUIDC_ROLLINGSCANRESET

    /// <summary>
    /// 设置自动曝光状态
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetAutoExposure(bool value)
        => AssertRet(TUCamAPI.TUCAM_Capa_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_ATEXPOSURE, value ? 1 : 0));

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [Obsolete("这个方法不适合当前相机", true)]
    public static bool SetOnceExposure()
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_ATEXPOSURE, 2, 0));

    /// <summary>
    /// 设置自动曝光类型
    /// 0 - 居中曝光
    /// 1 - 居右曝光
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetAutoExposureMode(int value)
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDCAPA.TUIDC_ATEXPOSURE_MODE, value, 0));

    #endregion

    #region Prop

    /**
 * 对增益模式的描述
 * CMS
 * 12Bit Mode=1 Gain=0
 * HDR
 * 16Bit Mode=2 Gain=0
 * HighGain
 * 11Bit Mode=2 Gain=1
 * 12Bit(High Speed) Mode=3 Gain=1
 * 12Bit(Global Reset) Mode=5 Gain=1
 * LowGain
 * 11Bit Mode=2 Gain=2
 * 12Bit(High Speed) Mode=4 Gain=2
 * 12Bit(Global Reset) Mode=5 Gain=2
 */

    /// <summary>
    /// 全局增益模式，对应0-5
    /// </summary>
    public static readonly List<string> GlobalGain = new List<string>()
    {
        "HDR" ,"High gain" ,"Low gain" ,"HDR - Raw" ,"High gain - Raw","Low gain - Raw"
    };

    /// <summary>
    /// 设置增益模式，和SetImageMode配合使用
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetGlobalGain(int value)
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
                       (int)TUCAM_IDPROP.TUIDP_GLOBALGAIN, value, 0));

    /// <summary>
    /// 获取增益
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetGlobalGain(ref double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_GLOBALGAIN, ref value, 0));

    /// <summary>
    /// 设置曝光
    /// 这个设置的最小值是0，但是最大值和步进都需要从接口获取
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetExposure(double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_EXPOSURETM, value, 0));

    /// <summary>
    /// 获取曝光值
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetExposure(ref double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_EXPOSURETM, ref value, 0));

    /// <summary>
    /// 设置自动曝光
    /// 范围20-255，步进1
    /// 自动曝光生效前提为启动自动曝光模式且模式为居中自动曝光
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetBrightness(double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_BRIGHTNESS, value, 0));

    /// <summary>
    /// 获取自动曝光值
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetBrightness(ref double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_BRIGHTNESS, ref value, 0));

    // TODO TUIDP_BLACKLEVEL

    /// <summary>
    /// 设置黑电平值
    /// 范围1-8191，步进1
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetBlackLevel(double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_BLACKLEVEL, value, 0));

    /// <summary>
    /// 获取黑电平值
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetBlackLevel(ref double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_BLACKLEVEL, ref value, 0));

    /// <summary>
    /// 设置相机温度
    /// 范围0-100，步进1
    /// 实际温度为-50℃到50℃
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetTemperature(double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_TEMPERATURE, value, 0));

    /// <summary>
    /// 获取相机温度
    /// 范围从0-100
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetTemperature(ref double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_TEMPERATURE, ref value, 0));

    /// <summary>
    /// 设置降噪等级
    /// 可选为0 1 2 3
    /// 数值越大，降噪强度越大
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetNoiseLevel(double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_NOISELEVEL, value, 0));

    /// <summary>
    /// 获取降噪等级
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetNoiseLevel(ref double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_NOISELEVEL, ref value, 0));

    /// <summary>
    /// 设置伽马值
    /// 范围0-255
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetGamma(double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_GAMMA, value, 0));

    /// <summary>
    /// 获取伽马
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetGamma(out double value)
    {
        value = 0;
        return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_GAMMA, ref value, 0));
    }

    /// <summary>
    /// 设置对比度
    /// 范围0-255
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetContrast(double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_CONTRAST, value, 0));

    /// <summary>
    /// 获取对比度
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetContrast(out double value)
    {
        value = 0;
        return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_CONTRAST, ref value, 0));
    }


    /// <summary>
    /// 设置左色阶
    /// 范围在8bit为1-255
    /// 范围在16bit为1-65535
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetLeftLevels(double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_LFTLEVELS, value, 0));

    /// <summary>
    /// 获取左色阶
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetLeftLevels(out double value)
    {
        value = 0;
        return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_LFTLEVELS, ref value, 0));
    }

    /// <summary>
    /// 设置右色阶
    /// 范围在8bit为1-255
    /// 范围在16bit为1-65535
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool SetRightLevels(double value)
        => AssertRet(TUCamAPI.TUCAM_Prop_SetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_RGTLEVELS, value, 0));

    /// <summary>
    /// 获取右色阶
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool GetRightLevels(out double value)
    {
        value = 0;
        return AssertRet(TUCamAPI.TUCAM_Prop_GetValue(m_opCam.hIdxTUCam,
            (int)TUCAM_IDPROP.TUIDP_RGTLEVELS, ref value, 0));
    }

    #endregion

    #region PropAttr

    /// <summary>
    /// 获取属性
    /// </summary>
    /// <param name="attr"></param>
    /// <param name="prop"></param>
    /// <returns></returns>
    private static bool GetPropAttr(out TUCAM_PROP_ATTR attr, TUCAM_IDPROP prop)
    {
        attr = default;

        attr.nIdxChn = 0;
        attr.idProp = (int)prop;

        return AssertRet(TUCamAPI.TUCAM_Prop_GetAttr(m_opCam.hIdxTUCam, ref attr));
    }

    /// <summary>
    /// 获取曝光时间属性参数
    /// </summary>
    /// <param name="attr"></param>
    /// <returns></returns>
    public static bool GetExposureAttr(out TUCAM_PROP_ATTR attr)
        => GetPropAttr(out attr, TUCAM_IDPROP.TUIDP_EXPOSURETM);

    #endregion

    #region Capture

    private static TUCAM_FRAME m_drawframe; 
    private static TUCAM_TRIGGER_ATTR attrTgr;
    private static TUCAM_REC_SAVE m_rs;
    private static TUCAM_FILE_SAVE m_fs;

    /// <param name="mode">0为流模式（连续采集）</param>
    public static void Capture(int mode = 0)
    {
        m_drawframe.pBuffer = IntPtr.Zero;
        m_drawframe.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_RAW;
        m_drawframe.uiRsdSize = 1;

        //设置触发模式
        if (mode != 0)
        {
            TUCamAPI.TUCAM_Cap_GetTrigger(m_opCam.hIdxTUCam, ref attrTgr);//获取当前触发模式

            attrTgr.nTgrMode = (int)TUCAM_CAPTURE_MODES.TUCCM_TRIGGER_SOFTWARE;
            TUCamAPI.TUCAM_Cap_SetTrigger(m_opCam.hIdxTUCam, attrTgr);//设置触发模式
        }
        else
        {
            TUCamAPI.TUCAM_Cap_GetTrigger(m_opCam.hIdxTUCam, ref attrTgr);
            attrTgr.nTgrMode = (int)TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE;
            attrTgr.nFrames = 1;// TUCCM_SEQUENCE must set 1 frame                                                                                        
            attrTgr.nBufFrames = 2;
            TUCamAPI.TUCAM_Cap_SetTrigger(m_opCam.hIdxTUCam, attrTgr);
        }

        TUCamAPI.TUCAM_Buf_Alloc(m_opCam.hIdxTUCam, ref m_drawframe);
        TUCamAPI.TUCAM_Cap_Start(m_opCam.hIdxTUCam, (uint)attrTgr.nTgrMode);//以上部分为StartCapture

        bool isSaveImage = false;

        if (isSaveImage)
        {
            if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Buf_WaitForFrame(m_opCam.hIdxTUCam, ref m_drawframe,1000))
            {
                //save-image
                m_fs.nSaveFmt = (int)TUIMG_FORMATS.TUFMT_TIF;
                string strPath = string.Format(".\\{0}", 1);
                m_fs.pstrSavePath = Marshal.StringToHGlobalAnsi(strPath);       /* path */
                m_fs.pFrame = Marshal.AllocHGlobal(Marshal.SizeOf(m_drawframe));
                Marshal.StructureToPtr(m_drawframe, m_fs.pFrame, true);             /* struct to IntPtr */

                if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_File_SaveImage(m_opCam.hIdxTUCam, m_fs))
                    Console.WriteLine("Save the image data success, the path is {0}.tiff", strPath);           
            }
        }
        else
        {
            //save-vedio
            string strPath = ".\\example.avi";
            m_rs.fFps = 25.0f;
            m_rs.nCodec = 0;
            m_rs.pstrSavePath = Marshal.StringToHGlobalAnsi(strPath);
            if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Rec_Start(m_opCam.hIdxTUCam, m_rs))
            {
                for (int i = 0; i < 50; ++i)
                {
                    if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Buf_WaitForFrame(m_opCam.hIdxTUCam, ref m_drawframe))
                    {
                        TUCamAPI.TUCAM_Rec_AppendFrame(m_opCam.hIdxTUCam, ref m_drawframe);

                        Console.WriteLine("Save the frames of video success, index number is {0}", i);
                    }
                }
                TUCamAPI.TUCAM_Rec_Stop(m_opCam.hIdxTUCam);
            }
        }

        //以下部分为StopCapture
        TUCamAPI.TUCAM_Buf_AbortWait(m_opCam.hIdxTUCam);//结束数据等待
        TUCamAPI.TUCAM_Cap_Stop(m_opCam.hIdxTUCam);//停止数据捕获
        TUCamAPI.TUCAM_Buf_Release(m_opCam.hIdxTUCam);//内存释放
    }

    #endregion

    #region Extensions

    /**
     * TODO
     * 捕获Frame开启和关闭
     * 视频录制导出
     * Frame导出
     * Bin设置
     */


    /// <summary>
    /// 
    /// </summary>
    private static TUCAM_ROI_ATTR _cameraRoiAttr = default;

    /// <summary>
    /// 设置数据ROI
    /// NOTE ： 这个是SDK层面的设置，所以，修改后会更改相机实际输出数据部分
    /// </summary>
    /// <param name="width">宽</param>
    /// <param name="height">高</param>
    /// <param name="hOffset">水平偏移</param>
    /// <param name="vOffset">垂直偏移</param>
    /// <returns></returns>
    public static bool SetRoi(int width = 0, int height = 0, int hOffset = 0, int vOffset = 0)
    {
        _cameraRoiAttr.bEnable = true;

        _cameraRoiAttr.nHOffset = hOffset >> 2 << 2;
        _cameraRoiAttr.nVOffset = vOffset >> 2 << 2;
        _cameraRoiAttr.nWidth = width >> 2 << 2;
        _cameraRoiAttr.nHeight = height >> 2 << 2;

        return AssertRet(TUCamAPI.TUCAM_Cap_SetROI(m_opCam.hIdxTUCam, _cameraRoiAttr)) && GetRoi(ref _cameraRoiAttr);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static bool UnSetRoi()
    {
        _cameraRoiAttr.bEnable = false;
        return AssertRet(TUCamAPI.TUCAM_Cap_SetROI(m_opCam.hIdxTUCam, _cameraRoiAttr)) && GetRoi(ref _cameraRoiAttr);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static bool GetRoi(ref TUCAM_ROI_ATTR attr)
        => AssertRet(TUCamAPI.TUCAM_Cap_GetROI(m_opCam.hIdxTUCam, ref attr));

    /// <summary>
    /// 
    /// </summary>
    public static readonly List<string> PictureType = new List<string>()
    {
        "RAW","TIF","PNG","JPG","BMP"
    };

    /// <summary>
    /// 
    /// </summary>
    public static readonly List<TUIMG_FORMATS> SaveFormatList = new List<TUIMG_FORMATS>()
    {
        TUIMG_FORMATS.TUFMT_RAW,
        TUIMG_FORMATS.TUFMT_TIF,
        TUIMG_FORMATS.TUFMT_PNG,
        TUIMG_FORMATS.TUFMT_JPG,
        TUIMG_FORMATS.TUFMT_BMP,
    };

    /// <summary>
    /// 
    /// </summary>
    /// <param name="path"></param>
    /// <param name="typeId"></param>
    /// <returns></returns>
    public static bool SaveCurrentFrame(string path, int typeId)
    {
        TUCAM_FRAME capture = default;

        // NOTE 这里非常离谱，他们路径只支持\\这种写法
        path = path.Replace("/", "\\");

        TUCAM_FILE_SAVE fSave;

        fSave.pstrSavePath = Marshal.StringToHGlobalAnsi(path);
        fSave.pFrame = Marshal.AllocHGlobal(Marshal.SizeOf(capture));
        Marshal.StructureToPtr(capture, fSave.pFrame, true);

        var fmt = (int)SaveFormatList[typeId];

        TUCAM_FRAME frame = default;

        frame.pBuffer = IntPtr.Zero;
        frame.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_USUAl;
        frame.uiRsdSize = 1;

        // Format RAW
        if (typeId == 0)
        {
            fmt &= ~(int)TUIMG_FORMATS.TUFMT_RAW;
            fSave.nSaveFmt = (int)TUIMG_FORMATS.TUFMT_RAW;
            // Get RAW data
            frame.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_RAW;


            if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Buf_CopyFrame(m_opCam.hIdxTUCam, ref frame))
            {
                var recall = TUCamAPI.TUCAM_File_SaveImage(m_opCam.hIdxTUCam, fSave);
                if (recall != TUCAMRET.TUCAMRET_SUCCESS)
                {
                    return false;
                }

                return true;
            }

        }

        // Format other
        if (0 != fmt)
        {
            fSave.nSaveFmt = (int)SaveFormatList[typeId];

            // Get other format data
            frame.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_USUAl;
            if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Buf_CopyFrame(m_opCam.hIdxTUCam, ref frame))
                return TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_File_SaveImage(m_opCam.hIdxTUCam, fSave);
        }
        return false;
    }

    public static void SaveVideo(int interval=1000, float fps = 25f, string path = "a.avi")
    {
        int nTimes = 50;

        TUCAM_FRAME frame = default;
        TUCAM_REC_SAVE rec = default;

        frame.pBuffer = IntPtr.Zero;
        frame.ucFormatGet = (byte)TUFRM_FORMATS.TUFRM_FMT_USUAl;
        frame.uiRsdSize = 1;

        TUCamAPI.TUCAM_Buf_Alloc(m_opCam.hIdxTUCam, ref frame);

        if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Cap_Start(m_opCam.hIdxTUCam, (uint)TUCAM_CAPTURE_MODES.TUCCM_SEQUENCE))
        {
            rec.fFps = fps;
            rec.nCodec = 0;
            rec.pstrSavePath = Marshal.StringToHGlobalAnsi(path);

            if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Rec_Start(m_opCam.hIdxTUCam, rec))
            {
                for (int i = 0; i < nTimes; ++i)
                {
                    if (TUCAMRET.TUCAMRET_SUCCESS == TUCamAPI.TUCAM_Buf_WaitForFrame(m_opCam.hIdxTUCam, ref frame, interval))
                        TUCamAPI.TUCAM_Rec_AppendFrame(m_opCam.hIdxTUCam, ref frame);
                }

                TUCamAPI.TUCAM_Rec_Stop(m_opCam.hIdxTUCam);
            }

            TUCamAPI.TUCAM_Buf_AbortWait(m_opCam.hIdxTUCam);
            TUCamAPI.TUCAM_Cap_Stop(m_opCam.hIdxTUCam);
        }

        TUCamAPI.TUCAM_Buf_Release(m_opCam.hIdxTUCam);
    }

    #endregion

}