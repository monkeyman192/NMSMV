using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore.Systems
{
    public class EntityRegistrySystem : EngineSystem
    {
        private long itemCounter = 0;
        private Dictionary<long, Entity> EntityMap = new();

        public EntityRegistrySystem() : base(EngineSystemEnum.REGISTRY_SYSTEM)
        {

        }

        public void RegisterEntity(Entity e)
        {
            if (e.ID > 0)
            {
                Log("Entity has no default ID, probably already registered", Common.LogVerbosityLevel.INFO);
                return;
            }

            if (IsRegistered(e))
            {
                Log("Entity already registered", Common.LogVerbosityLevel.INFO);
                return;
            }
            
            e.ID = itemCounter++;
            EntityMap[e.ID] = e;
        }

        //This clears the registry, other systems are responsible for disposing all generated components
        public void Clear()
        {
            itemCounter = 0;
            EntityMap.Clear();
        }

        public bool IsRegistered(Entity e)
        {
            if (EntityMap.ContainsKey(e.ID))
                return true;
            return false;
        }


    }
}
