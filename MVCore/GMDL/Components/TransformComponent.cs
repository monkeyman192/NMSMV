using System;
using OpenTK.Mathematics;
using libMBIN.NMS.Toolkit;

namespace MVCore.GMDL.Components
{
    //TODO: Make sure that every entity (previous model) uses this component by default

    public class TransformData : TkTransformData
    {
        //Raw values 
        public Vector4 Translation
        {
            get
            {
                return new Vector4(TransX, TransY, TransZ, 1.0f);
            }
        }

        public Vector4 WorldPosition
        {
            get
            {
                return new Vector4(1.0f) * WorldTransformMat;
            }
        }

        public Vector4 Scale
        {
            get
            {
                return new Vector4(ScaleX, ScaleY, ScaleZ, 1.0f);
            }
        }

        //Keep Original Values
        private float OldTransX;
        private float OldTransY;
        private float OldTransZ;
        private float OldRotX;
        private float OldRotY;
        private float OldRotZ;
        private float OldScaleX;
        private float OldScaleY;
        private float OldScaleZ;


        public Quaternion rotation = new(); //TODO: Make property
        public Vector3 scale = new();

        public Matrix4 LocalTransformMat = Matrix4.Identity;
        public Matrix4 WorldTransformMat = Matrix4.Identity;
        public Matrix4 inverseTransform = Matrix4.Identity;

        public TransformData() : base() {


        }

    }

    public class TransformComponent : Component {

        public TransformData Data;
        
        public TransformComponent(): base()
        {
            
        }

        public override Component Clone()
        {
            TransformComponent n = new TransformComponent();
            
            //TODO actually copy the component data to the new object
            return n;
        }
        
    }
}
