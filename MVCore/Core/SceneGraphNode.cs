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
        //public SceneGraphNode ParentScene = null; //Is this useful at all?
        public List<float> LODDistances = new();
        public SceneGraphNode Parent = null;
        public Scene SceneRef = null;
        public List<SceneGraphNode> Children = new();

        //Disposable Stuff
        private bool disposed = false;
        private Microsoft.Win32.SafeHandles.SafeFileHandle handle = new(IntPtr.Zero, true);

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
                case SceneNodeType.LOCATOR:
                    base.Type = EntityType.SceneNode; // Not sure if this should be any different
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

        public void SetRenderableStatusRec(bool status)
        {
            IsRenderable = status;
            TransformComponent tc = GetComponent<TransformComponent>() as TransformComponent;
            tc.Data.IsActive = status;
            
            foreach (SceneGraphNode child in Children)
                child.SetRenderableStatusRec(status);
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
            GUIDComponent gc = m.GetComponent<GUIDComponent>() as GUIDComponent;
            if (gc.ID == id)
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
