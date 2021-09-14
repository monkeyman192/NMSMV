using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using MVCore;


namespace ImGuiHelper
{
    
    class ImGuiSceneGraphViewer
    {
        private SceneGraphNode _root = null;
        private Dictionary<long, SceneGraphNode> ModelMap = new();
        private SceneGraphNode _selected = null;
        private SceneGraphNode _clicked = null;
        private bool showctxmenu = false;

        //Inline AddChild Function
        private static void AddChild(SceneGraphNode m, SceneGraphNode n) => m.Children.Add(n);

        public ImGuiSceneGraphViewer()
        {
               
        }
        
        private void AddModelToMap(SceneGraphNode n)
        {
            if (!ModelMap.ContainsKey(n.ID))
                ModelMap[n.ID] = n;
        }
        
        public void Traverse_Init(SceneGraphNode m)
        {
            AddModelToMap(m);
            foreach (SceneGraphNode child in m.Children)
                Traverse_Init(child);
        }
        
        public void Clear()
        {
            ModelMap.Clear();
            _root = null;
            _selected = null;
            _clicked = null;
        }

        public void Init(SceneGraphNode root)
        {
            Clear();

            //Setup root
            _root = root;
            Traverse_Init(root);
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

            bool node_open = ImGui.TreeNodeEx(n.Name, base_flags);
            
            n.IsOpen = node_open;
            System.Numerics.Vector2 ctxPos = Vector2.Zero;
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                _clicked = n;
                ImGuiManager.SetObjectReference(n);
                ImGuiManager.SetActiveMaterial(n);
                ImGui.CloseCurrentPopup();
            } 
            if (ImGui.BeginPopupContextItem()) // <-- use last item id as popup id
            {
                if (ImGui.BeginMenu("Add Child##child-ctx"))
                {
                    if (ImGui.MenuItem("Add Locator"))
                    {
                        Console.WriteLine("Add new locator node as a child to selected node");
                    }
                    
                    if (ImGui.MenuItem("Add Light"))
                    {
                        Console.WriteLine("Add new locator node as a child to selected node");
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.MenuItem("Delete"))
                {
                    Console.WriteLine("Delete Node permanently");
                }
                
                ImGui.EndPopup();
            }

            if (n.IsOpen)
            {
                if (n.Children.Count > 0)
                {
                    foreach (SceneGraphNode nc in n.Children)
                        DrawNode(nc);
                    ImGui.TreePop();
                }
            }

        }
        

    }
}
