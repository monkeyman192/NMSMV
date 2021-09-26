using System;
using MVCore;
using MVCore.Common;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;


namespace ImGuiHelper
{
    public class ImGuiMaterialEditor
    {
        private MeshMaterial _ActiveMaterial = null;
        private static int current_material_flag = 0;
        private static int current_material_sampler = 0;
        private int _SelectedId = -1; 
        public void Draw()
        {
            //Items
            List<MeshMaterial> materialList = RenderState.engineRef.renderSys.MaterialMgr.Entities;
            string[] items = new string[materialList.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = materialList[i].Name == "" ? "Material_" + i : materialList[i].Name; 

            if (ImGui.Combo("##1", ref _SelectedId, items, items.Length))
                _ActiveMaterial = materialList[_SelectedId];
            
            ImGui.SameLine();

            if (ImGui.Button("Add"))
            {
                Console.WriteLine("Todo Create Shader");
            }
            ImGui.SameLine();
            if (ImGui.Button("Del"))
            {
                Console.WriteLine("Todo Delete Shader");
            }
            if (_ActiveMaterial is null)
            {
                ImGui.Text("NULL");
                return;
            }
                
            ImGui.Columns(2);
            ImGui.Text("Name");
            ImGui.Text("Class");
            ImGui.Text("Flags");
            ImGui.NextColumn();
            ImGui.InputText("", ref _ActiveMaterial.Name, 30);
            ImGui.Text(_ActiveMaterial.Class);
            
            //Flags
            //Create string list of flags
            List<string> flags = new();
            for (int i = 0;i<_ActiveMaterial.Flags.Count;i++)
                flags.Add(_ActiveMaterial.Flags[i].ToString());
            
            if (ImGui.ListBox("", ref current_material_flag, flags.ToArray(), flags.Count, Math.Min(flags.Count, 5)))
            {
                Console.WriteLine("ListBox event");
            }
            ImGui.SameLine();
            if (ImGui.Button("Add"))
            {
                Console.WriteLine("Todo Add Material");
            }
            ImGui.SameLine();
            
            if (ImGui.Button("Del"))
            {
                Console.WriteLine("Todo Delete Material");
            }
            
            ImGui.NextColumn();
            ImGui.Text("Samplers");
            ImGui.NextColumn();
            List<string> samplers = new();
            for (int i=0;i<_ActiveMaterial.Samplers.Count;i++)
                samplers.Add(_ActiveMaterial.Samplers[i].Name);
            if (ImGui.ListBox("", ref current_material_sampler, samplers.ToArray(), samplers.Count, Math.Min(samplers.Count, 5)))
            {
                Sampler current_sampler = _ActiveMaterial.Samplers[current_material_sampler];
                ImGui.Text(current_sampler.Name);
                ImGui.Text(current_sampler.Map);
            }
            
            ImGui.SameLine();
            if (ImGui.Button("+"))
            {
                Console.WriteLine("Adding Sampler");
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("-"))
            {
                Console.WriteLine("Removing Sampler");   
            }
            //Uniforms
            ImGui.NextColumn();
            ImGui.Text("Uniforms");
            ImGui.NextColumn();
            ImGui.NextColumn();
            List<string> uniforms = new();
            for (int i = 0; i < _ActiveMaterial.Uniforms.Count; i++)
            {
                Uniform un = _ActiveMaterial.Uniforms[i];
                ImGui.Text(un.Name);
                ImGui.NextColumn();
                Vector4 val = new Vector4();
                val.X = un.Values.X;
                val.Y = un.Values.Y;
                val.Z = un.Values.Z;
                val.W = un.Values.W;
                if (ImGui.InputFloat4("", ref val))
                {
                    un.Values.X = val.X;
                    un.Values.Y = val.Y;
                    un.Values.Z = val.Z;
                    un.Values.W = val.W;
                }
            }
            ImGui.Columns(1);
        }

        public void SetMaterial(MeshMaterial mat)
        {
            _ActiveMaterial = mat;
            List<MeshMaterial> materialList = RenderState.engineRef.renderSys.MaterialMgr.Entities;
            _SelectedId = materialList.IndexOf(mat);
        }
    }
}