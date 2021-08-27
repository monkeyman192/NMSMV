using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using MVCore;
using OpenTK.Graphics.OpenGL4;

namespace MVCore.Systems
{
    public class MeshComponent : Component
    {
        //Store TkSceneNodeAttributes for Meshes

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
        public DrawElementsType indicesLength = DrawElementsType.UnsignedShort;

        MeshComponent()
        {
            
        }

        public override Component Clone()
        {
            MeshComponent mc = new MeshComponent();
            mc.CopyFrom(this);
            return mc;
        }

        public override void CopyFrom(Component c)
        {
            if (c is not MeshComponent)
                return;

            MeshComponent mc = c as MeshComponent;

            AABBMAX = mc.AABBMAX;
            AABBMIN = mc.AABBMIN;
            Hash = mc.Hash;
            VertrStartGraphics = mc.VertrStartGraphics;
            VertrEndGraphics = mc.VertrEndGraphics;
            VertrStartPhysics = mc.VertrStartPhysics;
            VertrEndPhysics = mc.VertrEndPhysics;
            BatchStartPhysics = mc.BatchStartPhysics;
            BatchStartGraphics = mc.BatchStartGraphics;
            BatchCount = mc.BatchCount;
            FirstSkinMat = mc.FirstSkinMat;
            LastSkinMat = mc.LastSkinMat;
            LODLevel = mc.LODLevel;
            BoundHullStart = mc.BoundHullStart;
            BoundHullEnd = mc.BoundHullEnd;
            LODDistance1 = mc.LODDistance1;
            LODDistance2 = mc.LODDistance2;
            indicesLength = mc.indicesLength;

        }
    }
}
