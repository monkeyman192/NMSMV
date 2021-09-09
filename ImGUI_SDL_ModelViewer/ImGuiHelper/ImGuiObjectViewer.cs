using ImGuiNET;
using MVCore;
using MVCore.Systems;

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
            ImGui.Text("ID");
            ImGui.Text("Type");
            ImGui.Text("LOD");

            ImGui.NextColumn();
            ImGui.Text(_model.ID.ToString());
            ImGui.Text(_model.Type.ToString());
            ImGui.Text("TODO");

            ImGui.Columns(1);
            ImGui.InputText("Name", ref _model.Name, 30);
            
            
            //ImGui.Text(_model.LODNumber.ToString());

            ImGui.Columns(1);
            //TODO LOD Distances

            //Draw Transform
            ImGui.Separator();
            ImGui.Text("Node Transform");
            TransformData td = TransformationSystem.GetEntityTransformData(_model);

            //Draw TransformMatrix
            bool transform_changed = false;
            ImGui.Columns(3);
            transform_changed |= ImGui.DragFloat("TransX", ref td.TransX, 0.005f);
            transform_changed |= ImGui.DragFloat("RotX", ref td.RotX);
            transform_changed |= ImGui.DragFloat("ScaleX", ref td.ScaleX, 0.005f);
            ImGui.NextColumn();
            transform_changed |= ImGui.DragFloat("TransY", ref td.TransY, 0.005f);
            transform_changed |= ImGui.DragFloat("RotY", ref td.RotY);
            transform_changed |= ImGui.DragFloat("ScaleY", ref td.ScaleY, 0.005f);
            ImGui.NextColumn();
            transform_changed |= ImGui.DragFloat("TransZ", ref td.TransZ, 0.005f);
            transform_changed |= ImGui.DragFloat("RotZ", ref td.RotZ);
            transform_changed |= ImGui.DragFloat("ScaleZ", ref td.ScaleZ, 0.005f);
            ImGui.Columns(1);

            if (transform_changed)
            {
                MVCore.Common.RenderState.engineRef.transformSys.RequestEntityUpdate(_model);
            }

            //Draw Components
            
            if (_model.HasComponent<MeshComponent>())
            {
                MeshComponent mc = _model.GetComponent<MeshComponent>() as MeshComponent;
                ImGui.Separator();
                ImGui.Text("Mesh Component");
                ImGui.InputText("Material Name", ref mc.Material.Name, 30);
                ImGui.Text("Mesh Info");
                ImGui.Text("TODO");

            }

        
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
