using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Common;

namespace NbCore.Plugins
{
    public abstract class PluginBase
    {
        //Properties
        public string Name = "";
        public string Description = "";
        public string Version = "";
        public string Creator = "";

        public PluginSettings Settings;
        public Engine EngineRef;

        public PluginBase()
        {
            
        }
        public PluginBase(Engine e)
        {
            EngineRef = e;
        } 
            
        public abstract void OnLoad();
        public abstract void Import(string filepath);
        public abstract void Export(string filepath);
        public abstract void OnUnload();

        public abstract void DrawImporters(); //Used to draw import functions in the File menu
        
        public abstract void DrawExporters(Scene scn); //Used to draw export functions in the File menu

        public abstract void Draw(); //Used to draw plugin panels and popups

        public virtual void Log(string message, LogVerbosityLevel level)
        {
            string msg = string.Format("{0} : {1}", Name, message);
            Callbacks.Log(msg, level);
        }

    }
}
