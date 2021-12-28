using NbCore;

namespace NbCore.UI.ImGui
{
    public abstract class ImGuiPanel
    {
        public Engine EngineRef = null;

        public ImGuiPanel(Engine engine)
        {
            EngineRef = engine;
        }
    }
}