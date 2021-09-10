﻿using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using MVCore;
using MVCore.Systems;

namespace ImGuiHelper
{
    class ImGuiObjectViewer
    {
        private Entity _model;
        
        //Imgui variables to reference 
        private int current_material_sampler = 0;
        private int current_material_flag = 0;
        
        public ImGuiObjectViewer(){
        
        }

        public void SetModel(Entity m)
        {
            if (m == null)
                return;
            _model = m;
        }

        public void Draw()
        {

            //Assume that a Popup has begun
            //ImGui.Begin("Info", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);


            if (_model != null)
            {
                switch (_model.Type)
                {
                    case TYPES.MODEL:
                    case TYPES.LOCATOR:
                        DrawLocator();
                        break;
                    case TYPES.MESH:
                        DrawMesh();
                        break;
                    case TYPES.LIGHT:
                        DrawLight();
                        break;
                    default:
                        ImGui.Text("Not Supported yet");
                        break;
                }
            }
                
            //ImGui.End();
        
        }

        private void DrawModel()
        {
            //Name
            ImGui.Columns(2);
            ImGui.Text("ID");
            ImGui.Text("Type");
            ImGui.Text("LOD");

            ImGui.NextColumn();
            ImGui.Text(_model.ID.ToString());
            ImGui.Text(_model.Type.ToString());
            ImGui.Text("TODO");

            ImGui.Columns(1);
            ImGui.InputText("Name", ref _model.Name, 30);
            
            
            //ImGui.Text(_model.LODNumber.ToString());

            ImGui.Columns(1);
            //TODO LOD Distances

            //Draw Transform
            TransformData td = TransformationSystem.GetEntityTransformData(_model);
            if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                //Draw TransformMatrix
                bool transform_changed = false;
                ImGui.Columns(3);
                transform_changed |= ImGui.DragFloat("TransX", ref td.TransX, 0.005f);
                transform_changed |= ImGui.DragFloat("RotX", ref td.RotX);
                transform_changed |= ImGui.DragFloat("ScaleX", ref td.ScaleX, 0.005f);
                ImGui.NextColumn();
                transform_changed |= ImGui.DragFloat("TransY", ref td.TransY, 0.005f);
                transform_changed |= ImGui.DragFloat("RotY", ref td.RotY);
                transform_changed |= ImGui.DragFloat("ScaleY", ref td.ScaleY, 0.005f);
                ImGui.NextColumn();
                transform_changed |= ImGui.DragFloat("TransZ", ref td.TransZ, 0.005f);
                transform_changed |= ImGui.DragFloat("RotZ", ref td.RotZ);
                transform_changed |= ImGui.DragFloat("ScaleZ", ref td.ScaleZ, 0.005f);
                ImGui.Columns(1);

                if (transform_changed)
                    MVCore.Common.RenderState.engineRef.transformSys.RequestEntityUpdate(_model);
            }
            
            //Draw Components
            
            if (_model.HasComponent<MeshComponent>())
            {
                MeshComponent mc = _model.GetComponent<MeshComponent>() as MeshComponent;
                if (ImGui.CollapsingHeader("Mesh Component", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Columns(2);
                    ImGui.Text("Instance ID");
                    ImGui.Text("Render Instance ID");
                    ImGui.NextColumn();
                    ImGui.Text(mc.InstanceID.ToString());
                    ImGui.Text(mc.RenderInstanceID.ToString());
                    ImGui.Columns(1);
                    MeshMaterial mm = mc.Material;
                    if (ImGui.TreeNode("Material"))
                    {
                        ImGui.Columns(2);
                        ImGui.Text("Name");
                        ImGui.Text("Class");
                        ImGui.Text("Flags");
                        ImGui.NextColumn();
                        ImGui.Text(mm.Name);
                        ImGui.Text(mm.Class);
                        //Flags
                        int current_item = 0;
                        //Create string list of flags
                        List<string> flags = new();
                        for (int i = 0;i<mm.Flags.Count;i++)
                            flags.Add(mm.Flags[i].ToString());
                        
                        if (ImGui.ListBox("", ref current_material_flag, flags.ToArray(), flags.Count, 5))
                        {
                            Console.WriteLine("ListBox event");
                        }
                        
                        if (ImGui.Button("+"))
                        {
                            
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("-"))
                        {
                            
                        }
                        ImGui.NextColumn();
                        ImGui.Text("Samplers");
                        ImGui.NextColumn();
                        List<string> samplers = new();
                        for (int i=0;i<mm.Samplers.Count;i++)
                            samplers.Add(mm.Samplers[i].Name);
                        if (ImGui.ListBox("", ref current_material_sampler, samplers.ToArray(), samplers.Count, 5))
                        {
                            Sampler current_sampler = mm.Samplers[current_material_sampler];
                            ImGui.Text(current_sampler.Name);
                            ImGui.Text(current_sampler.Map);
                        }
                        
                        if (ImGui.Button("+"))
                        {
                            Console.WriteLine("Adding Sampler");
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("-"))
                        {
                            Console.WriteLine("Removing Sampler");   
                        }
                        
                        ImGui.Columns(1);
                        ImGui.TreePop();
                    }

                    if (ImGui.TreeNode("Mesh"))
                    {
                        GLInstancedMesh mesh = mc.MeshVao;
                        ImGui.Columns(2);
                        ImGui.Text("Instance Count");
                        ImGui.Text("Rendered Instance Count");
                        ImGui.NextColumn();
                        ImGui.Text(mesh.InstanceCount.ToString());
                        ImGui.Text(mesh.RenderedInstanceCount.ToString());
                        ImGui.Columns(1);
                        if (ImGui.TreeNode("MetaData"))
                        {
                            ImGui.Columns(2);
                            ImGui.Text("Hash");
                            ImGui.Text("BatchCount");
                            ImGui.Text("Vertex Start Graphics");
                            ImGui.Text("Vertex End Graphics");
                            ImGui.NextColumn();
                            ImGui.Text(mesh.MetaData.Hash.ToString());
                            ImGui.Text(mesh.MetaData.BatchCount.ToString());
                            ImGui.Text(mesh.MetaData.VertrStartGraphics.ToString());
                            ImGui.Text(mesh.MetaData.VertrEndGraphics.ToString());
                            ImGui.TreePop();
                        }
                        ImGui.Columns(1);
                        ImGui.TreePop();
                    }
                }
            }

        
        }

        private void DrawLocator()
        {
            DrawModel();
            //TODO Add Locator Stuff
        }

        private void DrawMesh()
        {
            DrawModel();
            //TODO add Mesh Stuff
        }

        private void DrawLight()
        {
            DrawModel();
            //Todo add Light Stuff
        }

        
        ~ImGuiObjectViewer()
        {

        }



    }
}
