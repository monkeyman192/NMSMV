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
        private GLSLShaderSource ActiveShaderSource = null;
        private int selectedId = -1;

        public ImGuiShaderEditor()
        {
            
        }
        
        public void Draw()
        {
            //TODO: Make this static if possible or maybe maintain a list of shaders in the resource manager
            
            //Items
            List<Entity> shaderSourceList = new();
            shaderSourceList = RenderState.engineRef.GetEntityTypeList(EntityType.ShaderSource);
            string[] items = new string[shaderSourceList.Count];
            for (int i = 0; i < items.Length; i++)
            {
                GLSLShaderSource ss = (GLSLShaderSource) shaderSourceList[i];
                items[i] = ss.Name == "" ? "Shader_" + i : ss.Name;
            }
                
            if (ImGui.Combo("##1", ref selectedId, items, items.Length))
                ActiveShaderSource = (GLSLShaderSource) shaderSourceList[selectedId];
            
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

            ImGui.InputTextMultiline("##2", ref ActiveShaderSource.SourceText, 50000,
                new System.Numerics.Vector2(400, 400));

            if (ImGui.Button("Recompile Shader"))
            {
                Console.WriteLine("Shader recompilation not supported yet");
            }
        }

        public void SetShader(GLSLShaderSource conf)
        {
            ActiveShaderSource = conf;
            List<Entity> shaderList = RenderState.engineRef.GetEntityTypeList(EntityType.ShaderSource);
            selectedId = shaderList.IndexOf(conf);
        }
    }
    
    
}