using MVCore.Systems;
using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore
{
    public class Scene
    {
        public int ID = -1;
        private readonly List<long> _entityIDList;
        private SceneGraphNode root = null;
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

        public void AddNode(SceneGraphNode n, bool Isroot = false)
        {
            if (_entityIDList.Contains(n.ID))
            {
                Common.Callbacks.Log(string.Format("Node {0} already belongs to scene {1}", n.ID, ID),
                    Common.LogVerbosityLevel.WARNING);
                return;
            }

            _entityIDList.Add(n.ID);
            _Nodes.Add(n);

            if (n.HasComponent<MeshComponent>())
                _MeshNodes.Add(n);

            if (Isroot)
                root = n;

        }

        public void Clear()
        {
            _Nodes.Clear();
            _MeshNodes.Clear();
            _entityIDList.Clear();
            root = null;
        }

        public void Update()
        {
            //Add instances to all non occluded Nodes
            foreach (SceneGraphNode n in _MeshNodes)
            {
                TransformData td = TransformationSystem.GetEntityTransformData(n);
                MeshComponent mc = n.GetComponent<MeshComponent>() as MeshComponent;

                if (td.IsOccluded && !td.WasOccluded)
                {
                    //Remove Instance
                    GLMeshBufferManager.RemoveInstance(ref mc.MeshVao, mc);
                }
                else if (!td.IsOccluded && td.WasOccluded)
                {
                    mc.InstanceID = GLMeshBufferManager.AddInstance(ref mc.MeshVao, td, mc);
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
            root.findNodeByID(e.ID, ref res);

            return res;
        }

    }
}
