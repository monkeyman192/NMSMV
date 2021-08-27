using ImGuiNET;
using MVCore;


namespace ImGuiHelper
{
    class ImGuiObjectViewer
    {
        private Entity _model;

        public ImGuiObjectViewer(){


        }

        public void SetModel(Entity m)
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
                switch (_model.Type)
                {
                    case TYPES.MODEL:
                    case TYPES.LOCATOR:
                        DrawLocator();
                        break;
                    case TYPES.MESH:
                        DrawMesh();
                        break;
                    case TYPES.LIGHT:
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
            ImGui.Text(_model.Name);
            ImGui.Text(_model.ID.ToString());
            ImGui.Text(_model.Type.ToString());
            //ImGui.Text(_model.LODNumber.ToString());

            ImGui.Columns(1);
            //TODO LOD Distances


        }

        private void DrawLocator()
        {
            DrawModel();
            //TODO Add Locator Stuff
        }

        private void DrawMesh()
        {
            DrawModel();
            //TODO add Mesh Stuff
        }

        private void DrawLight()
        {
            DrawModel();
            //Todo add Light Stuff
        }

        
        ~ImGuiObjectViewer()
        {

        }



    }
}
