using MathNet.Numerics.Distributions;
using OpenTK.Mathematics;
using System;

namespace MVCore
{
    public enum AnimationType
    {
        OneShot,
        Loop
    }
    
    public class Animation
    {
        public AnimationData animData; //Static Animation Data
        private int prevFrameIndex = 0;
        private int activeFrameIndex = 0;
        private int nextFrameIndex = 0;
        private float animationTime = 0.0f;
        private float prevFrameTime = 0.0f;
        private float nextFrameTime = 0.0f;
        private float LERP_coeff = 0.0f;
        public bool loaded = false;
        private bool _playing = false;
        public bool Override = false; //Used to manually manipulate animation
        
        //Constructors
        public Animation()
        {
            
        }
        public Animation(Animation anim)
        {
            CopyFrom(anim);
        }

        public void CopyFrom(Animation anim)
        {
            
        }

        public Animation Clone()
        {
            Animation ad = new();
            ad.CopyFrom(this);
            

            return ad;
        }

        public bool IsValid()
        {
            return animData.FileName != "";
        }

        public void Update(float dt) //time in milliseconds
        {
            animationTime += dt;
            Progress();
        }


        public void Progress() 
        {
            //Override frame based on the GUI
            if (Override)
                return;
            //TODO: The imgui panel for animation should set all these values
            /*
            {
                //Find frames
                prevFrameIndex = activeFrameIndex;
                nextFrameIndex = activeFrameIndex;
                LERP_coeff = 0.0f;
                return;
            }
            */

            int activeFrameCount = (animData.FrameEnd == 0 ? animData.FrameCount : Math.Min(animData.FrameEnd, animData.FrameCount)) - (animData.FrameStart != 0 ? animData.FrameStart : 0);
            //Assuming a fixed frequency of 60 fps for the animations
            float activeAnimDuration = activeFrameCount * 1000.0f / 60.0f; // In ms TOTAL
            float activeAnimInterval = activeAnimDuration / (activeFrameCount - 1); // Per frame time

            if (animationTime > activeAnimDuration)
            {
                if ((animData.Type == AnimationType.OneShot) && animationTime > activeAnimDuration)
                {
                    animationTime = 0.0f;
                    prevFrameTime = 0.0f;
                    nextFrameTime = 0.0f;
                    _playing = false;
                    return;
                }
                else
                {
                    animationTime %= activeAnimDuration; //Clamp to correct time span

                    //Properly calculate previous and nextFrameTimes
                    prevFrameIndex = (int) Math.Floor(animationTime / activeAnimInterval);
                    nextFrameIndex = (prevFrameIndex + 1) % activeFrameCount;
                    prevFrameTime = activeAnimInterval * prevFrameIndex;
                    nextFrameTime = prevFrameTime + activeAnimInterval;
                }
                    
            }


            if (animationTime > nextFrameTime)
            {
                //Progress animation
                prevFrameIndex = nextFrameIndex;
                activeFrameIndex = prevFrameIndex;
                prevFrameTime = nextFrameTime;
                
                nextFrameIndex = (prevFrameIndex + 1) % activeFrameCount;
                nextFrameTime = prevFrameTime + activeAnimInterval;
            }

            LERP_coeff = (animationTime - prevFrameTime) / activeAnimInterval;

            //Console.WriteLine("AnimationTime {0} PrevAnimationTime {1} NextAnimationTime {2} LERP Coeff {3}",
            //    animationTime, prevFrameTime, nextFrameTime, LERP_coeff);

        }

        //TODO: Use this new definition for animation blending
        //public void applyNodeTransform(model m, string node, out Quaternion q, out Vector3 p)
        public void ApplyNodeTransform(TransformController tc, string node)
        {
            //Fetch prevFrame stuff
            Quaternion prev_q = animData.GetNodeRotation(node, prevFrameIndex);
            Vector3 prev_p = animData.GetNodeTranslation(node, prevFrameIndex);
            Vector3 prev_s = animData.GetNodeScale(node, prevFrameIndex);

            //Fetch nextFrame stuff
            Quaternion next_q = animData.GetNodeRotation(node, nextFrameIndex);
            Vector3 next_p = animData.GetNodeTranslation(node, nextFrameIndex);
            Vector3 next_s = animData.GetNodeScale(node, nextFrameIndex);

            //Interpolate
            Quaternion q = Quaternion.Slerp(next_q, prev_q, LERP_coeff);
            Vector3 p = next_p * LERP_coeff + prev_p * (1.0f - LERP_coeff);
            Vector3 s = next_s * LERP_coeff + prev_s * (1.0f - LERP_coeff);

            //Convert transforms
            tc.AddFutureState(p, q, s);
        }

    }

}