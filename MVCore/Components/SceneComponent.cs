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

        //TODO: Remove that shit from here it doesn't feel rights
        public readonly TextureManager TexManager; 
        //Keep reference of all the animation Joints of the scene and the skinmatrices
        public readonly float[] skinMats; //Final Matrices
        public readonly Dictionary<string, SceneGraphNode> jointDict;
        public int activeLOD = 0;

        public SceneComponent()
        {
            TexManager = new TextureManager();
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

        public void setupJointDict(SceneGraphNode m)
        {
            if (m.Type == TYPES.JOINT)
                jointDict[m.Name] = m;

            foreach (SceneGraphNode c in m.Children)
                setupJointDict(c);
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
