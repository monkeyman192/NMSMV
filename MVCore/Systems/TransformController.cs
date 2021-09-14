﻿using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using MVCore.Common;
using MVCore.Systems;

namespace MVCore
{
    public unsafe class TransformController
    {
        //Previous
        public Vector3 PrevPosition;
        public Quaternion PrevRotation;
        public Vector3 PrevScale;
        
        //Next
        public Vector3 NextPosition;
        public Quaternion NextRotation;
        public Vector3 NextScale;

        //LastQueued
        public Vector3 LastPosition;
        public Quaternion LastRotation;
        public Vector3 LastScale;

        //Current
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        
        private Queue<Vector3> FutureTranslation = new();
        private Queue<Quaternion> FutureRotation = new();
        private Queue<Vector3> FutureScale = new();

        private double Time = 0.0;
        private double InterpolationCoeff = 1.0f;
        private TransformData actorData = null;
        
        public TransformController(TransformData data)
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

        public void AddFutureState(Vector3 dp, Quaternion dr, Vector3 ds)
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
            Position = Vector3.Lerp(PrevPosition, NextPosition, (float) InterpolationCoeff);
            Rotation = Quaternion.Slerp(PrevRotation, NextRotation, (float) InterpolationCoeff);
            Scale = Vector3.Lerp(PrevScale, NextScale, (float) InterpolationCoeff);

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
