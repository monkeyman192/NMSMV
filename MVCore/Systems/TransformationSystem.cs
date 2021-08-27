using System;
using System.Collections.Generic;
using MVCore;
using OpenTK.Mathematics;


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

            if (!e.HasComponent<TransformComponent>())
            {
                Log(string.Format("Entity {0} should have a transform component", e.ID), Common.LogVerbosityLevel.INFO);
                return;
            }
            
            TransformComponent tc = e.GetComponent<TransformComponent>() as TransformComponent;
            
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

        public static void AddTransformComponentToEntity(Entity e)
        {
            TransformData td = new();
            TransformComponent tc = new(td);

            e.AddComponent<TransformComponent>(tc);
        }

        public static void SetEntityLocation(Entity e, Vector3 loc)
        {
            TransformData td = (e.GetComponent<TransformComponent>() as TransformComponent).Data;
            td.localTranslation = loc;
        }

        public static void SetEntityRotation(Entity e, Quaternion rot)
        {
            TransformData td = (e.GetComponent<TransformComponent>() as TransformComponent).Data;
            td.localRotation = rot;
        }

        public static void SetEntityScale(Entity e, Vector3 scale)
        {
            TransformData td = (e.GetComponent<TransformComponent>() as TransformComponent).Data;
            td.localScale = scale;
        }

        public static Matrix4 GetEntityLocalMat(Entity e)
        {
            TransformData td = (e.GetComponent<TransformComponent>() as TransformComponent).Data;
            return td.LocalTransformMat;
        }

        public static Quaternion GetEntityLocalRotation(Entity e)
        {
            TransformData td = (e.GetComponent<TransformComponent>() as TransformComponent).Data;
            return td.localRotation;
        }

        public static Matrix4 GetEntityWorldMat(Entity e)
        {
            TransformData td = (e.GetComponent<TransformComponent>() as TransformComponent).Data;
            return td.WorldTransformMat;
        }

        public static Vector4 GetEntityWorldPosition(Entity e)
        {
            TransformData td = (e.GetComponent<TransformComponent>() as TransformComponent).Data;
            return td.WorldPosition;
        }

        public static TransformData GetEntityTransformData(Entity e)
        {
            return (e.GetComponent<TransformComponent>() as TransformComponent).Data;
        }

    }
}
