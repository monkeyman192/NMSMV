using System;
using MVCore;
using ImGuiNET;
using System.Collections.Generic;

namespace ImGuiHelper
{
    public class ImGuiMaterialEditor
    {
        private MeshMaterial _material = null;
        private static int current_material_flag = 0;
        private static int current_material_sampler = 0;
        public void Draw()
        {
            if (_material is null)
            {
                ImGui.Text("NULL");
                return;
            }
                
            ImGui.Columns(2);
            ImGui.Text("Name");
            ImGui.Text("Class");
            ImGui.Text("Flags");
            ImGui.NextColumn();
            ImGui.InputText("", ref _material.Name, 30);
            ImGui.Text(_material.Class);
            //Flags
            int current_item = 0;
            //Create string list of flags
            List<string> flags = new();
            for (int i = 0;i<_material.Flags.Count;i++)
                flags.Add(_material.Flags[i].ToString());
            
            if (ImGui.ListBox("", ref current_material_flag, flags.ToArray(), flags.Count, Math.Min(flags.Count, 5)))
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
            for (int i=0;i<_material.Samplers.Count;i++)
                samplers.Add(_material.Samplers[i].Name);
            if (ImGui.ListBox("", ref current_material_sampler, samplers.ToArray(), samplers.Count, Math.Min(samplers.Count, 5)))
            {
                Sampler current_sampler = _material.Samplers[current_material_sampler];
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
        }

        public void SetMaterial(MeshMaterial mat)
        {
            _material = mat;
        }
    }
}