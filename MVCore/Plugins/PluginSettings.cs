using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace MVCore.Plugins
{
    public abstract class PluginSettings
    {
        public string PluginName = "";

        public static PluginSettings GenerateDefaultSettings()
        {
            return null;
        }

        public abstract void Draw(); //Imgui Panel for controlling Plugin Settings
        public abstract void DrawModals(); //Imgui draw modals and popups
    }

}
