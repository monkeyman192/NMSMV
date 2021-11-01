using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using NbCore;
using OpenTK.Graphics.OpenGL4;

namespace NbCore
{
    public class CollisionComponent : MeshComponent
    {
        public COLLISIONTYPES CollisionType;

        public CollisionComponent()
        {
            
        }

        public override Component Clone()
        {
            CollisionComponent mc = new();
            mc.CopyFrom(this);
            return mc;
        }

        public override void CopyFrom(Component c)
        {
            if (c is not CollisionComponent)
                return;

            CollisionComponent mc = c as CollisionComponent;
            base.CopyFrom(mc);
        }
    }
}
