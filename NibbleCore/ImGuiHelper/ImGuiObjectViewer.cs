using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using NbCore;
using NbCore.Systems;

namespace ImGuiHelper
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
                        ImGui.Text("Not Supported yet");
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
            ImGui.Columns(2);
            ImGui.Text("GUID");
            ImGui.Text("Type");
            ImGui.Text("LOD");

            ImGui.NextColumn();
            ImGui.Text(gc.ID.ToString());
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
                    RequestNodeUpdateRecursive(_model);
            }
            
            //Draw Components
            
            if (_model.HasComponent<MeshComponent>())
            {
                MeshComponent mc = _model.GetComponent<MeshComponent>() as MeshComponent;
                MeshMaterial mm = mc.Material;
                if (ImGui.CollapsingHeader("Mesh Component", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Columns(2);
                    ImGui.Text("Instance ID");
                    ImGui.Text("Render Instance ID");
                    ImGui.Text("Material");
                    ImGui.NextColumn();
                    ImGui.Text(mc.InstanceID.ToString());
                    ImGui.Text(mc.RenderInstanceID.ToString());
                    if (mm != null)
                    {
                        ImGui.Text(mm.Name);
                        ImGui.SameLine();
                        if (ImGui.Button("-"))
                            Console.WriteLine("Remove Material not implemented yet");
                    }
                    else
                    {
                        ImGui.Text("Null");
                        ImGui.SameLine();
                        if (ImGui.Button("-"))
                            Console.WriteLine("Add Material not implemented yet");
                    }
                    
                    ImGui.Columns(1);
                    
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
