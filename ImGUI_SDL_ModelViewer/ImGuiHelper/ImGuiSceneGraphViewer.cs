﻿using System;
using System.Collections.Generic;
using ImGuiNET;
using MVCore;


namespace ImGuiHelper
{
    
    class ImGuiSceneGraphViewer
    {
        internal class SceneGraphNode
        {
            public Model refModel = null;
            public bool isSelected = false;
            public bool isOpen = false;
            public List<SceneGraphNode> Children = new();
            
            public SceneGraphNode()
            {
                                
            }

            public void RemoveChild(SceneGraphNode m)
            {
                if (Children.Contains(m))
                    Children.Remove(m);
            }

        }
        
        private SceneGraphNode _root = null;
        private Dictionary<int, SceneGraphNode> ModelMap = new();
        private SceneGraphNode _selected = null;
        private SceneGraphNode _clicked = null;

        //Inline AddChild Function
        private static void AddChild(SceneGraphNode m, SceneGraphNode n) => m.Children.Add(n);

        public ImGuiSceneGraphViewer()
        {
               
        }
        
        private void AddModelToMap(SceneGraphNode n)
        {
            if (!ModelMap.ContainsKey(n.refModel.ID))
                ModelMap[n.refModel.ID] = n;
        }

        public SceneGraphNode Traverse_Init(Model m)
        {
            SceneGraphNode n = new SceneGraphNode();
            n.refModel = m;
            foreach (Model child in m.Children)
                AddChild(n, Traverse_Init(child));
            
            AddModelToMap(n);
            return n;
        }
        
        public void Clear()
        {
            ModelMap.Clear();
            _root = null;
            _selected = null;
            _clicked = null;
        }

        public void Init(Model root)
        {
            Clear();
            
            //Setup root
            _root = Traverse_Init(root);
        }

        public bool AddChild(Model m, Model child)
        {
            if (!ModelMap.ContainsKey(m.ID))
                return false;

            if (!ModelMap.ContainsKey(child.ID))
                return false;

            AddChild(ModelMap[m.ID], ModelMap[child.ID]);

            return true;
        }
        
        public void Draw()
        {
            DrawNode(_root);
        }

        private void DrawNode(SceneGraphNode n)
        {
            if (n is null)
                return;
            
            //Draw using ImGUI
            ImGuiTreeNodeFlags base_flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;

            if (n.Children.Count == 0)
                base_flags |= ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Leaf;

            if (_clicked != null && n == _clicked)

            {
                base_flags |= ImGuiTreeNodeFlags.Selected;
                _selected = n;
            }

            bool node_open = ImGui.TreeNodeEx(n.refModel.name, base_flags);
            n.isOpen = node_open;
            
            if (ImGui.IsItemClicked())
            {
                _clicked = n;
                ImGuiManager.SetObjectReference(n.refModel);
            }

            if (n.isOpen)
            {
                if (n.Children.Count > 0)
                {
                    foreach (SceneGraphNode nc in n.Children)
                        DrawNode(nc);
                }
                ImGui.TreePop();
            }


        }
        

    }
}
