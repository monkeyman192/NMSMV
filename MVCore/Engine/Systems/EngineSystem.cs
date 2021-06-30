using System;
using System.Collections.Generic;
using System.Text;
using MVCore.Common;

namespace MVCore.Engine.Systems
{
    public enum EngineSystemEnum
    {
        UNITIALIZED,
        RENDERING_SYSTEM,
        ANIMATION_SYSTEM,
        ACTION_SYSTEM,
        PHYSICS_SYSTEM,
    }

    public abstract class EngineSystem
    {
        private Engine engineRef = null;
        private string systemName = "";
        private EngineSystemEnum systemType = EngineSystemEnum.UNITIALIZED;

        //Methods

        public EngineSystem(EngineSystemEnum type)
        {
            systemType = type;
            switch (type)
            {
                case EngineSystemEnum.RENDERING_SYSTEM:
                    systemName = "RENDERING_SYSTEM";
                    break;
                case EngineSystemEnum.ACTION_SYSTEM:
                    systemName = "ACTION_SYSTEM";
                    break;
                case EngineSystemEnum.ANIMATION_SYSTEM:
                    systemName = "ANIMATION_SYSTEM";
                    break;
            }
        }

        public void Log(string msg, LogVerbosityLevel lvl)
        {
            CallBacks.Log(string.Format("*{0} : {1}", systemName, msg), lvl);
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
