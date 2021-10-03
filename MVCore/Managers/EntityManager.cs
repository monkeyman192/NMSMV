using System.Collections.Generic;
using MVCore.Common;

namespace MVCore.Managers
{
    public class EntityManager<T>
    {
        public int EntityCount = 0;
        public List<T> Entities = new();
        public Dictionary<long, Entity> EntityMap = new();

        private Entity _CheckEntity(T item)
        {
            Entity e = (Entity)(object)item;
            Callbacks.Assert(e != null, "Null entity, is item an entity??");
            return e;
        }

        public virtual bool Exists(Entity item)
        {
            return EntityMap.ContainsKey(item.ID);
        }
            
        public virtual bool Add(T item)
        {
            Entity e = _CheckEntity(item);
            if (!Exists(e))
            {
                Entities.Add(item);
                EntityMap[e.ID] = e;
                EntityCount++;
                return true;
            }
            return false;
        }

        public virtual bool Remove(T item)
        {
            Entity e = _CheckEntity(item);
            if (Exists(e))
            {
                Entities.Remove(item);
                EntityMap.Remove(e.ID);
                EntityCount--;
                return true;
            }
            return false;
        }

        public Entity Get(long id)
        {
            return EntityMap[id];
        }

        public virtual void CleanUp()
        {
            EntityMap.Clear();
            Entities.Clear();
        }

    }
}
