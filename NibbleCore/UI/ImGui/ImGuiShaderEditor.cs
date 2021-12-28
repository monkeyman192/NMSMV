using System;
using System.Collections.Generic;
using System.Linq;
using NbCore.Platform.Graphics.OpenGL; //TODO: Abstract
using NbCore;
using NbCore.Common;
using ImGuiCore = ImGuiNET.ImGui;

namespace NbCore.UI.ImGui
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
                
            if (ImGuiCore.Combo("##1", ref selectedId, items, items.Length))
                ActiveShaderSource = (GLSLShaderSource) shaderSourceList[selectedId];

            ImGuiCore.SameLine();

            if (ImGuiCore.Button("Add"))
            {
                Console.WriteLine("Todo Create Shader");
            }
            ImGuiCore.SameLine();
            if (ImGuiCore.Button("Del"))
            {
                Console.WriteLine("Todo Delete Shader");
            }

            if (ActiveShaderSource != null)
            {
                ImGuiCore.InputTextMultiline("##2", ref ActiveShaderSource.SourceText, 50000,
                    new System.Numerics.Vector2(400, 400));

                if (ImGuiCore.Button("Recompile Shader"))
                {
                    Console.WriteLine("Shader recompilation not supported yet");
                }    
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