using System;
using System.Collections.Generic;
using System.Text;
using MVCore;
using MVCore.Systems;

namespace MVCore
{
    public class SceneComponent : Component
    {
        public int NumLods;
        public GeomObject Gobject;
        public readonly List<float> LODDistances = new();

        //Keep reference of all the animation Joints of the scene and the skinmatrices
        public readonly float[] skinMats; //Final Matrices
        public readonly Dictionary<string, SceneGraphNode> jointDict;
        public int activeLOD = 0;

        public SceneComponent()
        {
            //Init Animation Stuff
            skinMats = new float[256 * 16];
            jointDict = new();
        }

        public override Component Clone()
        {
            throw new NotImplementedException();
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();
                jointDict.Clear();
                
                //Free other resources here
                base.Dispose(disposing);
            }

            //Free unmanaged resources
            disposed = true;
        }
    }
}
