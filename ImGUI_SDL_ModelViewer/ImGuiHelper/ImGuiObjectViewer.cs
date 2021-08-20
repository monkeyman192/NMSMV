using ImGuiNET;
using MVCore.GMDL;


namespace ImGuiHelper
{
    class ImGuiObjectViewer
    {
        private Model _model;

        public ImGuiObjectViewer(){


        }

        public void SetModel(Model m)
        {
            if (m == null)
                return;
            _model = m;
        }

        public void Draw()
        {

            //Assume that a Popup has begun
            //ImGui.Begin("Info", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);


            if (_model != null)
            {
                switch (_model.type)
                {
                    case MVCore.TYPES.MODEL:
                    case MVCore.TYPES.LOCATOR:
                        DrawLocator();
                        break;
                    case MVCore.TYPES.MESH:
                        DrawMesh();
                        break;
                    case MVCore.TYPES.LIGHT:
                        DrawLight();
                        break;
                    default:
                        ImGui.Text("Not Supported yet");
                        break;
                }
            }
                
            //ImGui.End();
        
        }

        private void DrawModel()
        {
            //Name
            ImGui.Columns(2);
            ImGui.Text("Name");
            ImGui.Text("ID");
            ImGui.Text("Type");
            ImGui.Text("LOD");

            ImGui.NextColumn();
            ImGui.Text(_model.name);
            ImGui.Text(_model.ID.ToString());
            ImGui.Text(_model.Type);
            ImGui.Text(_model.LODNumber.ToString());
            //TODO LOD Distances



        }

        private void DrawLocator()
        {
            DrawModel();
        }

        private void DrawMesh()
        {
            DrawModel();
        }

        private void DrawLight()
        {
            DrawModel();
        }

        
        ~ImGuiObjectViewer()
        {

        }



    }
}
