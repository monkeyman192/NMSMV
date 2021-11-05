using System.Collections.Generic;
using ImGuiNET;

namespace NbCore
{
    public abstract class RenderApi
    {   
        public abstract void AddMesh(NbMesh mesh);
        public abstract void RenderMesh();
    }

    public class OpenGLApi : RenderApi
    {
        public Dictionary<NbMesh, GLInstancedMesh> MeshMap = new();
        
        public override void AddMesh(NbMesh mesh)
        {
            if (!MeshMap.ContainsKey(mesh))
            { 
                //Generate instanced mesh
                GLInstancedMesh imesh = GenerateAPIMesh(mesh);
                MeshMap[mesh] = imesh;
            }
        }

        private GLInstancedMesh GenerateAPIMesh(NbMesh mesh)
        {
            GLInstancedMesh imesh = new();
            imesh.BaseMesh = mesh;
            

            return imesh;
        }
        
        public override void RenderMesh()
        {
            throw new System.NotImplementedException();
        }
    }
}