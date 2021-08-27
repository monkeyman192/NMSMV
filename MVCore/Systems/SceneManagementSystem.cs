using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore.Systems
{
    public class SceneManagementSystem : EngineSystem
    {
        private Dictionary<long, long> _EntityToSceneMap = new(); //Maps entities to scenes
        private Dictionary<long, SceneGraphNode> _SceneToRootNodeMap = new(); //Maps scene ids to the root scenegraphnodes
        
        public SceneManagementSystem() : base(EngineSystemEnum.SCENE_MANAGEMENT_SYSTEM)
        {
            
        }

        public SceneGraphNode FindEntitySceneGraphNode(Entity e)
        {
            //At first find the scene that e belongs
            if (!_EntityToSceneMap.ContainsKey(e.ID))
            {
                Log("Entity not assigned to scene", Common.LogVerbosityLevel.ERROR);
                return null;
            }
            
            long sceneID = _EntityToSceneMap[e.ID];

            //Fetch root SceneGraphNode
            SceneGraphNode root = _SceneToRootNodeMap[sceneID];

            SceneGraphNode res = null;
            root.findNodeByID(e.ID, ref res);
            
            return res;
        }

        public void AddScene()
        {
            
        }

    }
}
