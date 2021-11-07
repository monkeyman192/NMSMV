using System.Collections.Generic;
using ImGuiNET;

namespace NbCore
{
    public interface IRenderApi
    {
        public void AddMesh(NbMesh mesh);
        public void RenderMesh();

        public void RenderMesh(NbMesh mesh, MeshMaterial mat);
        public void RenderLocator(NbMesh mesh, MeshMaterial mat);
        public void RenderJoint(NbMesh mesh, MeshMaterial mat);
        public void RenderCollision(NbMesh mesh, MeshMaterial mat);
        public void RenderLight(NbMesh mesh, MeshMaterial mat);
        public void RenderLightVolume(NbMesh mesh, MeshMaterial mat);
    }


}