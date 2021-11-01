using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace NbCore
{
    public class JointData
    {
        public int jointIndex;

        //Add a bunch of shit for posing
        //public Vector3 _localPosePosition = new Vector3(0.0f);
        //public Matrix4 _localPoseRotation = Matrix4.Identity;
        //public Vector3 _localPoseScale = new Vector3(1.0f);
        public Matrix4 BindMat = Matrix4.Identity; //This is the local Bind Matrix related to the parent joint
        public Matrix4 invBMat = Matrix4.Identity; //This is the inverse of the local Bind Matrix related to the parent
                                                   //DO NOT MIX WITH THE gobject.invBMat which reverts the transformation to the global space
        //Blending Queues
        public List<Vector3> PositionQueue = new();
        public List<Vector3> ScaleQueue = new();
        public List<Quaternion> RotationQueue = new();

        public void CopyFrom(JointData jd)
        {
            
            jointIndex = jd.jointIndex;
            BindMat = jd.BindMat;
            invBMat = jd.invBMat;

            PositionQueue.Clear();
            ScaleQueue.Clear();
            RotationQueue.Clear();
            
            foreach (Vector3 v in jd.PositionQueue)
                PositionQueue.Add(v);

            foreach (Vector3 v in jd.ScaleQueue)
                ScaleQueue.Add(v);

            foreach (Quaternion v in jd.RotationQueue)
                RotationQueue.Add(v);
            
        }
    }

    public class JointComponent : Component
    {
        public JointData Data;
        
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
