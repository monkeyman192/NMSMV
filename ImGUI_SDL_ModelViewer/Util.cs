using System;
using System.Collections.Generic;
using System.IO;
using MVCore;
using MVCore.Common;
using OpenTK;
using ImGuiHelper;
using ImGuiNET;

namespace ImGUI_SDL_ModelViewer
{
    public static class Util
    {
        public static int VersionMajor = 0;
        public static int VersionMedium = 91;
        public static int VersionMinor = 0;
        
        public static string DonateLink = "https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=4365XYBWGTBSU&currency_code=USD&source=url";
        public static readonly Random randgen = new Random();
        
        //Current GLControl Handle
        public static OpenTK.Windowing.Desktop.GameWindow activeWindow;
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
            if (ImGui.BeginPopupModal("show-error"))
            {
                ImGui.Text(string.Format("%s", message));
                ImGui.EndPopup();
            }
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
    
    }

}
