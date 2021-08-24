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

    public enum ViewSettingsEnum
    {
        ViewInfo = 1,
        ViewLights = 2,
        ViewLightVolumes = 4,
        ViewJoints = 8,
        ViewLocators = 16,
        ViewCollisions = 32,
        ViewBoundHulls = 64,
        ViewGizmos = 128,
        EmulateActions = 256
    }

    public struct ViewSettings
    {
        public bool ViewInfo;
        public bool ViewLights;
        public bool ViewLightVolumes;
        public bool ViewJoints;
        public bool ViewLocators;
        public bool ViewCollisions;
        public bool ViewBoundHulls;
        public bool ViewGizmos;
        public bool EmulateActions;
        public int SettingsMask;
        
        
        //Use the settings mask when serializing the struct to the settings file
        public ViewSettings(int settings_mask)
        {
            SettingsMask = settings_mask;
            ViewInfo = (settings_mask & (int) ViewSettingsEnum.ViewInfo) == 0 ? false : true;
            ViewLights = (settings_mask & (int)ViewSettingsEnum.ViewLights) == 0 ? false : true;
            ViewLightVolumes = (settings_mask & (int)ViewSettingsEnum.ViewLightVolumes) == 0 ? false : true;
            ViewJoints = (settings_mask & (int)ViewSettingsEnum.ViewJoints) == 0 ? false : true;
            ViewLocators = (settings_mask & (int)ViewSettingsEnum.ViewLocators) == 0 ? false : true;
            ViewCollisions = (settings_mask & (int)ViewSettingsEnum.ViewCollisions) == 0 ? false : true;
            ViewBoundHulls = (settings_mask & (int)ViewSettingsEnum.ViewBoundHulls) == 0 ? false : true;
            ViewGizmos = (settings_mask & (int)ViewSettingsEnum.ViewGizmos) == 0 ? false : true;
            EmulateActions = (settings_mask & (int)ViewSettingsEnum.EmulateActions) == 0 ? false : true;
        }

    }


    public class RenderSettings
    {
        public int FPS = 60;
        public bool UseVSync = false;
        public float HDRExposure = 0.005f;
        
        //Set Full rendermode by default
        [JsonIgnore]
        public PolygonMode RENDERMODE 
        {
            get {
                if (RenderWireFrame)
                    return PolygonMode.Line;
                return PolygonMode.Fill;
            }
        }

        [JsonIgnore]
        public Color clearColor = System.Drawing.Color.FromArgb(255, 33, 33, 33);
        public bool UseTextures = true;
        public bool UseLighting = true;

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
        public bool UseFXAA = true;
        public bool UseBLOOM = true;

        [JsonIgnore]
        public bool UseFrustumCulling = true;

        [JsonIgnore]
        public bool LODFiltering = true;

        [JsonIgnore]
        public bool RenderWireFrame = false;

        [JsonIgnore]
        public bool ToggleAnimations = true;

    }


    public class Settings : INotifyPropertyChanged
    {
        //Public Settings
        public RenderSettings renderSettings = new RenderSettings();
        public ViewSettings viewSettings = new ViewSettings(31);
        
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
    public delegate void UpdateStatusCallback(string msg);
    public delegate void OpenAnimCallback(string filepath, Model animScene);
    public delegate void OpenPoseCallback(string filepath, Model animScene);
    public delegate void ShowInfoMsg(string msg, string caption);
    public delegate void ShowErrorMsg(string msg, string caption);
    public delegate void LogCallback(string msg, LogVerbosityLevel level);
    public delegate void AssertCallback(bool status, string msg);
    public delegate void SendRequestCallback(ref ThreadRequest req);
    public delegate byte[] GetResourceCallback(string resourceName);
    public delegate Bitmap GetBitMapResourceCallback(string resourceName);
    public delegate string GetTextResourceCallback(string resourceName);
    public delegate object GetResourceWithTypeCallback(string resourceName, out string resourceType);

    public static class Callbacks
    {
        public static UpdateStatusCallback updateStatus = null;
        public static ShowInfoMsg showInfo = null;
        public static ShowErrorMsg showError = null;
        public static OpenAnimCallback openAnim = null;
        public static OpenPoseCallback openPose = null;
        public static LogCallback Log = null;
        public static AssertCallback Assert = null;
        public static SendRequestCallback issueRequestToGLControl = null;
        public static GetResourceCallback getResource = null;
        public static GetBitMapResourceCallback getBitMapResource = null;
        public static GetTextResourceCallback getTextResource = null;
        public static GetResourceWithTypeCallback getResourceWithType = null;
    }
}
