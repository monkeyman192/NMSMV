using System;
using System.Collections.Generic;
using System.Linq;
using GLSLHelper;
using MVCore;
using MVCore.Common;
using ImGuiNET;

namespace ImGuiHelper
{
    public class ImGuiShaderEditor
    {
        private GLSLShaderConfig ActiveShader = null;
        private int selectedId = -1;

        public ImGuiShaderEditor()
        {
            
        }
        
        public void Draw()
        {
            //TODO: Make this static if possible or maybe maintain a list of shaders in the resource manager
            
            //Items
            List<GLSLShaderConfig> shaderList = new();
            shaderList = RenderState.activeResMgr.ShaderMap.Values.ToList();
            string[] items = new string[shaderList.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = shaderList[i].Name == "" ? "Shader_" + i : shaderList[i].Name; 

            if (ImGui.Combo("##1", ref selectedId, items, items.Length))
                ActiveShader = shaderList[selectedId];
            
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

            if (ImGui.CollapsingHeader("Vertex Shader"))
            {
                ImGui.InputTextMultiline("##2", ref ActiveShader.VSText.ResolvedText, 50000,
                    new System.Numerics.Vector2(400, 400));
            }
            
            if (ImGui.CollapsingHeader("Fragment Shader"))
            {
                ImGui.InputTextMultiline("##3", ref ActiveShader.FSText.ResolvedText, 50000,
                    new System.Numerics.Vector2(400, 400));
            }
            
            if (ImGui.Button("Recompile Shader"))
            {
                Console.WriteLine("Shader recompilation not supported yet");
            }
        }

        public void SetShader(GLSLShaderConfig conf)
        {
            ActiveShader = conf;
            List<GLSLShaderConfig> shaderList = RenderState.activeResMgr.ShaderMap.Values.ToList();
            selectedId = shaderList.IndexOf(conf);
        }
    }
    
    
}