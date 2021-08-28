using System.Collections.Generic;
using MVCore.Systems;
using libMBIN.NMS.Toolkit;
using OpenTK.Mathematics;
using MVCore.Utils;

namespace MVCore
{
    public class SceneGraphNode : Entity
    {
        public bool IsSelected = false;
        public bool IsRenderable = true;
        public bool IsOpen = false;
        public SceneGraphNode ParentScene = null;
        public TkSceneNodeData Template = null;
        public List<float> LODDistances = new();
        public SceneGraphNode Parent = null;
        public List<SceneGraphNode> Children = new();

        public SceneGraphNode()
        {

        }

        public void RemoveChild(SceneGraphNode m)
        {
            if (Children.Contains(m))
            {
                Children.Remove(m);
                m.Parent = null;
            }
        }

        public void AddChild(SceneGraphNode e)
        {
            e.SetParent(this);
        }

        public void SetParent(SceneGraphNode e)
        {
            Parent = e;
            Parent.Children.Add(this);

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
            Parent.RemoveChild(this);
            
            if (HasComponent<TransformComponent>())
            {
                TransformComponent tc = GetComponent<TransformComponent>() as TransformComponent;
                tc.Data.ClearParentData();
            }
        }

        public void findNodeByID(long id, ref SceneGraphNode m)
        {
            if (ID == id)
            {
                m = this;
                return;
            }

            foreach (SceneGraphNode child in Children)
            {
                child.findNodeByID(id, ref m);
            }
        }

        public void findNodeByName(string name, ref SceneGraphNode m)
        {
            if (Name == name)
            {
                m = this;
                return;
            }

            foreach (SceneGraphNode child in Children)
            {
                child.findNodeByName(name, ref m);
            }
        }

        public void exportToMBIN()
        {
            if (Template != null)
            {
                //Fetch scene name
                string[] split = Template.Name.Value.Split('\\');
                string scnName = split[^1];

                //TODO Bring Back MBIN Export Support
                TkSceneNodeData temp = new();
                temp.WriteToMbin(scnName.ToUpper() + ".SCENE.MBIN");
                Common.Callbacks.showInfo("Scene successfully exported to " + scnName.ToUpper() + ".MBIN", "Info");
            }
        }

        public void resetTransform()
        {
            TransformData td = TransformationSystem.GetEntityTransformData(this);
            td.ResetTransform();

            //Save values to underlying SceneNode
            if (Template != null)
            {
                Template.Transform.ScaleX = td.ScaleX;
                Template.Transform.ScaleY = td.ScaleY;
                Template.Transform.ScaleZ = td.ScaleZ;
                Template.Transform.TransX = td.TransX;
                Template.Transform.TransY = td.TransY;
                Template.Transform.TransZ = td.TransZ;
                //Convert rotation from matrix to angles
                //Vector3 q_euler = quaternionToEuler(oldrotation);
                Matrix4 tempMat = Matrix4.CreateFromQuaternion(td.localRotation);
                tempMat.Transpose();
                Vector3 q_euler = MathUtils.matrixToEuler(tempMat, "ZXY");

                Template.Transform.RotX = q_euler.X;
                Template.Transform.RotY = q_euler.Y;
                Template.Transform.RotZ = q_euler.Z;
            }
        }

        //Static Methods for node generation
        public static SceneGraphNode CreateScene(string name)
        {
            SceneGraphNode n = new()
            {
                Name = name,
                Type = TYPES.MODEL
            };

            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Create MeshComponent
            MeshComponent mc = new()
            {
                MeshVao = Common.RenderState.activeResMgr.GLPrimitiveMeshes["default_cross"],
                Material = Common.RenderState.activeResMgr.GLmaterials["crossMat"]
            };

            n.AddComponent<MeshComponent>(mc);

            //Create SceneComponent
            SceneComponent sc = new();
            n.AddComponent<SceneComponent>(sc);

            return n;
        }

        public static SceneGraphNode CreateLocator(string name)
        {
            SceneGraphNode n = new()
            {
                Name = name,
                Type = TYPES.LOCATOR
            };

            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Create MeshComponent
            MeshComponent mc = new()
            {
                MeshVao = Common.RenderState.activeResMgr.GLPrimitiveMeshes["default_cross"],
                Material = Common.RenderState.activeResMgr.GLmaterials["crossMat"]
            };

            n.AddComponent<MeshComponent>(mc);

            return n;
        }

        public static SceneGraphNode CreateJoint()
        {
            SceneGraphNode n = new();

            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Add Mesh Component
            MeshComponent mc = new();
            mc.MeshVao = new()
            {
                type = TYPES.JOINT
            };

            mc.MeshVao.vao = new Primitives.LineSegment(n.Children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            mc.Material = Common.RenderState.activeResMgr.GLmaterials["jointMat"];

            //Add Joint Component
            JointComponent jc = new();
            n.AddComponent<JointComponent>(jc);
            
            return n;
        }

        public static SceneGraphNode CreateLight(string name="default light", float intensity=1.0f, 
                                                ATTENUATION_TYPE attenuation=ATTENUATION_TYPE.QUADRATIC,
                                                LIGHT_TYPE lighttype = LIGHT_TYPE.POINT)
        {
            SceneGraphNode n = new()
            {
                Name = name
            };

            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Add Mesh Component
            MeshComponent mc = new()
            {
                MeshVao = new()
                {
                    type = TYPES.LIGHT,
                    vao = new Primitives.LineSegment(n.Children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO(),
                    MetaData = new()
                    {
                        BatchCount = 2
                    },
                },
                Material = Common.RenderState.activeResMgr.GLmaterials["lightMat"]
            };
            n.AddComponent<MeshComponent>(mc);

            //Add Light Component

            LightComponent lc = new()
            {
                Intensity = intensity,
                Falloff = attenuation,
                LightType = lighttype
            };
            n.AddComponent<LightComponent>(lc);

            return n;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //Free other resources here
                if (Children != null)
                    foreach (SceneGraphNode c in Children) c.Dispose();
                Children.Clear();
                
                //Free textureManager
            }

            //Free unmanaged resources
            disposed = true;
        }

    }
    
}
