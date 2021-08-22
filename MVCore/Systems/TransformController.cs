using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using MVCore.Common;
using MVCore.Systems;

namespace MVCore
{
    public class TransformController
    {
        //Previous
        private Vector3 PrevPosition;
        private Quaternion PrevRotation;
        private Vector3 PrevScale;
        
        //Next
        private Vector3 NextPosition;
        private Quaternion NextRotation;
        private Vector3 NextScale;

        //Current
        private Vector3 Position;
        private Quaternion Rotation;
        private Vector3 Scale;
        private Matrix4 State;

        private Queue<Vector3> FutureTranslation = new();
        private Queue<Quaternion> FutureRotation = new();
        private Queue<Vector3> FutureScale = new();

        private double Time = 0.0;
        private double InterpolationCoeff = 1.0f;
        private TransformComponent actor = null;

        public TransformController(TransformComponent act)
        {
            actor = act;
        }

        public void AddFutureState(Vector3 dp, Quaternion dr, Vector3 ds)
        {
            FutureTranslation.Enqueue(dp);
            FutureRotation.Enqueue(dr);
            FutureScale.Enqueue(ds);
        }
        
        public void Update(double interval)
        {
            Time += interval;

            //For performance reasons the check for an empty queue is removed
            //The movement component assumes that future states are continously
            //added to the moving objects
            
            if (Time > TransformationSystem.updateInterval)
            {
                NextPosition = FutureTranslation.Dequeue();
                NextRotation = FutureRotation.Dequeue();
                NextScale = FutureScale.Dequeue();
                
                Time %= TransformationSystem.updateInterval;
            }

            InterpolationCoeff = (TransformationSystem.updateInterval - Time) / 
                                  TransformationSystem.updateInterval;
            CalculateState(); //Recalculate state
            ApplyStateToActor(); //Update Actor Data
        }

        public void CalculateState()
        {
            //Interpolate between the two states
            Position = Vector3.Lerp(PrevPosition, NextPosition, (float) InterpolationCoeff);
            Rotation = Quaternion.Slerp(PrevRotation, NextRotation, (float) InterpolationCoeff);
            Scale = Vector3.Lerp(PrevScale, NextScale, (float) InterpolationCoeff);

            State = Matrix4.CreateScale(Scale) * 
                    Matrix4.CreateFromQuaternion(Rotation) * 
                    Matrix4.CreateTranslation(Position);

        }

        private void ApplyStateToActor()
        {
            if (actor != null)
            {
                actor.Data.localTranslation = Position;
                actor.Data.localRotation = Rotation;
                actor.Data.localScale = Scale;
                actor.Data.LocalTransformMat = State;
                actor.Data.WorldTransformMat = actor.Data.CalculateWorldTransformMatrix();
            }
        }

        public Matrix4 GetState()
        {
            return State;
        }


    }
}
