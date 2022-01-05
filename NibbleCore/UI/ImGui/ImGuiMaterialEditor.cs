using System;
using NbCore;
using NbCore.Common;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;


namespace NbCore.UI.ImGui
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

            if (ImGuiNET.ImGui.Combo("##1", ref _SelectedId, items, items.Length))
                _ActiveMaterial = materialList[_SelectedId];

            ImGuiNET.ImGui.SameLine();

            if (ImGuiNET.ImGui.Button("Add"))
            {
                Console.WriteLine("Todo Create Material");
            }
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.Button("Del"))
            {
                Console.WriteLine("Todo Delete Material");
            }
            if (_ActiveMaterial is null)
            {
                ImGuiNET.ImGui.Text("NULL");
                return;
            }

            ImGuiNET.ImGui.Columns(2);
            ImGuiNET.ImGui.Text("Name");
            ImGuiNET.ImGui.Text("Class");
            ImGuiNET.ImGui.Text("Flags");
            ImGuiNET.ImGui.NextColumn();
            ImGuiNET.ImGui.InputText("", ref _ActiveMaterial.Name, 30);
            ImGuiNET.ImGui.Text(_ActiveMaterial.Class);
            
            //Flags
            //Create string list of flags
            List<string> flags = new();
            for (int i = 0;i<_ActiveMaterial.Flags.Count;i++)
                flags.Add(_ActiveMaterial.Flags[i].ToString());
                
            //TODO Add combobox here with all the available flags that can be selected and added to the material
            if (ImGuiNET.ImGui.Button("+"))
            {
                Console.WriteLine("Add Flag");
            }

            float lineheight = ImGuiNET.ImGui.GetTextLineHeight();
            if (ImGuiNET.ImGui.BeginListBox("##FlagsListBox", new Vector2(0, (lineheight + 5) * flags.Count)))
            {
                foreach (string flag in flags)
                {
                    ImGuiNET.ImGui.Selectable(flag);

                    if (ImGuiNET.ImGui.BeginPopupContextItem(flag, ImGuiPopupFlags.MouseButtonRight))
                    {
                        if (ImGuiNET.ImGui.MenuItem("Delete ##flag"))
                        {
                            Console.WriteLine("Delete Flag");
                        }
                        ImGuiNET.ImGui.EndPopup();
                    }
                }

                ImGuiNET.ImGui.EndListBox();
            }

            //if (ImGuiNET.ImGui.ListBox("", ref current_material_flag, flags.ToArray(), flags.Count, System.Math.Min(flags.Count, 5)))
            //{
            //    if (ImGuiNET.ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            //    {
            //        Console.WriteLine("ListBox event1");
            //    }
            //}
            
            ImGuiNET.ImGui.NextColumn();
            ImGuiNET.ImGui.Text("Samplers");
            ImGuiNET.ImGui.NextColumn();
            if (ImGuiNET.ImGui.Button("+"))
            {
                Console.WriteLine("Adding Sampler");
            }

            List<string> samplers = new();
            for (int i=0;i<_ActiveMaterial.Samplers.Count;i++)
                samplers.Add(_ActiveMaterial.Samplers[i].Name);
            if (samplers.Count > 0)
            {
                if (ImGuiNET.ImGui.ListBox("", ref current_material_sampler, samplers.ToArray(), samplers.Count, System.Math.Min(samplers.Count, 5)))
                {
                    Sampler current_sampler = _ActiveMaterial.Samplers[current_material_sampler];
                    ImGuiNET.ImGui.Text(current_sampler.Name);
                    ImGuiNET.ImGui.Text(current_sampler.Map);
                }
                ImGuiNET.ImGui.SameLine();
            }
            
            
            //Uniforms
            ImGuiNET.ImGui.NextColumn();
            ImGuiNET.ImGui.Text("Uniforms");
            ImGuiNET.ImGui.NextColumn();
            ImGuiNET.ImGui.NextColumn();
            List<string> uniforms = new();
            for (int i = 0; i < _ActiveMaterial.Uniforms.Count; i++)
            {
                Uniform un = _ActiveMaterial.Uniforms[i];
                ImGuiNET.ImGui.Text(un.Name);
                ImGuiNET.ImGui.NextColumn();
                Vector4 val = new Vector4();
                val.X = un.Values.X;
                val.Y = un.Values.Y;
                val.Z = un.Values.Z;
                val.W = un.Values.W;
                if (ImGuiNET.ImGui.InputFloat4("", ref val))
                {
                    un.Values.X = val.X;
                    un.Values.Y = val.Y;
                    un.Values.Z = val.Z;
                    un.Values.W = val.W;
                }
            }
            ImGuiNET.ImGui.Columns(1);
        }

        public void SetMaterial(MeshMaterial mat)
        {
            _ActiveMaterial = mat;
            List<MeshMaterial> materialList = RenderState.engineRef.renderSys.MaterialMgr.Entities;
            _SelectedId = materialList.IndexOf(mat);
        }
    }
}