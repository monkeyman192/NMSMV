﻿using System;
using System.Collections.Generic;
using MVCore;
using OpenTK.Mathematics;


namespace MVCore.Systems
{
    public unsafe class TransformationSystem : EngineSystem
    {
        private readonly List<TransformData> _Data;
        private readonly Dictionary<long, TransformController> EntityControllerMap;
        private readonly Dictionary<long, TransformComponent> EntityDataMap;
        private readonly Queue<Entity> UpdatedEntities; //Entities to update on demand
        private readonly List<Entity> DynamicEntities; //Dynamic entities that need to be constantly updated
        
        //Properties
        public static double updateInterval = 1.0 / 60; //Default Update interval of 60hz

        public TransformationSystem (): base(EngineSystemEnum.TRANSFORMATION_SYSTEM)
        {
            EntityControllerMap = new();
            EntityDataMap = new();
            DynamicEntities = new();
            UpdatedEntities = new Queue<Entity> ();
            _Data = new();
        }

        public void SetInterval(double interval)
        {
            updateInterval = interval;
        }

        public void RegisterEntity(Entity e, bool createController, bool isDynamic)
        {
            if (EntityDataMap.ContainsKey(e.GetID()))
            {
                Log("Entity Already Registered", Common.LogVerbosityLevel.INFO);
                return;
            }

            if (!e.HasComponent<TransformComponent>())
            {
                Log(string.Format("Entity {0} should have a transform component", e.GetID()), Common.LogVerbosityLevel.INFO);
                return;
            }
            
            TransformComponent tc = e.GetComponent<TransformComponent>() as TransformComponent;
            
            //Insert to Maps
            EntityDataMap[e.GetID()] = tc;
            _Data.Add(tc.Data); //Add ref to TransformData list
            
            if (createController)
                EntityControllerMap[e.GetID()] = new TransformController(tc.Data);

            if (isDynamic)
                AddDynamicEntity(e);
        }

        public override void OnRenderUpdate(double dt)
        {
            //Dynamic entities that have transform controllers should be updated per frame

            //Update Dynamic Entities
            foreach (Entity e in DynamicEntities)
            {
                TransformController tc = GetEntityTransformController(e);
                tc.Update(dt);
            }

            //TODO: Apply frustum culling to all transform data objects and set visibility
            //For now mark all as visible
            foreach (TransformData td in _Data)
            {
                td.WasOccluded = td.IsOccluded;
                td.IsOccluded = false;
                td.IsUpdated = true;
            }

        }

        public override void OnFrameUpdate(double dt)
        {
            //Update On Demand Entities
            while (UpdatedEntities.Count > 0)
            {
                Entity e = UpdatedEntities.Dequeue();

                //Immediately calculate new transforms
                TransformData td = GetEntityTransformData(e);
                MeshComponent mc = e.GetComponent<MeshComponent>() as MeshComponent;
                td.RecalculateTransformMatrices();
            }
        }

        public void AddDynamicEntity(Entity e)
        {
            if (EntityDataMap.ContainsKey(e.GetID()) && !DynamicEntities.Contains(e))
                DynamicEntities.Add(e);
        }

        public void RequestEntityUpdate(Entity e)
        {
            if (EntityDataMap.ContainsKey(e.GetID()))
                UpdatedEntities.Enqueue(e);
            else
                Log("Entity not registered to the transformation system", 
                    Common.LogVerbosityLevel.WARNING);
        }

        public void RemoveDynamicEntity(Entity e)
        {
            DynamicEntities.Remove(e);
        }

        public TransformController GetEntityTransformController(Entity e)
        {
            if (EntityControllerMap.ContainsKey(e.GetID()))
                return EntityControllerMap[e.GetID()];
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

        public override void CleanUp()
        {
            _Data.Clear();
            EntityControllerMap.Clear();
            EntityDataMap.Clear();
            UpdatedEntities.Clear();
            DynamicEntities.Clear();
        }        
    }
}
