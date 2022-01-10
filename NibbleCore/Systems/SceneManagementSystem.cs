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
            Scene scn = new Scene();
            scn.SetID(sceneID);
            
            //Create root
            SceneGraphNode sceneRoot = EngineRef.CreateSceneNode("SCENE ROOT");
            scn.SetRoot(sceneRoot);
            
            _SceneMap[sceneID] = scn; //Register
            return scn;
        }

        public void DeleteScene(int id)
        {
            //TODO: Add Scene dispose method to also dispose the root node
            ClearScene(_SceneMap[id]);
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
            UpdateScene(ActiveScene);
        }

        public void UpdateAllScenes()
        {
            foreach (Scene s in _SceneMap.Values)
                UpdateScene(s);
        }

        public override void OnFrameUpdate(double dt)
        {
            
        }

        public override void OnRenderUpdate(double dt)
        {
            UpdateActiveScene();
        }

        public void UpdateScene(Scene s)
        {
            //Add instances to all non occluded Nodes
            foreach (SceneGraphNode n in s.MeshNodes)
            {
                TransformData td = TransformationSystem.GetEntityTransformData(n);
                MeshComponent mc = n.GetComponent<MeshComponent>() as MeshComponent;

                if (td.IsUpdated)
                {
                    if (td.IsOccluded && !td.WasOccluded)
                    {
                        //Remove Instance
                        Console.WriteLine("Removing Instance {0}", n.Name);
                        //TODO: Maybe it is  a good idea to keep queues for 
                        //instances that will be removed and instance that will be added
                        //which will be passed per frame update to the rendering system
                        //which has direct access to the renderer
                        EngineRef.renderSys.Renderer.RemoveRenderInstance(ref mc.Mesh, mc);
                    }
                    else if (!td.IsOccluded && td.WasOccluded)
                    {
                        Console.WriteLine("Adding Instance {0}", n.Name);
                        EngineRef.renderSys.Renderer.AddRenderInstance(ref mc, td);
                    }
                    else if (!td.IsOccluded)
                    {
                        EngineRef.renderSys.Renderer.SetInstanceWorldMat(mc.Mesh, mc.InstanceID, td.WorldTransformMat);
                        EngineRef.renderSys.Renderer.SetInstanceWorldMatInv(mc.Mesh, mc.InstanceID, td.InverseTransformMat);
                    }

                    td.IsUpdated = false; //Reset updated status to prevent further updates on the same frame update
                }
            }

            //Process Lights
            foreach (SceneGraphNode n in s.LightNodes)
            {
                TransformData td = TransformationSystem.GetEntityTransformData(n);
                LightComponent lc = n.GetComponent<LightComponent>() as LightComponent;

                if (!lc.Data.IsRenderable && lc.InstanceID != -1)
                {
                    //Remove Instance
                    Console.WriteLine("Removing Instance {0}", n.Name);
                    //TODO: Maybe it is  a good idea to keep queues for 
                    //instances that will be removed and instance that will be added
                    //which will be passed per frame update to the rendering system
                    //which has direct access to the renderer
                    EngineRef.renderSys.Renderer.RemoveLightRenderInstance(ref lc.Mesh, lc);
                }
                else if (lc.Data.IsRenderable && lc.InstanceID == -1)
                {
                    Console.WriteLine("Adding Instance {0}", n.Name);
                    EngineRef.renderSys.Renderer.AddLightRenderInstance(ref lc, td);
                }
                else if (lc.Data.IsRenderable)
                {
                    EngineRef.renderSys.Renderer.SetInstanceWorldMat(lc.Mesh, lc.InstanceID, td.WorldTransformMat);
                }
                
                if (lc.Data.IsUpdated && lc.InstanceID != -1)
                {
                    EngineRef.renderSys.Renderer.SetLightInstanceData(lc);
                    lc.Data.IsUpdated = false;
                }
                    
            }
        }

        public void ClearScene(Scene s)
        {
            foreach (SceneGraphNode node in s.Nodes)
                EngineRef.DisposeSceneGraphNode(node);
            
            s.Root.Children.Clear();
            s.Nodes.Clear();
            s.MeshNodes.Clear();
            s.LightNodes.Clear();
        }

        public override void CleanUp()
        {
            //TODO : Check if more has to be cleaned up or if the registry system will handle everything
            foreach (Scene s in _SceneMap.Values)
                ClearScene(s);
            _SceneMap.Clear();
        }
    }
}
