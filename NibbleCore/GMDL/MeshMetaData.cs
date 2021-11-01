using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;


namespace NbCore
{
    public class MeshMetaData
    {

        //Mesh Properties
        public ulong Hash;
        public Vector3 AABBMIN;
        public Vector3 AABBMAX;
        public int VertrStartPhysics;
        public int VertrEndPhysics;
        public int VertrStartGraphics;
        public int VertrEndGraphics;
        public int BatchStartPhysics;
        public int BatchStartGraphics;
        public int BatchCount;
        public int FirstSkinMat;
        public int LastSkinMat;
        public int LODLevel;
        public int BoundHullStart;
        public int BoundHullEnd;

        //LOD Properties
        public int LODDistance1;
        public int LODDistance2;

        //New stuff Properties
        public DrawElementsType IndicesLength = DrawElementsType.UnsignedShort;

        public MeshMetaData()
        {
            //Init values to null
            VertrEndGraphics = 0;
            VertrStartGraphics = 0;
            VertrEndPhysics = 0;
            VertrStartPhysics = 0;
            BatchStartGraphics = 0;
            BatchStartPhysics = 0;
            BatchCount = 0;
            FirstSkinMat = 0;
            LastSkinMat = 0;
            BoundHullStart = 0;
            BoundHullEnd = 0;
            Hash = 0xFFFFFFFF;
            AABBMIN = new Vector3();
            AABBMAX = new Vector3();
        }

        public MeshMetaData(MeshMetaData input)
        {
            //Init values to null
            VertrEndGraphics = input.VertrEndGraphics;
            VertrStartGraphics = input.VertrStartGraphics;
            VertrEndPhysics = input.VertrEndPhysics;
            VertrStartPhysics = input.VertrStartPhysics;
            BatchStartGraphics = input.BatchStartGraphics;
            BatchStartPhysics = input.BatchStartPhysics;
            BatchCount = input.BatchCount;
            FirstSkinMat = input.FirstSkinMat;
            LastSkinMat = input.LastSkinMat;
            BoundHullStart = input.BoundHullStart;
            BoundHullEnd = input.BoundHullEnd;
            Hash = input.Hash;
            LODLevel = input.LODLevel;
            IndicesLength = input.IndicesLength;
            AABBMIN = new Vector3(input.AABBMIN);
            AABBMAX = new Vector3(input.AABBMAX);
        }
    }
}
