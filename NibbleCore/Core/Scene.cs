using NbCore.Systems;
using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Common;

namespace NbCore
{
    public class Scene
    {
        public int ID = -1;
        public string Name = "";
        public SceneGraphNode Root = null;
        public readonly List<SceneGraphNode> Nodes = new();
        public readonly List<SceneGraphNode> MeshNodes = new();
        
        public Scene()
        {
            Nodes = new();
            MeshNodes = new();
        }

        public void SetID(int id)
        {
            ID = id;
        }

        public bool HasNode(SceneGraphNode n)
        {
            return Nodes.Contains(n);
        }

        public void AddNode(SceneGraphNode n)
        {
            //I should not chekck for registration status of n here
            //This should allow for node generation from the plugins
            //And then try to register the entire scene once its ready
            //to the entity registry
            
            if (HasNode(n))
            {
                Callbacks.Log(string.Format("Node {0} already belongs to scene {1}", n.Name, ID),
                    LogVerbosityLevel.WARNING);
                return;
            }

            //Handle orphans
            if (n.Parent == null)
                Root?.AddChild(n);

            Nodes.Add(n);

            if (n.HasComponent<MeshComponent>())
                MeshNodes.Add(n);
        }

        public void CacheUninitializedNodes()
        {
            foreach (SceneGraphNode n in Nodes)
            {
                GUIDComponent gc = n.GetComponent<GUIDComponent>() as GUIDComponent;

                if (!gc.Initialized)
                {
                    
                }
            }
        }

        public void SetRoot(SceneGraphNode n)
        {
            Root = n;
        }


    }
}
