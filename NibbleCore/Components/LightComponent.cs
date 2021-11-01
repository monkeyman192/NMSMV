using System;
using System.Collections.Generic;
using NbCore;
using OpenTK.Mathematics;


namespace NbCore
{
    public enum ATTENUATION_TYPE
    {
        QUADRATIC = 0x0,
        CONSTANT,
        LINEAR,
        COUNT
    }

    public enum LIGHT_TYPE
    {
        POINT = 0x0,
        SPOT,
        COUNT
    }

    class LightComponent : Component
    {
        //Light Volume Mesh
        public GLInstancedLightMesh VolumeMeshVao;
        
        //Exposed Light Properties
        public Vector3 Color;
        public Vector3 Direction;
        public float FOV;
        public float Intensity;
        public bool IsRenderable;
        public ATTENUATION_TYPE Falloff;
        public LIGHT_TYPE LightType;

        //Light Projection + View Matrices
        public Matrix4[] lightSpaceMatrices;
        public Matrix4 lightProjectionMatrix;

        //Light Falloff Radius
        public float radius;

        public LightComponent() : base()
        {
            Color = new Vector3(1.0f);
            Direction = new Vector3(0.0f, 0.0f, -1.0f); //target front
            FOV = 360.0f;
            Intensity = 1.0f;
            IsRenderable = true;
            Falloff = ATTENUATION_TYPE.QUADRATIC;
            LightType = LIGHT_TYPE.POINT;
        }


        public override Component Clone()
        {
            throw new NotImplementedException();
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }
    }
}
