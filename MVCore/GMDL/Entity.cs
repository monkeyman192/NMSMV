using System;
using System.Collections.Generic;
using System.Text;
using MVCore;

namespace MVCore
{
    public class Entity
    {
        public long ID; //unique entity identifier
        private Dictionary<Type, Component> _componentMap = new();
        
        private Entity Parent = null;
        private List<Entity> Children = new();
        
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

        public Component GetComponent<T>()
        {
            if (HasComponent<T>())
                return _componentMap[typeof(T)];
            return null;
        }

        public void AddComponent<T>(Component comp)
        {
            if (HasComponent<T>())
                return;
            _componentMap[typeof(T)] = comp;
        }


        public void RemoveComponent<T>()
        {
            //Orphan Component will be collected from the GC
            if (HasComponent<T>())
                _componentMap.Remove(typeof(T));
        }
    }
}
