using System;
using System.Collections.Generic;
using MVCore.Systems;
using OpenTK.Mathematics;
using MVCore.Utils;

namespace MVCore
{
    public enum SceneNodeType
    {
        MODEL=0x0,
        LOCATOR,
        JOINT,
        MESH,
        LIGHT,
        LIGHTVOLUME,
        EMITTER,
        COLLISION,
        REFERENCE,
        DECAL,
        GIZMO,
        GIZMOPART,
        TEXT,
        UNKNOWN
    }
    public class SceneGraphNode : Entity
    {
        public new SceneNodeType Type = SceneNodeType.UNKNOWN;
        public bool IsSelected = false;
        public string Name = "";
        public bool IsRenderable = true;
        public bool IsOpen = false;
        public SceneGraphNode ParentScene = null;
        public List<float> LODDistances = new();
        public SceneGraphNode Parent = null;
        public List<SceneGraphNode> Children = new();
        

        public SceneGraphNode(SceneNodeType type) : base(EntityType.SceneNode)
        {
            Type = type;
            switch (type)
            {
                case SceneNodeType.MESH:
                    base.Type = EntityType.SceneNodeMesh;
                    break;
                case SceneNodeType.MODEL:
                    base.Type = EntityType.SceneNodeModel;
                    break;
                case SceneNodeType.JOINT:
                    base.Type = EntityType.SceneNodeJoint;
                    break;
                case SceneNodeType.LIGHT:
                    base.Type = EntityType.SceneNodeLight;
                    break;
                default:
                    throw new Exception("make sure to property initialize base type");
            }
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
                TransformComponent parent_tc = Parent.GetComponent<TransformComponent>() as TransformComponent;
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

        public void resetTransform()
        {
            TransformData td = TransformationSystem.GetEntityTransformData(this);
            td.ResetTransform();
        }

        //Static Methods for node generation
        public static SceneGraphNode CreateScene(string name)
        {
            SceneGraphNode n = new(SceneNodeType.MODEL)
            {
                Name = name
            };

            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Create MeshComponent
            MeshComponent mc = new()
            {
                MeshVao = Common.RenderState.engineRef.resourceMgmtSys.GLPrimitiveMeshes["default_cross"],
                Material = Common.RenderState.engineRef.GetMaterialByName("crossMat")
            };
            
            //Register new instance in the meshVao
            mc.InstanceID = GLMeshBufferManager.AddMeshInstance(ref mc.MeshVao, mc);
            
            n.AddComponent<MeshComponent>(mc);

            //Create SceneComponent
            SceneComponent sc = new();
            n.AddComponent<SceneComponent>(sc);

            return n;
        }

        public static SceneGraphNode CreateLocator(string name)
        {
            SceneGraphNode n = new(SceneNodeType.LOCATOR)
            {
                Name = name
            };

            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Create MeshComponent
            MeshComponent mc = new()
            {
                MeshVao = Common.RenderState.engineRef.resourceMgmtSys.GLPrimitiveMeshes["default_cross"],
                Material = Common.RenderState.engineRef.GetMaterialByName("crossMat")
            };
            
            //Register new instance in the meshVao
            mc.InstanceID = GLMeshBufferManager.AddMeshInstance(ref mc.MeshVao, mc);

            n.AddComponent<MeshComponent>(mc);

            return n;
        }

        public static SceneGraphNode CreateJoint()
        {
            SceneGraphNode n = new(SceneNodeType.JOINT);
            
            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Add Mesh Component
            MeshComponent mc = new();
            mc.MeshVao = new()
            {
                type = SceneNodeType.JOINT
            };

            mc.MeshVao.vao = new Primitives.LineSegment(n.Children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            mc.Material = Common.RenderState.engineRef.GetMaterialByName("jointMat");

            //Add Joint Component
            JointComponent jc = new();
            n.AddComponent<JointComponent>(jc);
            
            return n;
        }

        public static SceneGraphNode CreateLight(string name="default light", float intensity=1.0f, 
                                                ATTENUATION_TYPE attenuation=ATTENUATION_TYPE.QUADRATIC,
                                                LIGHT_TYPE lighttype = LIGHT_TYPE.POINT)
        {
            SceneGraphNode n = new(SceneNodeType.LIGHT)
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
                    type = SceneNodeType.LIGHT,
                    vao = new Primitives.LineSegment(n.Children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO(),
                    MetaData = new()
                    {
                        BatchCount = 2
                    },
                },
                Material = Common.RenderState.engineRef.GetMaterialByName("lightMat")
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
