using System;
using NbCore;
using NbCore.Common;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;


namespace ImGuiHelper
{
    public class ImGuiTextureEditor
    {
        private Texture _ActiveTexture = null;
        private int _SelectedId = -1; 
        
        public void Draw()
        {
            //Items
            List<Texture> textureList = RenderState.engineRef.renderSys.TextureMgr.Entities;
            string[] items = new string[textureList.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = textureList[i].Name == "" ? "Texture_" + i : textureList[i].Name; 

            if (ImGui.Combo("##1", ref _SelectedId, items, items.Length))
                _ActiveTexture = textureList[_SelectedId];
            
            ImGui.SameLine();

            if (ImGui.Button("Add"))
            {
                Console.WriteLine("Todo Create Texture");
            }
            ImGui.SameLine();
            if (ImGui.Button("Del"))
            {
                Console.WriteLine("Todo Delete Texture");
            }
            if (_ActiveTexture is null)
            {
                ImGui.Text("NULL");
                return;
            }
                
            //TODO show texture info

            ImGui.Columns(2);
            ImGui.Text("Name");
            ImGui.Text("Class");
            ImGui.Text("Flags");
            ImGui.NextColumn();
            ImGui.InputText("", ref _ActiveTexture.Name, 30);
            
        }

        public void SetTexture(Texture tex)
        {
            _ActiveTexture = tex;
            List<Texture> textureList = RenderState.engineRef.renderSys.TextureMgr.Entities;
            _SelectedId = textureList.IndexOf(tex);
        }
    }
}