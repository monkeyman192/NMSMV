using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using MVCore.GMDL;
using MVCore.Input;
using MVCore;
using GLSLHelper;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using Newtonsoft.Json;
using System.Resources;
using System.Reflection;
using System.IO;
using MVCore.Utils;
using System.Drawing;

namespace MVCore.Common
{

    public enum MouseMovementStatus
    {
        CAMERA_MOVEMENT = 0x0,
        GIZMO_MOVEMENT,
        IDLE
    }

    public class MouseMovementState
    {
        public Vector2 Position = new Vector2();
        public Vector2 Delta = new Vector2();   
    }

    public static class RenderState
    {
        //Add a random generator just for the procgen procedures
        public static Random randgen = new Random();

        //Keep the view rotation Matrix
        public static Matrix4 rotMat = Matrix4.Identity;

        //Keep the view rotation Angles (in degrees)
        public static Vector3 rotAngles = new Vector3(0.0f);

        //RenderSettings
        //USE SETTINGS CLASS
        //public static RenderSettings renderSettings = new RenderSettings();

        //renderViewSettings
        public static RenderViewSettings renderViewSettings = new RenderViewSettings();

        //App Settings
        public static Settings settings = new Settings();

        //Keep the main camera global
        public static Camera activeCam;
        //Active ResourceManager
        public static ResourceManager activeResMgr;
        //RootObject
        public static Model rootObject;
        //ActiveModel
        public static Model activeModel;
        //ActiveGizmo
        public static Gizmo activeGizmo;
        //Active GamePad
        public static BaseGamepadHandler activeGamepad;

        public static bool enableShaderCompilationLog = true;
        public static string shaderCompilationLog;

        //Static methods

        public static float progressTime(double dt)
        {
            float new_time = (float) dt / 500;
            new_time %= 1000.0f;
            return new_time;
        }

    }

    public class RenderViewSettings: INotifyPropertyChanged
    {
        //Properties
        
        public bool RenderInfo { get; set; } = true;

        public bool RenderLights { get; set; } = true;

        public bool RenderLightVolumes { get; set; } = true;

        public bool RenderJoints { get; set; } = true;

        public bool RenderLocators { get; set; } = true;

        public bool RenderCollisions { get; set; } = false;

        public bool RenderBoundHulls { get; set; } = false;

        public bool RenderGizmos { get; set; } = false;

        public bool EmulateActions { get; set; } = false;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

    }

    public class RenderSettings: INotifyPropertyChanged
    {
        public int _fps = 60;
        public bool _useVSYNC = false;
        public float _HDRExposure = 0.005f;
        //Set Full rendermode by default
        [JsonIgnore]
        public PolygonMode RENDERMODE = PolygonMode.Fill;
        [JsonIgnore]
        public System.Drawing.Color clearColor = System.Drawing.Color.FromArgb(255, 33, 33, 33);
        public float _useTextures = 1.0f;
        public float _useLighting = 1.0f;

        //Test Settings
#if (DEBUG)
        [JsonIgnore]
        public float testOpt1 = 0.0f;
        [JsonIgnore]
        public float testOpt2 = 0.0f;
        [JsonIgnore]
        public float testOpt3 = 0.0f;
#endif

        //Properties

        public bool UseFXAA { get; set; } = true;

        public bool UseBLOOM { get; set; } = true;

        public bool UseVSYNC
        {
            get
            {
                return _useVSYNC;
            }
            set
            {
                _useVSYNC = value;
                NotifyPropertyChanged("UseVSYNC");
            }
        }

        [JsonIgnore]
        public string FPS
        {
            get => _fps.ToString();
            set
            {
                int.TryParse(value, out _fps);
                NotifyPropertyChanged("FPS");
            }
        }

        public string HDRExposure
        {
            get => _HDRExposure.ToString();
            set
            {
                _HDRExposure = Utils.MathUtils.FloatParse(value);
                NotifyPropertyChanged("HDRExposure");
            }
        }

        //Add properties
        [JsonIgnore]
        public bool UseTextures
        {
            get
            {
                return (_useTextures > 0.0f);
            }

            set
            {
                if (value)
                    _useTextures = 1.0f;
                else
                    _useTextures = 0.0f;
                NotifyPropertyChanged("UseTextures");
            }
        }

        [JsonIgnore]
        public bool UseLighting
        {
            get
            {
                return (_useLighting > 0.0f);
            }

            set
            {
                if (value)
                    _useLighting = 1.0f;
                else
                    _useLighting = 0.0f;
                NotifyPropertyChanged("UseLighting");
            }
        }

        [JsonIgnore]
        public bool UseFrustumCulling { get; set; } = true;

        [JsonIgnore]
        public bool LODFiltering { get; set; } = true;

        [JsonIgnore]
        public bool ToggleWireframe
        {
            get => (RENDERMODE == PolygonMode.Line);
            set
            {
                if (value)
                    RENDERMODE = PolygonMode.Line;
                else
                    RENDERMODE = PolygonMode.Fill;
            }
        }

        [JsonIgnore]
        public bool ToggleAnimations { get; set; } = true;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

    }


    public class Settings : INotifyPropertyChanged
    {
        //Public Settings
        public RenderSettings rendering = new RenderSettings();

        //Private Settings
        private int forceProcGen;
        private string gamedir;
        private string unpackdir;
        private LogVerbosityLevel _logVerbosity;

        public string GameDir
        {
            get
            {
                return gamedir;
            }

            set
            {
                gamedir = value;
                NotifyPropertyChanged("GameDir");
            }
        }


        public string UnpackDir
        {

            get
            {
                return unpackdir;
            }

            set
            {
                unpackdir = value;
                NotifyPropertyChanged("UnpackDir");
            }

        }

        public int ProcGenWinNum;

        public bool ForceProcGen;

        public event PropertyChangedEventHandler PropertyChanged;

        public LogVerbosityLevel LogVerbosity
        {
            get
            {
                return _logVerbosity;
            }

            set
            {
                _logVerbosity = value;
            }
        }

        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        //Methods
        public static Settings generateDefaultSettings()
        {
            Settings settings = new Settings();

            settings.GameDir = NMSUtils.getGameInstallationDir();
            settings.unpackdir = settings.GameDir;

            return settings;
        }

        public static Settings loadFromDisk()
        {
            //Load jsonstring
            Settings settings;
            if (File.Exists("settings.json"))
            {
                string jsonstring = File.ReadAllText("settings.json");
                settings =  JsonConvert.DeserializeObject<Settings>(jsonstring);
            } else
            {
                //Generate Settings
                //Generating new settings file
                settings = generateDefaultSettings();
                saveToDisk(settings);
            }
            
            return settings;
        }

        public static void saveToDisk(Settings settings)
        {
            //Test Serialize object
            string jsonstring = JsonConvert.SerializeObject(settings);
            File.WriteAllText("settings.json", jsonstring);
        }
    }

    public static class RenderStats
    {
        //Set Full rendermode by default
        public static int vertNum = 0;
        public static int trisNum = 0;
        public static int texturesNum = 0;
        public static float fpsCount = 0;
        public static int occludedNum = 0;

        public static void ClearStats()
        {
            vertNum = 0;
            trisNum = 0;
            texturesNum = 0;
        }
    }

    public enum LogVerbosityLevel
    {
        HIDEBUG,
        DEBUG,
        INFO,
        WARNING,
        ERROR
    }

    //Delegates - Function Types for Callbacks
    public delegate void UpdateStatusCallBack(string msg);
    public delegate void OpenAnimCallBack(string filepath, Model animScene);
    public delegate void OpenPoseCallBack(string filepath, Model animScene);
    public delegate void ShowInfoMsg(string msg, string caption);
    public delegate void ShowErrorMsg(string msg, string caption);
    public delegate void LogCallBack(string msg, LogVerbosityLevel level);
    public delegate void SendRequestCallBack(ref ThreadRequest req);
    public delegate byte[] GetResourceCallBack(string resourceName);
    public delegate Bitmap GetBitMapResourceCallBack(string resourceName);
    public delegate string GetTextResourceCallBack(string resourceName);
    public delegate object GetResourceWithTypeCallBack(string resourceName, out string resourceType);

    public static class CallBacks
    {
        public static UpdateStatusCallBack updateStatus = null;
        public static ShowInfoMsg showInfo = null;
        public static ShowErrorMsg showError = null;
        public static OpenAnimCallBack openAnim = null;
        public static OpenPoseCallBack openPose = null;
        public static LogCallBack Log = null;
        public static SendRequestCallBack issueRequestToGLControl = null;
        public static GetResourceCallBack getResource = null;
        public static GetBitMapResourceCallBack getBitMapResource = null;
        public static GetTextResourceCallBack getTextResource = null;
        public static GetResourceWithTypeCallBack getResourceWithType = null;
    }
}
