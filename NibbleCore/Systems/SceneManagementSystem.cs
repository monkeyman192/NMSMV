using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore.Systems
{
    public class SceneManagementSystem : EngineSystem
    {
        private Dictionary<long, Scene> _SceneMap = new(); //Maps entities to scenes
        private int SceneCount;
        public Scene ActiveScene = null;

        public SceneManagementSystem() : base(EngineSystemEnum.SCENE_MANAGEMENT_SYSTEM)
        {
            
        }

        //For now I think one Scene is enough
        public Scene CreateScene()
        {
            int sceneID = SceneCount++;
            _SceneMap[sceneID] = new Scene(sceneID);
            return _SceneMap[sceneID];
        }

        public void DeleteScene(int id)
        {
            _SceneMap[id].Clear();
            _SceneMap.Remove(id);
        }

        public void SetActiveScene(Scene s)
        {
            SetActiveScene(s.ID);
        }

        public void SetActiveScene(int id)
        {
            if (_SceneMap.ContainsKey(id))
                ActiveScene = _SceneMap[id];
            else
                Log(string.Format("Invalid Scene ID {0}", id), 
                    Common.LogVerbosityLevel.WARNING);
        }

        public void UpdateActiveScene()
        {
            ActiveScene.Update();
        }

        public void UpdateAllScenes()
        {
            foreach (Scene s in _SceneMap.Values)
                s.Update();
        }

        public override void OnFrameUpdate(double dt)
        {
            
        }

        public override void OnRenderUpdate(double dt)
        {
            UpdateActiveScene();
        }

        public override void CleanUp()
        {
            //TODO : Check if more has to be cleaned up or if the registry system will handle everything
            _SceneMap.Clear();
        }
    }
}
