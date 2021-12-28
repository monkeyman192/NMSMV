using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiCore = ImGuiNET.ImGui;
using NbCore;
using NbCore.Common;


namespace NbCore.UI.ImGui
{
    
    public class ImGuiSceneGraphViewer
    {
        private SceneGraphNode _root = null;
        private SceneGraphNode _selected = null;
        private SceneGraphNode _clicked = null;
        private bool showctxmenu = false;
        private ImGuiManager _manager = null;

        //Inline AddChild Function
        private static void AddChild(SceneGraphNode m, SceneGraphNode n) => m.Children.Add(n);

        public ImGuiSceneGraphViewer(ImGuiManager manager)
        {
            _manager = manager;
        }
        
        public void Traverse_Init(SceneGraphNode m)
        {
            foreach (SceneGraphNode child in m.Children)
                Traverse_Init(child);
        }
        
        public void Clear()
        {
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

        public void Draw()
        {
            DrawNode(_root);
        }

        private void DrawNode(SceneGraphNode n)
        {
            if (n is null)
                return;

            //Draw using ImGUI
            ImGuiNET.ImGuiTreeNodeFlags base_flags = ImGuiNET.ImGuiTreeNodeFlags.OpenOnArrow | 
                                                     ImGuiNET.ImGuiTreeNodeFlags.SpanAvailWidth;

            if (n.Children.Count == 0)
                base_flags |= ImGuiNET.ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiNET.ImGuiTreeNodeFlags.Leaf;

            if (_clicked != null && n == _clicked)
            {
                base_flags |= ImGuiNET.ImGuiTreeNodeFlags.Selected;
                _selected = n;
            }

            //DrawCheckbox for non root nodes
            if (n != _root)
            {
                if (ImGuiCore.Checkbox("##Entity" + n.GetID(), ref n.IsRenderable))
                {
                    n.SetRenderableStatusRec(n.IsRenderable);
                }
                ImGuiCore.SameLine();    
            }

            ImGuiCore.SetNextItemOpen(n.IsOpen);
            bool node_open = ImGuiCore.TreeNodeEx(n.Name, base_flags);
            
            n.IsOpen = node_open;
            Vector2 ctxPos = Vector2.Zero;
            if (ImGuiCore.IsItemClicked(ImGuiNET.ImGuiMouseButton.Left))
            {
                _clicked = n;
                _manager.SetObjectReference(n);
                _manager.SetActiveMaterial(n);
                ImGuiCore.CloseCurrentPopup();
            } 
            if (ImGuiCore.BeginPopupContextItem()) // <-- use last item id as popup id
            {
                if (ImGuiCore.BeginMenu("Add Child Node##child-ctx"))
                {
                    bool EntityAdded = false;
                    SceneGraphNode new_node = null;
                    if (ImGuiCore.MenuItem("Add Locator"))
                    {
                        //Create and register locator node
                        new_node = _manager.EngineRef.CreateLocatorNode("Locator#1");
                        Callbacks.Log("Creating Locator node", LogVerbosityLevel.INFO);
                        EntityAdded = true;
                    }
                    
                    if (ImGuiCore.MenuItem("Add Light"))
                    {
                        //Create and register locator node
                        new_node = _manager.EngineRef.CreateLightNode("Light#1");
                        Callbacks.Log("Creating Light node", LogVerbosityLevel.INFO);
                        EntityAdded = true;
                    }

                    if (EntityAdded)
                    {
                        //Register new locator node to engine
                        _manager.EngineRef.RegisterEntity(new_node);
                        
                        //Add locator the activeScene
                        Scene activeScene = _manager.EngineRef.GetActiveScene();
                        activeScene.AddNode(new_node);
                        _manager.EngineRef.transformSys.RequestEntityUpdate(new_node);
                        
                        //Set parent
                        new_node.SetParent(n);

                        n.IsOpen = true; //Make sure to open the node so that the new node is visible

                        //Set Reference to the new node
                        _clicked = new_node;
                        _manager.SetObjectReference(new_node);
                        _manager.SetActiveMaterial(new_node);

                    }


                    ImGuiCore.EndMenu();
                }

                if (ImGuiCore.MenuItem("Delete"))
                {
                    Console.WriteLine("Delete Node permanently");
                }

                ImGuiCore.EndPopup();
            }

            if (n.IsOpen)
            {
                if (n.Children.Count > 0)
                {
                    foreach (SceneGraphNode nc in n.Children)
                    {
                        DrawNode(nc);
                    }

                    ImGuiCore.TreePop();
                }
            }

        }
        

    }
}
