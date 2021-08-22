using System;
using System.Collections.Generic;
using MVCore;


namespace MVCore.Systems
{
    class TransformationSystem : EngineSystem
    {
        private List<TransformData> data;
        private Dictionary<long, TransformController> EntityControllerMap;
        private Dictionary<long, TransformData> EntityDataMap;
        private List<Entity> DynamicEntities; //Entities to update on update

        //Properties
        public static double updateInterval = 1.0 / 60; //Default interval to 60hz

        public TransformationSystem (): base(EngineSystemEnum.TRANSFORMATION_SYSTEM)
        {

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

            if (!e.HasComponent<TransformComponent>())
            {
                Log(string.Format("Entity {0} does not have a transform component", e.ID), Common.LogVerbosityLevel.INFO);
                return;
            }

            TransformComponent tc = e.GetComponent<TransformComponent>() as TransformComponent;

            //Insert to Maps
            EntityDataMap[e.ID] = new TransformData();
            
            //Bind data to Component
            tc.Data = EntityDataMap[e.ID];

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

        TransformData GetEntityData(Entity e)
        {
            if (EntityDataMap.ContainsKey(e.ID))
                return EntityDataMap[e.ID];
            return null;
        }

        TransformController GetEntityTransformController(Entity e)
        {
            if (EntityControllerMap.ContainsKey(e.ID))
                return EntityControllerMap[e.ID];
            return null;
        }



    }
}
