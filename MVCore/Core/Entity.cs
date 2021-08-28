using System;
using System.Collections.Generic;
using System.Text;
using MVCore;

namespace MVCore
{
    public class Entity : IDisposable
    {
        //Public
        public long ID; //unique entity identifier
        public string Name = "";
        public ulong NameHash;
        public TYPES Type;
        

        //Private
        private readonly Dictionary<Type, Component> _componentMap = new();

        //Disposable Stuff
        public bool disposed = false;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new(IntPtr.Zero, true);


        public Entity()
        {

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
            Entity n = new();
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

        public void Dispose()
        {
            Dispose(true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();
            }

            //Free unmanaged resources
            disposed = true;
        }

#if DEBUG
        ~Entity()
        {
            // If this finalizer runs, someone somewhere failed to
            // call Dispose, which means we've failed to leave
            // a monitor!
            System.Diagnostics.Debug.Fail("Undisposed lock. Object Type " + Type.ToString());
        }
#endif
    }
}
