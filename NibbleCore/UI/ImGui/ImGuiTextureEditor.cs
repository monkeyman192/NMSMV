using System;
using NbCore;
using NbCore.Common;
using ImGuiCore = ImGuiNET.ImGui;
using System.Collections.Generic;


namespace NbCore.UI.ImGui
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

            if (ImGuiCore.Combo("##1", ref _SelectedId, items, items.Length))
                _ActiveTexture = textureList[_SelectedId];

            ImGuiCore.SameLine();

            if (ImGuiCore.Button("Add"))
            {
                Console.WriteLine("Todo Create Texture");
            }
            ImGuiCore.SameLine();
            if (ImGuiCore.Button("Del"))
            {
                Console.WriteLine("Todo Delete Texture");
            }
            if (_ActiveTexture is null)
            {
                ImGuiCore.Text("NULL");
                return;
            }

            //TODO show texture info

            ImGuiCore.Columns(2);
            ImGuiCore.Text("Name");
            ImGuiCore.Text("Class");
            ImGuiCore.Text("Flags");
            ImGuiCore.NextColumn();
            ImGuiCore.InputText("", ref _ActiveTexture.Name, 30);
            
        }

        public void SetTexture(Texture tex)
        {
            _ActiveTexture = tex;
            List<Texture> textureList = RenderState.engineRef.renderSys.TextureMgr.Entities;
            _SelectedId = textureList.IndexOf(tex);
        }
    }
}