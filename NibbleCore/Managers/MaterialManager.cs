using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore.Managers
{
    sealed public class MaterialManager : EntityManager<MeshMaterial>
    {
        public readonly Dictionary<string, MeshMaterial> MaterialNameMap = new();
        private readonly Dictionary<long, List<GLInstancedMesh>> MaterialMeshMap = new();

        public bool AddMaterial(MeshMaterial mat) {
            
            if (Add(mat))
            {
                GUIDComponent gc = mat.GetComponent<GUIDComponent>() as GUIDComponent;
                MaterialNameMap[mat.Name] = mat;
                MaterialMeshMap[gc.ID] = new();
                return true;
            }
            return false;
        }

        public MeshMaterial GetByName(string name)
        {
            if (MaterialNameMap.ContainsKey(name))
                return MaterialNameMap[name];
            return null;
        }

        public List<GLInstancedMesh> GetMaterialMeshes(MeshMaterial mat)
        {
            return MaterialMeshMap[mat.GetID()];
        }

        public bool AddMeshToMaterial(MeshMaterial mat, GLInstancedMesh mesh)
        {
            if (MaterialMeshMap[mat.GetID()].Contains(mesh))
                return false;
            MaterialMeshMap[mat.GetID()].Add(mesh);
            return true;
        }

        public bool RemoveMeshFromMaterial(MeshMaterial mat, GLInstancedMesh mesh)
        {
            if (!MaterialMeshMap[mat.GetID()].Contains(mesh))
                return false;
            MaterialMeshMap[mat.GetID()].Remove(mesh);
            return true;
        }

        public bool MaterialContainsMesh(MeshMaterial mat, GLInstancedMesh mesh)
        {
            return MaterialMeshMap[mat.GetID()].Contains(mesh);
        }


    }
}
