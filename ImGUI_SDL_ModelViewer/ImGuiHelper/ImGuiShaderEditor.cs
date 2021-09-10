using System;
using GLSLHelper;
using MVCore;
using ImGuiNET;

namespace ImGuiHelper
{
    public class ImGuiShaderEditor
    {
        private GLSLShaderConfig shader = null;
        private string test_text = "";
        
        public void Draw()
        {
            if (shader is null)
                return;

            if (ImGui.CollapsingHeader("Vertex Shader"))
            {
                ImGui.InputTextMultiline("##1", ref shader.vs_text.resolved_text, 50000,
                    new System.Numerics.Vector2(400, 400));
            }
            
            if (ImGui.CollapsingHeader("Fragment Shader"))
            {
                ImGui.InputTextMultiline("##2", ref shader.fs_text.resolved_text, 50000,
                    new System.Numerics.Vector2(400, 400));
            }
            
            if (ImGui.Button("Recompile Shader"))
            {
                Console.WriteLine("Shader recompilation not supported yet");
            }
        }

        public void SetShader(GLSLShaderConfig conf)
        {
            shader = conf;
        }
    }
    
    
}