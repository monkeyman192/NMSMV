﻿using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using System;
using MVCore;

namespace ImGuiHelper
{   
    public class ImGuiManager
    {
        //ImGui Variables
        private readonly ImGuiObjectViewer ObjectViewer;
        private readonly ImGuiSceneGraphViewer SceneGraphViewer;
        private readonly ImGuiMaterialEditor MaterialEditor;
        private readonly ImGuiShaderEditor ShaderEditor;
        private ImGuiController _controller;
        public GameWindow WindowRef = null;

        //ImguiPalette Colors
        //Blue
        public static System.Numerics.Vector4 DarkBlue = new(0.04f, 0.2f, 0.96f, 1.0f);

        public ImGuiManager(GameWindow win)
        {
            WindowRef = win;
            _controller = new ImGuiController(win.ClientSize.X, win.ClientSize.Y); //Init with a start size
            
            //Enable docking by default
            ImGuiIOPtr io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable; //Enable Docking
            
            //Initialize items
            ObjectViewer = new();
            SceneGraphViewer = new(this);
            MaterialEditor = new();
            ShaderEditor = new();
        }

        //Resize available imgui space
        public virtual void Resize(int x, int y)
        {
            _controller.WindowResized(x, y);
        }

        public virtual void Update(double dt)
        {
            _controller.Update(WindowRef, (float) dt);
        }

        public virtual void Render()
        {
            _controller.Render();
        }

        public virtual void SendChar(char e)
        {
            _controller.PressChar(e);
        }

        //Material Viewer Related Methods
        public virtual void DrawMaterialEditor()
        {
            MaterialEditor?.Draw();
        }

        public virtual void SetActiveMaterial(Entity m)
        {
            if (m.HasComponent<MeshComponent>())
            {
                MeshComponent mc = m.GetComponent<MeshComponent>() as MeshComponent;
                MaterialEditor.SetMaterial(mc.Material);
            }
        }

        //Shader Editor Related Methods
        public virtual void DrawShaderEditor()
        {
            ShaderEditor?.Draw();
        }

        public virtual void SetActiveShaderSource(GLSLHelper.GLSLShaderSource s)
        {
            ShaderEditor.SetShader(s);
        }

        //Object Viewer Related Methods

        public virtual void DrawObjectInfoViewer()
        {
            ObjectViewer?.Draw();
        }

        public virtual void SetObjectReference(SceneGraphNode m)
        {
            ObjectViewer.SetModel(m);
        }

        //SceneGraph Related Methods

        public void DrawSceneGraph()
        {
            SceneGraphViewer?.Draw();
        }

        public void PopulateSceneGraph(Scene scn)
        {
            SceneGraphViewer.Init(scn.Root);
        }

        public void ClearSceneGraph()
        {
            SceneGraphViewer.Clear();
        }

        public virtual void ProcessModals(GameWindow win, string current_file_path)
        {
            //Override to provide modal processing
            throw new Exception("Not Implented!");
        }



    }





}
