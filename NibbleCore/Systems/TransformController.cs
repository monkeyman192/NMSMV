using System;
using System.Collections.Generic;
using NbCore.Math;
using NbCore.Systems;

namespace NbCore
{
    public unsafe class TransformController
    {
        //Previous
        public NbVector3 PrevPosition;
        public NbQuaternion PrevRotation;
        public NbVector3 PrevScale;
        
        //Next
        public NbVector3 NextPosition;
        public NbQuaternion NextRotation;
        public NbVector3 NextScale;

        //LastQueued
        public NbVector3 LastPosition;
        public NbQuaternion LastRotation;
        public NbVector3 LastScale;

        //Current
        public NbVector3 Position;
        public NbQuaternion Rotation;
        public NbVector3 Scale;
        
        private Queue<NbVector3> FutureTranslation = new();
        private Queue<NbQuaternion> FutureRotation = new();
        private Queue<NbVector3> FutureScale = new();

        private double Time = 0.0;
        private double InterpolationCoeff = 1.0f;
        private TransformData actorData = null;
        
        public TransformController(TransformData data)
        {
            SetActor(data);
        }
        
        public void SetActor(TransformData data)
        {
            actorData = data;
            //Init States
            
            PrevPosition = actorData.localTranslation;
            PrevRotation = actorData.localRotation;
            PrevScale = actorData.localScale;

            NextPosition = actorData.localTranslation;
            NextRotation = actorData.localRotation;
            NextScale = actorData.localScale;

            LastPosition = actorData.localTranslation;
            LastRotation = actorData.localRotation;
            LastScale = actorData.localScale;
        }

        public void ClearActor()
        {
            actorData = null;
        }

        public void AddFutureState(NbVector3 dp, NbQuaternion dr, NbVector3 ds)
        {
            LastPosition = dp;
            LastRotation = dr;
            LastScale = ds;

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
                PrevPosition = NextPosition;
                PrevRotation = NextRotation;
                PrevScale = NextScale;
                
                if (FutureTranslation.Count > 5)
                {
                    FutureTranslation.Dequeue();
                    FutureRotation.Dequeue();
                    FutureScale.Dequeue();
                } 
                
                if (FutureTranslation.Count > 0)
                {
                    NextPosition = FutureTranslation.Dequeue();
                    NextRotation = FutureRotation.Dequeue();
                    NextScale = FutureScale.Dequeue();
                } 

                Time %= TransformationSystem.updateInterval;
            }

            InterpolationCoeff = 1.0 - (TransformationSystem.updateInterval - Time) / 
                                  TransformationSystem.updateInterval;
            
            CalculateState(); //Recalculate state
            ApplyStateToActor(); //Update Actor Data
        }

        public void CalculateState()
        {
            //Interpolate between the two states
            Position = NbVector3.Lerp(PrevPosition, NextPosition, (float) InterpolationCoeff);
            Rotation = NbQuaternion.Slerp(PrevRotation, NextRotation, (float) InterpolationCoeff);
            Scale = NbVector3.Lerp(PrevScale, NextScale, (float) InterpolationCoeff);

            //Callbacks.Log(string.Format("Interpolated Position {0} {1} {2}",
            //                    Position.X, Position.Y, Position.Z, Time),
            //                    LogVerbosityLevel.INFO);

        }

        private void ApplyStateToActor()
        {
            if (actorData != null)
            {
                actorData.localTranslation = Position;
                actorData.localRotation = Rotation;
                actorData.localScale = Scale;
                actorData.RecalculateTransformMatrices();
            }
        }
    }
}
