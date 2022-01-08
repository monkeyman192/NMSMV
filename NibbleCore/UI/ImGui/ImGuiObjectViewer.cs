using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using NbCore;
using NbCore.Systems;

using ImGuiCore = ImGuiNET.ImGui;

namespace NbCore.UI.ImGui
{
    public class ImGuiObjectViewer
    {
        private SceneGraphNode _model;
        private ImGuiManager _manager;
        
        //Imgui variables to reference 
        private int current_material_sampler = 0;
        private int current_material_flag = 0;
        
        public ImGuiObjectViewer(ImGuiManager mgr)
        {
            _manager = mgr;
        }

        public void SetModel(SceneGraphNode m)
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
                    case SceneNodeType.MODEL:
                    case SceneNodeType.LOCATOR:
                        DrawLocator();
                        break;
                    case SceneNodeType.MESH:
                        DrawMesh();
                        break;
                    case SceneNodeType.LIGHT:
                        DrawLight();
                        break;
                    default:
                        ImGuiNET.ImGui.Text("Not Supported yet");
                        break;
                }
            }
                
            //ImGui.End();
        
        }

        private void RequestNodeUpdateRecursive(SceneGraphNode n)
        {
            _manager.EngineRef.transformSys.RequestEntityUpdate(n);
            
            foreach (SceneGraphNode child in n.Children)
                RequestNodeUpdateRecursive(child);
        }

        private void DrawModel()
        {
            GUIDComponent gc = _model.GetComponent<GUIDComponent>() as GUIDComponent;
            //Name
            ImGuiCore.Columns(2);
            ImGuiCore.Text("GUID");
            ImGuiCore.Text("Type");
            ImGuiCore.Text("LOD");
            ImGuiCore.Text("Name");

            ImGuiCore.NextColumn();
            ImGuiCore.Text(gc.ID.ToString());
            ImGuiCore.Text(_model.Type.ToString());
            ImGuiCore.Text("TODO");
            ImGuiCore.InputText("##Name", ref _model.Name, 30);

            ImGuiCore.Columns(1);
            


            //ImGui.Text(_model.LODNumber.ToString());

            ImGuiCore.Columns(1);
            //TODO LOD Distances

            //Draw Transform
            TransformData td = TransformationSystem.GetEntityTransformData(_model);
            if (ImGuiCore.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                //Draw TransformMatrix
                bool transform_changed = false;
                ImGuiCore.Columns(4);
                ImGuiCore.Text("Translation");
                ImGuiCore.NextColumn();
                transform_changed |= ImGuiCore.DragFloat("##TransX", ref td.TransX, 0.005f);
                ImGuiCore.NextColumn();
                transform_changed |= ImGuiCore.DragFloat("##TransY", ref td.TransY, 0.005f);
                ImGuiCore.NextColumn();
                transform_changed |= ImGuiCore.DragFloat("##TransZ", ref td.TransZ, 0.005f);
                ImGuiCore.NextColumn();
                ImGuiCore.Text("Rotation");
                ImGuiCore.NextColumn();
                transform_changed |= ImGuiCore.DragFloat("##RotX", ref td.RotX);
                ImGuiCore.NextColumn();
                transform_changed |= ImGuiCore.DragFloat("##RotY", ref td.RotY);
                ImGuiCore.NextColumn();
                transform_changed |= ImGuiCore.DragFloat("##RotZ", ref td.RotZ);
                ImGuiCore.NextColumn();
                ImGuiCore.Text("Scale");
                ImGuiCore.NextColumn();
                transform_changed |= ImGuiCore.DragFloat("##ScaleX", ref td.ScaleX, 0.005f);
                ImGuiCore.NextColumn();
                transform_changed |= ImGuiCore.DragFloat("##ScaleY", ref td.ScaleY, 0.005f);
                ImGuiCore.NextColumn();
                transform_changed |= ImGuiCore.DragFloat("##ScaleZ", ref td.ScaleZ, 0.005f);
                ImGuiCore.Columns(1);

                if (transform_changed)
                    RequestNodeUpdateRecursive(_model);
            }
            
            //Draw Components
            
            //MeshComponent
            if (_model.HasComponent<MeshComponent>())
            {
                MeshComponent mc = _model.GetComponent<MeshComponent>() as MeshComponent;
                MeshMaterial mm = mc.Material;
                if (ImGuiCore.CollapsingHeader("Mesh Component", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGuiCore.Columns(2);
                    ImGuiCore.Text("Instance ID");
                    ImGuiCore.Text("Material");
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text(mc.InstanceID.ToString());
                    if (mm != null)
                    {
                        ImGuiCore.Text(mm.Name);
                        ImGuiCore.SameLine();
                        if (ImGuiCore.Button("-"))
                            Console.WriteLine("Remove Material not implemented yet");
                    }
                    else
                    {
                        ImGuiCore.Text("Null");
                        ImGuiCore.SameLine();
                        if (ImGuiCore.Button("+"))
                            Console.WriteLine("Add Material not implemented yet");
                    }
                    
                    ImGuiCore.Columns(1);
                    
                    if (ImGuiCore.TreeNode("Mesh"))
                    {
                        NbMesh mesh = mc.Mesh;
                        ImGuiCore.Columns(2);
                        ImGuiCore.Text("Instance Count");
                        ImGuiCore.NextColumn();
                        ImGuiCore.Text(mesh.InstanceCount.ToString());
                        ImGuiCore.Columns(1);
                        if (ImGuiCore.TreeNode("MetaData"))
                        {
                            ImGuiCore.Columns(2);
                            ImGuiCore.Text("Hash");
                            ImGuiCore.Text("BatchCount");
                            ImGuiCore.Text("Vertex Start Graphics");
                            ImGuiCore.Text("Vertex End Graphics");
                            ImGuiCore.NextColumn();
                            ImGuiCore.Text(mesh.MetaData.Hash.ToString());
                            ImGuiCore.Text(mesh.MetaData.BatchCount.ToString());
                            ImGuiCore.Text(mesh.MetaData.VertrStartGraphics.ToString());
                            ImGuiCore.Text(mesh.MetaData.VertrEndGraphics.ToString());
                            ImGuiCore.TreePop();
                        }
                        ImGuiCore.Columns(1);
                        ImGuiCore.TreePop();
                    }
                }
            }

            //LightComponent
            if (_model.HasComponent<LightComponent>())
            {
                LightComponent lc = _model.GetComponent<LightComponent>() as LightComponent;

                if (ImGuiCore.CollapsingHeader("Light Component", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGuiCore.Columns(2);
                    ImGuiCore.Text("Intensity");
                    ImGuiCore.NextColumn();
                    ImGuiCore.InputFloat("##Intensity", ref lc.Intensity);
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text("FOV");
                    ImGuiCore.NextColumn();
                    ImGuiCore.InputFloat("##fov", ref lc.FOV);
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text("FallOff");
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text(lc.Falloff.ToString());
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text("Color");
                    ImGuiCore.NextColumn();
                    
                    System.Numerics.Vector3 v = new(lc.Color.X, lc.Color.Y, lc.Color.Z);
                    if (ImGuiCore.ColorPicker3("##Color", ref v))
                    {
                        lc.Color = new(v.X, v.Y, v.Z);
                    }
                    ImGuiCore.Columns(1);
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
