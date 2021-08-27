using System;
using System.Collections.Generic;
using System.Text;
using MVCore;

namespace MVCore
{
    public class Entity
    {
        //Public
        public long ID; //unique entity identifier
        public string Name = "";
        public ulong NameHash;
        public TYPES Type;
        public Entity Parent = null;
        public List<Entity> Children = new();

        //Private
        private Dictionary<Type, Component> _componentMap = new();
        
        
        public Entity()
        {

        }

        public void SetParent(Entity e)
        {
            Parent = e;
            Parent.Children.Add(e);

            //Connect TransformComponents if both have
            if (e.HasComponent<TransformComponent>() && HasComponent<TransformComponent>())
            {
                TransformComponent tc = GetComponent<TransformComponent>() as TransformComponent;
                TransformComponent parent_tc = GetComponent<TransformComponent>() as TransformComponent;
                tc.Data.SetParentData(parent_tc.Data);
            }
        }

        public void DettachFromParent()
        {
            Common.Callbacks.Assert(Parent != null, "Parent Entity should not be null");
            Parent.Children.Remove(this);

            if (HasComponent<TransformComponent>())
            {
                TransformComponent tc = GetComponent<TransformComponent>() as TransformComponent;
                tc.Data.ClearParentData();
            }


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
            Entity n = new Entity();
            n.CopyFrom(this);
            
            return n;
        }

        public void CopyFrom(Entity e)
        {
            //Copy data from e
            Type = e.Type;
            Name = e.Name;
            ID = e.ID;

            //Clone components
            
            foreach (KeyValuePair<Type, Component> kp in _componentMap)
            {
                Component c = kp.Value.Clone();
                AddComponent(c.GetType(), c);
            }
        }

    }
}
