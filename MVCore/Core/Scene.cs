using MVCore.Systems;
using System;
using System.Collections.Generic;
using System.Text;
using MVCore.Common;

namespace MVCore
{
    public class Scene
    {
        public int ID = -1;
        public string Name = "";
        public SceneGraphNode Root = null;
        private readonly List<SceneGraphNode> _Nodes = new();
        private readonly List<SceneGraphNode> _MeshNodes = new();
        
        public Scene(int id)
        {
            ID = id;
            _Nodes = new();
            _MeshNodes = new();
        }

        public List<SceneGraphNode> GetMeshNodes()
        {
            return _MeshNodes;
        }

        public bool HasNode(SceneGraphNode n)
        {
            return _Nodes.Contains(n);
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
            
            _Nodes.Add(n);

            if (n.HasComponent<MeshComponent>())
                _MeshNodes.Add(n);
        }

        public void CacheUninitializedNodes()
        {
            foreach (SceneGraphNode n in _Nodes)
            {
                GUIDComponent gc = n.GetComponent<GUIDComponent>() as GUIDComponent;

                if (!gc.Initialized)
                {
                    
                }
            }
        }

        public void SetRoot(SceneGraphNode n)
        {
            if (HasNode(n))
                Root = n;
        }

        public void Clear()
        {
            _Nodes.Clear();
            _MeshNodes.Clear();
            Root = null;
        }

        public void Update()
        {
            //Add instances to all non occluded Nodes
            foreach (SceneGraphNode n in _MeshNodes)
            {
                TransformData td = TransformationSystem.GetEntityTransformData(n);
                MeshComponent mc = n.GetComponent<MeshComponent>() as MeshComponent;

                if (td.IsUpdated)
                {
                    if (td.IsOccluded && !td.WasOccluded)
                    {
                        //Remove Instance
                        Console.WriteLine("Removing Instance {0}", n.Name);
                        GLMeshBufferManager.RemoveRenderInstance(ref mc.MeshVao, mc);
                    }
                    else if (!td.IsOccluded && td.WasOccluded)
                    {
                        Console.WriteLine("Adding Instance {0}", n.Name);
                        mc.RenderInstanceID = GLMeshBufferManager.AddRenderInstance(ref mc.MeshVao, td);
                    }
                    else if (!td.IsOccluded)
                    {
                        GLMeshBufferManager.SetInstanceWorldMat(mc.MeshVao, mc.RenderInstanceID, td.WorldTransformMat);
                        GLMeshBufferManager.SetInstanceWorldMatInv(mc.MeshVao, mc.RenderInstanceID, td.InverseTransformMat);
                    }

                    td.IsUpdated = false; //Reset updated status to prevent further updates on the same frame update
                }
            }
        }


    }
}
