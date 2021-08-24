using System;
using System.Collections.Generic;
using System.Text;
using MVCore.Common;

namespace MVCore.Systems
{
    public enum EngineSystemEnum
    {
        NULL,
        TRANSFORMATION_SYSTEM,
        RENDERING_SYSTEM,
        ANIMATION_SYSTEM,
        ACTION_SYSTEM,
        PHYSICS_SYSTEM,
        REGISTRY_SYSTEM
    }

    public abstract class EngineSystem
    {
        private Engine engineRef = null;
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
            Callbacks.Log(string.Format("*{0} : {1}", systemName, msg), lvl);
        }
        
        public EngineSystemEnum GetSystemType()
        {
            return systemType;
        }

        public void SetEngine(Engine e)
        {
            engineRef = e;
        }

        public Engine GetEngine()
        {
            return engineRef;
        }
        public virtual void Update(float dt)
        {

        }

        public virtual void CleanUp()
        {

        }
        
    }
}
