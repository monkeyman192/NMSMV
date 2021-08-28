using System;
using System.Collections.Generic;
using System.Text;
using libMBIN;

namespace MVCore
{
    public class SceneGraphNode : Entity
    {
        public bool IsSelected = false;
        public bool IsRenderable = true;
        public bool IsOpen = false;
        public SceneGraphNode ParentScene = null;
        public NMSTemplate Template = null;
        
        public SceneGraphNode()
        {

        }

        public void AddChild(SceneGraphNode m)
        {
            Children.Add(m);
            m.Parent = this;
        }

        public void RemoveChild(SceneGraphNode m)
        {
            if (Children.Contains(m))
            {
                Children.Remove(m);
                m.Parent = null;
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

    }
    
}
