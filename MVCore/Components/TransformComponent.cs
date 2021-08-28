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

        public Quaternion localRotation
        {
            get
            {
                return Quaternion.FromEulerAngles(Utils.MathUtils.radians(RotX),
                                                  Utils.MathUtils.radians(RotY),
                                                  Utils.MathUtils.radians(RotZ));
            }

            set
            {
                Vector3 res;
                Quaternion.ToEulerAngles(value, out res);
                RotX = Utils.MathUtils.degrees(res.X);
                RotY = Utils.MathUtils.degrees(res.Y);
                RotZ = Utils.MathUtils.degrees(res.Z);
            }
        }

        public Vector4 WorldPosition
        {
            get
            {
                return new Vector4(1.0f) * WorldTransformMat;
            }

        }

        public Vector3 localScale
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

        public Matrix4 LocalTransformMat = Matrix4.Identity;
        public Matrix4 WorldTransformMat = Matrix4.Identity;
        
        public Matrix4 inverseTransform = Matrix4.Identity;

        private TransformData parent = null;

        public TransformData() : base() {

        }

        public TransformData(TkTransformData data) : base()
        {
            TransX = data.TransX;
            TransY = data.TransY;
            TransZ = data.TransZ;
            RotX = data.RotX;
            RotY = data.RotY;
            RotZ = data.RotZ;
            ScaleX = data.ScaleX;
            ScaleY = data.ScaleY;
            ScaleZ = data.ScaleZ;
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

        public void StoreAsOldTransform()
        {
            OldTransX = TransX;
            OldTransY = TransY;
            OldTransZ = TransZ;
            OldRotX = RotX;
            OldRotY = RotY;
            OldRotZ = RotZ;
            OldScaleX = ScaleX;
            OldScaleY = ScaleY;
            OldScaleZ = ScaleZ;
        }

        public void ResetTransform()
        {
            TransX = OldTransX;
            TransY = OldTransY;
            TransZ = OldTransZ;
            RotX = OldRotX;
            RotY = OldRotY;
            RotZ = OldRotZ;
            ScaleX = OldScaleX;
            ScaleY = OldScaleY;
            ScaleZ = OldScaleZ;
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

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }
    }
}
