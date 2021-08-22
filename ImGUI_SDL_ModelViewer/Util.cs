using System;
using System.Collections.Generic;
using System.IO;
using MVCore;
using MVCore.Common;
using ImGuiNET;
using System.Drawing;
using System.Reflection;



namespace ImGUI_SDL_ModelViewer
{
    public static class Util
    {
        public static int VersionMajor = 0;
        public static int VersionMedium = 91;
        public static int VersionMinor = 0;
        
        public static string DonateLink = @"https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=4365XYBWGTBSU&currency_code=USD&source=url";
        public static readonly Random randgen = new Random();
        
        //Current GLControl Handle
        public static OpenTK.Windowing.Desktop.NativeWindow activeWindow;
        public static string StatusStripText;
        
        //Public LogFile
        public static StreamWriter loggingSr;
        

        public static string getVersion()
        {
            string ver = string.Join(".", new string[] { VersionMajor.ToString(),
                                           VersionMedium.ToString(),
                                           VersionMinor.ToString()});
#if DEBUG
            return ver + " [DEBUG]";
#endif
            return ver;
        }

        //Update Status strip
        public static void setStatus(string status)
        {
            StatusStripText = status;
        }

        public static void showError(string message, string caption)
        {
            Console.WriteLine(string.Format("%s", message));
            //Cannot use ImGui from here
            /*
            if (ImGui.BeginPopupModal("show-error"))
            {
                ImGui.Text();
                ImGui.EndPopup();
            }
            */
        }
        
        
        public static void showInfo(string message, string caption)
        {

            if (ImGui.BeginPopupModal("show-info"))
            {
                ImGui.Text(string.Format("%s", message));
                ImGui.EndPopup();
            }
        }

        //Generic Procedures - File Loading
        
        public static void Log(string msg, LogVerbosityLevel lvl)
        {
            if (lvl >= RenderState.settings.LogVerbosity)
            {
                Console.WriteLine(msg);
                loggingSr.WriteLine(msg);
                loggingSr.Flush();
            }
        }

        public static void Assert(bool status, string msg)
        {
            if (!status)
                Callbacks.Log(msg, LogVerbosityLevel.ERROR);
            System.Diagnostics.Trace.Assert(status);
        }
    

        //Resource Handler
        public static byte[] getResource(string resource_name)
        {
            string nspace = nameof(ImGUI_SDL_ModelViewer);

            byte[] data = null; //output data

            // Determine path
            var assembly = Assembly.GetExecutingAssembly();
            string resourcePath = resource_name;

            Assembly _assembly = Assembly.GetExecutingAssembly();
            string[] resources = _assembly.GetManifestResourceNames();

            try
            {
                BinaryReader _textStreamReader = new BinaryReader(_assembly.GetManifestResourceStream(nspace + ".Resources." + resource_name));
                data = _textStreamReader.ReadBytes((int) _textStreamReader.BaseStream.Length);
            } catch
            {
                Callbacks.Log("Unable to Fetch Resource", LogVerbosityLevel.ERROR);
            }
            
            return data;
        }

        public static Bitmap getBitMapResource(string resource_name)
        {
            byte[] data = getResource(resource_name);

            if (data != null)
            {
                MemoryStream ms = new MemoryStream(data);
                Bitmap im = new Bitmap(ms);
                return im;
            }

            return null;
        }

        public static string getTextResource(string resource_name)
        {
            byte[] data = getResource(resource_name);

            if (data != null)
            {
                MemoryStream ms = new MemoryStream(data);
                StreamReader tr = new StreamReader(ms);
                return tr.ReadToEnd();
            }

            return "";
        }


    }

}
