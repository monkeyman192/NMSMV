using System;
using System.Collections.Generic;
using System.Text;
using MVCore;

namespace MVCore
{
    public enum EntityType
    {
        SceneNode,
        SceneNodeLight,
        SceneNodeJoint,
        SceneNodeMesh,
        SceneNodeLocator,
        SceneNodeModel,
        MeshComponent,
        AnimationComponent,
        SceneComponent,
        LightComponent,
        Material,
        Texture,
        GeometryObject,
        Camera,
        Script,
        Asset,
        ShaderSource,
        Shader,
        Mesh,
        InstancedMesh,
        LightInstancedMesh
    }
    public class Entity : IDisposable
    {
        //Public
        public long ID; //unique entity identifier
        public ulong NameHash;
        public EntityType Type;
        
        //Private
        private readonly Dictionary<Type, Component> _componentMap = new();

        //Disposable Stuff
        private bool disposedValue;

        public Entity(EntityType typ)
        {
            Type = typ;
        }

        public bool HasComponent<T>()
        {
            return _componentMap.ContainsKey(typeof(T));
        }

        public bool HasComponent(Type t)
        {
            return _componentMap.ContainsKey(t);
        }

        public Component GetComponent<T>()
        {
            if (HasComponent<T>())
                return _componentMap[typeof(T)];
            return null;
        }

        public void AddComponent<T>(Component comp)
        {
            AddComponent(typeof(T), comp);
        }

        public void AddComponent(Type t, Component comp)
        {
            if (HasComponent(t))
                return;
            _componentMap[t] = comp;
        }

        public void RemoveComponent<T>()
        {
            //Orphan Component will be collected from the GC
            if (HasComponent<T>())
                _componentMap.Remove(typeof(T));
        }


        public Entity Clone()
        {
            Entity n = new(this.Type);
            n.CopyFrom(this);
            
            return n;
        }

        public void CopyFrom(Entity e)
        {
            //Copy data from e
            Type = e.Type;
            ID = e.ID;

            //Clone components
            
            foreach (KeyValuePair<Type, Component> kp in _componentMap)
            {
                Component c = kp.Value.Clone();
                AddComponent(c.GetType(), c);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
