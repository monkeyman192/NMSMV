using NbCore.Plugins;
using Newtonsoft.Json;
using ImGuiNET;
using ImGuiHelper;
using System.Runtime.InteropServices;

namespace NibbleAssimpPlugin
{
    public class AssimpPluginSettings : PluginSettings
    {
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

        public override void SaveToFile()
        {
            throw new System.NotImplementedException();
        }
    }
}
