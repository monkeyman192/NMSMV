using System;
using System.Collections.Generic;
using NbCore;
using NbCore.Math;


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

    public struct LightData
    {
        public Math.NbVector3 Color;
        public float FOV;
        public float Intensity;
        public bool IsRenderable;
        public ATTENUATION_TYPE Falloff;
        public float Falloff_radius;
        public LIGHT_TYPE LightType;
        public bool IsUpdated;
    }

    public class LightComponent : MeshComponent
    {
        //Exposed Light Properties
        public LightData Data;
        
        public Math.NbVector3 Direction;
        //Light Projection + View Matrices
        public NbMatrix4[] lightSpaceMatrices;
        public NbMatrix4 lightProjectionMatrix;

        public LightComponent() : base()
        {
            Data = new()
            {
                Color = new NbVector3(1.0f),
                FOV = 360.0f,
                Intensity = 1.0f,
                IsRenderable = true,
                Falloff = ATTENUATION_TYPE.QUADRATIC,
                LightType = LIGHT_TYPE.POINT,
                IsUpdated = true
            };
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
