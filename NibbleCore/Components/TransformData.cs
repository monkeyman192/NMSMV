﻿using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Math;
using NbCore;

namespace NbCore
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
        public NbVector3 localTranslation
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

        public NbQuaternion localRotation
        {
            get
            {
                return NbQuaternion.FromEulerAngles(MathUtils.radians(RotX),
                                                  MathUtils.radians(RotY),
                                                  MathUtils.radians(RotZ));
            }

            set
            {
                NbVector3 res;
                NbQuaternion.ToEulerAngles(value, out res);
                RotX = MathUtils.degrees(res.X);
                RotY = MathUtils.degrees(res.Y);
                RotZ = MathUtils.degrees(res.Z);
            }
        }

        public NbVector4 WorldPosition
        {
            get
            {
                return new NbVector4(1.0f) * WorldTransformMat;
            }

        }

        public NbVector3 localScale
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

        public NbMatrix4 LocalTransformMat;
        public NbMatrix4 WorldTransformMat;

        public NbMatrix4 InverseTransformMat;

        private TransformData parent;
        public bool WasOccluded; //Set this to true so as to trigger the first instance setup
        public bool IsOccluded;
        public bool IsUpdated;
        public bool IsActive;

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
            LocalTransformMat = NbMatrix4.Identity();
            WorldTransformMat = NbMatrix4.Identity();
            InverseTransformMat = NbMatrix4.Identity();
            WasOccluded = true;
            IsOccluded = true;
            IsUpdated = false;
            IsActive = true; //by default
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
            LocalTransformMat = NbMatrix4.CreateScale(localScale) *
                                NbMatrix4.CreateFromQuaternion(localRotation) *
                                NbMatrix4.CreateTranslation(localTranslation);

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
