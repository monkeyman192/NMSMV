using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace NbCore.Plugins
{
    public abstract class PluginSettings
    {
        public static PluginSettings GenerateDefaultSettings()
        {
            return null;
        }

        public abstract void SaveToFile();
        

        public abstract void Draw(); //Imgui Panel for controlling Plugin Settings
        public abstract void DrawModals(); //Imgui draw modals and popups
    }

}
