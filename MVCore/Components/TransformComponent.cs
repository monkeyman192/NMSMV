using System;
using OpenTK.Mathematics;
using libMBIN.NMS.Toolkit;

namespace MVCore
{
    //TODO: Make sure that every entity (previous model) uses this component by default

    public class TransformData : TkTransformData
    {
        //Raw values 
        public Vector3 localTranslation
        {
            get
            {
                return new(TransX, TransY, TransZ);
            }

            set
            {
                TransX = value.X;
                TransY = value.Y;
                TransZ = value.Z;
            }
        }

        public Vector4 WorldPosition
        {
            get
            {
                return new Vector4(1.0f) * WorldTransformMat;
            }

        }

        public Vector3 Scale
        {
            get => new(ScaleX, ScaleY, ScaleZ);

            set
            {
                ScaleX = value.X;
                ScaleY = value.Y;
                ScaleZ = value.Z;
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

        public Quaternion localRotation = Quaternion.Identity; //TODO: Make property
        public Vector3 localScale = new(1.0f);
        
        public Matrix4 LocalTransformMat = Matrix4.Identity;
        public Matrix4 WorldTransformMat = Matrix4.Identity;
        
        public Matrix4 inverseTransform = Matrix4.Identity;

        private TransformData parent = null;

        public TransformData() : base() {

        }
        public void SetParentData(TransformData data)
        {
            parent = data;
        }

        public void ClearParentData()
        {
            parent = null;
        }
        
        internal Matrix4 CalculateWorldTransformMatrix()
        {
            if (parent != null)
                return LocalTransformMat * parent.WorldTransformMat;
            else
                return LocalTransformMat;
        }
    }

    public class TransformComponent : Component {

        public TransformData Data;
        
        public TransformComponent(TransformData data): base()
        {
            Data = data;
        }

        public override Component Clone()
        {
            //Use the same Data reference to the clone as well (not sure if this is correct)
            TransformComponent n = new TransformComponent(Data);
            
            return n;
        }
        
    }
}
