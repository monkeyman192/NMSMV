using MVCore;

namespace ImGuiHelper
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