using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore.Managers
{
    sealed public class MaterialManager : EntityManager<MeshMaterial>
    {
        public readonly Dictionary<string, MeshMaterial> MaterialNameMap = new();
        private readonly Dictionary<long, List<GLInstancedMesh>> MaterialMeshMap = new();

        public bool AddMaterial(MeshMaterial mat) {
            
            if (Add(mat))
            {
                MaterialNameMap[mat.Name] = mat;
                MaterialMeshMap[mat.ID] = new();
                return true;
            }
            return false;
        }

        public MeshMaterial GetByName(string name)
        {
            return MaterialNameMap[name];
        }

        public List<GLInstancedMesh> GetMaterialMeshes(MeshMaterial mat)
        {
            return MaterialMeshMap[mat.ID];
        }

        public bool AddMeshToMaterial(MeshMaterial mat, GLInstancedMesh mesh)
        {
            if (MaterialMeshMap[mat.ID].Contains(mesh))
                return false;
            MaterialMeshMap[mat.ID].Add(mesh);
            return true;
        }

        public bool RemoveMeshFromMaterial(MeshMaterial mat, GLInstancedMesh mesh)
        {
            if (!MaterialMeshMap[mat.ID].Contains(mesh))
                return false;
            MaterialMeshMap[mat.ID].Add(mesh);
            return true;
        }

        public bool MaterialContainsMesh(MeshMaterial mat, GLInstancedMesh mesh)
        {
            return MaterialMeshMap[mat.ID].Contains(mesh);
        }


    }
}
