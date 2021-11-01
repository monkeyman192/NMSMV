using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore.Managers
{
    public class GeometryManager :EntityManager<Entity>
    {
        public Dictionary<string, GeomObject> GLgeoms = new();
        public Dictionary<string, GLInstancedMesh> GLPrimitiveMeshes = new();

        public GeometryManager()
        {

        }

        #region GeomObjects
        public bool AddGeom(GeomObject o)
        {
            if (base.Add(o))
            {
                GLgeoms[o.Name] = o;
                return true;
            }
            return false;
        }

        public bool HasGeom(string name)
        {
            return GLgeoms.ContainsKey(name);
        }

        public GeomObject GetGeom(string name)
        {
            return GLgeoms[name];
        }

        #endregion

        #region Primitives
        public bool AddPrimitiveMesh(GLInstancedMesh mesh)
        {
            if (base.Add(mesh))
            {
                GLPrimitiveMeshes[mesh.Name] = mesh;
                return true;
            }
            return false;
        }

        public bool HasPrimitiveMesh(string name)
        {
            return GLPrimitiveMeshes.ContainsKey(name);
        }

        public GLInstancedMesh GetPrimitiveMesh(string name)
        {
            return GLPrimitiveMeshes[name];
        }

        #endregion



        public new void CleanUp()
        {
            GLgeoms.Clear();
            GLPrimitiveMeshes.Clear();

            //I hope that the correct Dispose methods will be called and not just the default
            base.CleanUp();
        }
    }
}
