using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;
using MVCore;

namespace MVCore
{
    public class TransformData
    {
        public float TransX;
        public float TransY;
        public float TransZ;
        public float RotX;
        public float RotY;
        public float RotZ;
        public float ScaleX;
        public float ScaleY;
        public float ScaleZ;

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

        public Matrix4 LocalTransformMat;
        public Matrix4 WorldTransformMat;

        public Matrix4 InverseTransformMat;

        private TransformData parent;
        public bool WasOccluded; //Set this to true so as to trigger the first instance setup
        public bool IsOccluded;
        public bool IsUpdated;

        public TransformData(float tx = 0.0f, float ty = 0.0f, float tz = 0.0f,
                             float rx = 0.0f, float ry = 0.0f, float rz = 0.0f,
                             float sx = 1.0f, float sy = 1.0f, float sz = 1.0f)
        {
            TransX = tx;
            TransY = ty;
            TransZ = tz;
            RotX = rx;
            RotY = ry;
            RotZ = rz;
            ScaleX = sx;
            ScaleY = sy;
            ScaleZ = sz;
            
            OldTransX = TransX;
            OldTransY = TransY;
            OldTransZ = TransZ;
            OldRotX = RotX;
            OldRotY = RotY;
            OldRotZ = RotZ;
            OldScaleX = ScaleX;
            OldScaleY = ScaleY;
            OldScaleZ = ScaleZ;

            //Rest Properties
            LocalTransformMat = Matrix4.Identity;
            WorldTransformMat = Matrix4.Identity;
            InverseTransformMat = Matrix4.Identity;
            WasOccluded = true;
            IsOccluded = true;
            IsUpdated = false;
        }

        public void SetParentData(TransformData data)
        {
            parent = data;
        }

        public void ClearParentData()
        {
            parent = null;
        }

        public void RecalculateTransformMatrices()
        {
            LocalTransformMat = Matrix4.CreateScale(localScale) *
                                Matrix4.CreateFromQuaternion(localRotation) *
                                Matrix4.CreateTranslation(localTranslation);

            if (parent != null)
                WorldTransformMat = LocalTransformMat * parent.WorldTransformMat;
            else
                WorldTransformMat = LocalTransformMat;
            IsUpdated = true;
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
}
