using System;
using System.Collections.Generic;
using System.Text;
using MVCore.Common;

namespace MVCore.Systems
{
    public class EntityRegistrySystem : EngineSystem
    {
        private long itemCounter = 0;
        private long NextID = 1;
        private Dictionary<long, Entity> EntityMap = new();
        private Dictionary<EntityType, List<Entity>> EntityTypeList = new();
        public EntityRegistrySystem() : base(EngineSystemEnum.REGISTRY_SYSTEM)
        {
            //Initialize EntityTypeList
            foreach (EntityType t in Enum.GetValues(typeof(EntityType)))
            {
                EntityTypeList[t] = new List<Entity>();
            }
        }

        public Entity GetEntity(long ID)
        {
            return EntityMap[ID];
        }
        
        public Entity GetEntity(EntityType type, long ID)
        {
            return EntityTypeList[type].Find(x=> x.ID == ID);
        }

        public List<Entity> GetEntityTypeList(EntityType type)
        {
            return EntityTypeList[type];
        }
        
        public bool RegisterEntity(Entity e)
        {
            if (e.ID > 0)
            {
                Log("Entity has no default ID, probably already registered", Common.LogVerbosityLevel.INFO);
                return false;
            }

            if (IsRegistered(e))
            {
                Log("Entity already registered", Common.LogVerbosityLevel.INFO);
                return false;
            }
            
            e.ID = NextID++;
            EntityMap[e.ID] = e;
            EntityTypeList[e.Type].Add(e);
            return true;
        }

        public bool DeleteEntity(Entity e)
        {
            if (!IsRegistered(e))
            {
                Log("Entity not registered. Nothing to do", LogVerbosityLevel.INFO);
                return false;
            }

            EntityMap.Remove(e.ID);
            EntityTypeList[e.Type].Remove(e);
            
            //Dispose entity lets hope that the overrides will work and we won't have to cast
            e.Dispose();
            
            return true;
        }
        //This clears the registry, other systems are responsible for disposing all generated components
        public void Clear()
        {
            itemCounter = 0;
            EntityMap.Clear();
            foreach (EntityType t in Enum.GetValues(typeof(EntityType)))
                EntityTypeList[t].Clear();
        }

        public bool IsRegistered(Entity e)
        {
            if (EntityMap.ContainsKey(e.ID))
                return true;
            return false;
        }


    }
}
