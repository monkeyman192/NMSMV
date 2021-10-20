using MVCore.Plugins;
using Newtonsoft.Json;
using ImGuiNET;
using ImGuiHelper;
using System.Runtime.InteropServices;

namespace NibbleAssimpPlugin
{
    public class AssimpPluginSettings : PluginSettings
    {
        public AssimpPluginSettings()
        {
            PluginName = "Assimp Nibble v0.1";
        }

        public override void Draw()
        {
            ImGui.Text("NULL");
        }

        public override void DrawModals()
        {
            
        }

        public new static AssimpPluginSettings GenerateDefaultSettings()
        {
            AssimpPluginSettings settings = new();

            return settings;
        }
    }
}
