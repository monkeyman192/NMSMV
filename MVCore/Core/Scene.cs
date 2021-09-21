using MVCore.Systems;
using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore
{
    public class Scene
    {
        public int ID = -1;
        public string Name = "";
        private readonly List<long> _entityIDList;
        public SceneGraphNode Root = null;
        private readonly List<SceneGraphNode> _Nodes = new();
        private readonly List<SceneGraphNode> _MeshNodes = new();

        public Scene(int id)
        {
            ID = id;
            _entityIDList = new();
            _Nodes = new();
            _MeshNodes = new();
        }

        public List<SceneGraphNode> GetMeshNodes()
        {
            return _MeshNodes;
        }

        public bool HasNode(SceneGraphNode n)
        {
            return _entityIDList.Contains(n.ID);
        }

        public void AddNode(SceneGraphNode n)
        {
            if (HasNode(n))
            {
                Common.Callbacks.Log(string.Format("Node {0} already belongs to scene {1}", n.ID, ID),
                    Common.LogVerbosityLevel.WARNING);
                return;
            }

            _entityIDList.Add(n.ID);
            _Nodes.Add(n);

            if (n.HasComponent<MeshComponent>())
                _MeshNodes.Add(n);

            

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
            _entityIDList.Clear();
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
                        GLMeshBufferManager.RemoveRenderInstance(ref mc.MeshVao, mc);
                    }
                    else if (!td.IsOccluded && td.WasOccluded)
                    {
                        mc.RenderInstanceID = GLMeshBufferManager.AddRenderInstance(ref mc.MeshVao, td);
                    }
                    else if (!td.IsOccluded)
                    {
                        GLMeshBufferManager.SetInstanceWorldMat(mc.MeshVao, mc.InstanceID, td.WorldTransformMat);
                        GLMeshBufferManager.SetInstanceWorldMatInv(mc.MeshVao, mc.InstanceID, td.InverseTransformMat);
                    }
                    td.IsUpdated = false; //Reset updated status to prevent further updates on the same frame update
                }
            }
        }

        //TODO: I think search by entity is not that useful.
        public SceneGraphNode FindSceneGraphNodeFromEntity(Entity e)
        {
            //At first find the scene that e belongs
            if (!_entityIDList.Contains(e.ID))
            {
                return null;
            }

            //Fetch root SceneGraphNode
            SceneGraphNode res = null;
            Root.findNodeByID(e.ID, ref res);

            return res;
        }

    }
}
