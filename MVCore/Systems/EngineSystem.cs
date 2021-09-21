using System;
using System.Collections.Generic;
using System.Text;
using MVCore.Common;

namespace MVCore.Systems
{
    public enum EngineSystemEnum
    {
        NULL,
        CORE_SYSTEM,
        TRANSFORMATION_SYSTEM,
        RENDERING_SYSTEM,
        ANIMATION_SYSTEM,
        ACTION_SYSTEM,
        PHYSICS_SYSTEM,
        REGISTRY_SYSTEM,
        SCENE_MANAGEMENT_SYSTEM,
        RESOURCE_MANAGEMENT_SYSTEM
    }

    public abstract class EngineSystem
    {
        protected Engine EngineRef = null;
        private string systemName = "";
        private EngineSystemEnum systemType = EngineSystemEnum.NULL;

        //Methods

        public EngineSystem(EngineSystemEnum type)
        {
            systemType = type;
            systemName = type.ToString();
        }

        public void Log(string msg, LogVerbosityLevel lvl)
        {
            Callbacks.Log(string.Format("* {0} : {1}", systemName, msg), lvl);
        }
        
        public EngineSystemEnum GetSystemType()
        {
            return systemType;
        }

        public void SetEngine(Engine e)
        {
            EngineRef = e;
        }

        public Engine GetEngine()
        {
            return EngineRef;
        }

        public abstract void OnRenderUpdate(double dt);
        public abstract void OnFrameUpdate(double dt);
        public abstract void CleanUp();
        
    }
}
