using System;
using System.Collections.Generic;
using MVCore;


namespace MVCore.Systems
{
    public class TransformationSystem : EngineSystem
    {
        private List<TransformData> data;
        private Dictionary<long, TransformController> EntityControllerMap;
        private Dictionary<long, TransformComponent> EntityDataMap;
        private List<Entity> DynamicEntities; //Entities to update on update
        
        //Properties
        public static double updateInterval = 1.0 / 60; //Default Update interval of 60hz

        public TransformationSystem (): base(EngineSystemEnum.TRANSFORMATION_SYSTEM)
        {
            EntityControllerMap = new();
            EntityDataMap = new();
            DynamicEntities = new();
        }

        public void SetInterval(double interval)
        {
            updateInterval = interval;
        }

        public void RegisterEntity(Entity e, bool createController)
        {
            if (EntityDataMap.ContainsKey(e.ID))
            {
                Log("Entity Already Registered", Common.LogVerbosityLevel.INFO);
                return;
            }

            if (e.HasComponent<TransformComponent>())
            {
                Log(string.Format("Entity {0} already has a transform component", e.ID), Common.LogVerbosityLevel.INFO);
                return;
            }

            TransformData td = new TransformData();
            TransformComponent tc = new TransformComponent(td);
            
            //Add component to entity
            e.AddComponent<TransformComponent>(tc);

            //Insert to Maps
            EntityDataMap[e.ID] = tc;
            
            if (createController)
                EntityControllerMap[e.ID] = new TransformController(tc);
                
        }

        public void Update(double dt)
        {
            foreach (Entity e in DynamicEntities)
            {
                TransformController tc = GetEntityTransformController(e);
                tc.Update(dt);
            }
        }

        public void AddDynamicEntity(Entity e)
        {
            if (EntityDataMap.ContainsKey(e.ID) && !DynamicEntities.Contains(e))
                DynamicEntities.Add(e);
        }

        public void RemoveDynamicEntity(Entity e)
        {
            DynamicEntities.Remove(e);
        }

        public TransformController GetEntityTransformController(Entity e)
        {
            if (EntityControllerMap.ContainsKey(e.ID))
                return EntityControllerMap[e.ID];
            return null;
        }



    }
}
