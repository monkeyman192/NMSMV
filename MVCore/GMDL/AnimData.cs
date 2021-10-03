using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
//using System.Windows.Forms;
//using System.Windows.Media.Animation;
using MVCore.Utils;
using MVCore.Systems;
using OpenTK;
using OpenTK.Mathematics;
using libMBIN.NMS.Toolkit;


namespace MVCore
{
    //TODO Remove the libmbin dependency
    public class AnimData : TkAnimationData, INotifyPropertyChanged
    {
        public AnimMetadata animMeta;
        private int prevFrameIndex = 0;
        private int activeFrameIndex = 0;
        private int nextFrameIndex = 0;
        private float animationTime = 0.0f;
        private float prevFrameTime = 0.0f;
        private float nextFrameTime = 0.0f;
        private float LERP_coeff = 0.0f;
        public bool loaded = false;
        private bool _playing = false;

        public event PropertyChangedEventHandler PropertyChanged;

        //Constructors
        public AnimData(TkAnimationData ad)
        {
            Anim = ad.Anim;
            Filename = ad.Filename;
            FrameStart = ad.FrameStart;
            FrameEnd = ad.FrameEnd;
            StartNode = ad.StartNode;
            AnimType = ad.AnimType;
            Speed = ad.Speed;
            Additive = ad.Additive;
        }

        public AnimData()
        {

        }

        

        public AnimData Clone()
        {
            AnimData ad = new();

            ad.Anim = Anim;
            ad.Filename = Filename;
            ad.FrameStart = FrameStart;
            ad.FrameEnd = FrameEnd;
            ad.StartNode = StartNode;
            ad.AnimType = AnimType;
            ad.Speed = Speed;
            ad.Additive = Additive;
            ad.animMeta = animMeta;

            return ad;
        }

        //Properties

        public string PName
        {
            get { return Anim; }
            set { Anim = value; }
        }

        public bool IsPlaying
        {
            get { return _playing; }
            set
            {
                _playing = value;
                prevFrameIndex = 0; //Reset frame counter on animation
                NotifyPropertyChanged("IsPlaying");
            }
        }

        public bool PActive
        {
            get { return Active; }
            set 
            { 
                Active = value;
                NotifyPropertyChanged("Active");
            }
        }

        public bool _override = false;
        public bool Override
        {
            get { return _override; }
            set
            {
                _override = value;
                NotifyPropertyChanged("Override");
            }
        }

        public int ActiveFrame
        {
            get { return activeFrameIndex; }
            set
            {
                activeFrameIndex = value;
                NotifyPropertyChanged("ActiveFrame");
            }
        }

        public int FrameCount
        {
            get { return (animMeta != null) ?  animMeta.FrameCount - 1 : 0;}
        }

        public bool IsValid
        {
            get { return Filename != ""; }
        }

        public string PAnimType
        {
            get
            {
                return AnimType.ToString();
            }
        }

        public bool PAdditive
        {
            get { return Additive; }
            set { Additive = value; }
        }

        public float PSpeed
        {
            get { return Speed; }
            set { Speed = value; }
        }

        //UI update
        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        public void LoadData()
        {
            if (Filename != "")
                FetchAnimMetaData();
        }


        private void FetchAnimMetaData()
        {
            if (Common.RenderState.engineRef.animationSys.Animations.ContainsKey(Filename))
            {
                animMeta = Common.RenderState.engineRef.animationSys.Animations[Filename];
            }
            else
            {
                //TODO: REDO get rid of libmbin
                
                //TkAnimMetadata amd = Import.NMS.FileUtils.LoadNMSTemplate(Filename) as TkAnimMetadata;
                //animMeta = new AnimMetadata(amd);
                //animMeta.load(); //Load data as well
                //Common.RenderState.engineRef.animationSys.Animations[Filename] = animMeta;
            }
            NotifyPropertyChanged("FrameCount");
        }


        public void Update(float dt) //time in milliseconds
        {
            if (!loaded)
            {
                FetchAnimMetaData();
                loaded = true;
            }
            
            animationTime += dt;
            Progress();
        }


        public void Progress() 
        {
            //Override frame based on the GUI
            if (Override)
            {
                //Find frames
                prevFrameIndex = ActiveFrame;
                nextFrameIndex = ActiveFrame;
                LERP_coeff = 0.0f;
                return;
            }

            int activeFrameCount = (FrameEnd == 0 ? animMeta.FrameCount : Math.Min(FrameEnd, animMeta.FrameCount)) - (FrameStart != 0 ? FrameStart : 0);
            //Assuming a fixed frequency of 60 fps for the animations
            float activeAnimDuration = activeFrameCount * 1000.0f / 60.0f; // In ms TOTAL
            float activeAnimInterval = activeAnimDuration / (activeFrameCount - 1); // Per frame time

            if (animationTime > activeAnimDuration)
            {
                if ((AnimType == AnimTypeEnum.OneShot) && animationTime > activeAnimDuration)
                {
                    animationTime = 0.0f;
                    prevFrameTime = 0.0f;
                    nextFrameTime = 0.0f;
                    IsPlaying = false;
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
                ActiveFrame = prevFrameIndex;
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
            Quaternion prev_q = animMeta.anim_rotations[node][prevFrameIndex];
            Vector3 prev_p = animMeta.anim_positions[node][prevFrameIndex];
            Vector3 prev_s = animMeta.anim_scales[node][prevFrameIndex];

            //Fetch nextFrame stuff
            Quaternion next_q = animMeta.anim_rotations[node][nextFrameIndex];
            Vector3 next_p = animMeta.anim_positions[node][nextFrameIndex];
            Vector3 next_s = animMeta.anim_scales[node][nextFrameIndex];

            //Interpolate
            Quaternion q = Quaternion.Slerp(next_q, prev_q, LERP_coeff);
            Vector3 p = next_p * LERP_coeff + prev_p * (1.0f - LERP_coeff);
            Vector3 s = next_s * LERP_coeff + prev_s * (1.0f - LERP_coeff);

            //Convert transforms
            tc.AddFutureState(p, q, s);
        }

        public void GetCurrentTransform(ref Vector3 p, ref Vector3 s, ref Quaternion q, string node)
        {
            //Fetch prevFrame stuff
            Quaternion prev_q = animMeta.anim_rotations[node][prevFrameIndex];
            Vector3 prev_p = animMeta.anim_positions[node][prevFrameIndex];
            Vector3 prev_s = animMeta.anim_scales[node][prevFrameIndex];

            //Fetch nextFrame stuff
            Quaternion next_q = animMeta.anim_rotations[node][nextFrameIndex];
            Vector3 next_p = animMeta.anim_positions[node][nextFrameIndex];
            Vector3 next_s = animMeta.anim_scales[node][nextFrameIndex];

            //Interpolate
            q = Quaternion.Slerp(prev_q, next_q, LERP_coeff);
            p = Vector3.Lerp(prev_p, next_p, LERP_coeff);
            s = Vector3.Lerp(prev_s, next_s, LERP_coeff);
            
        }

    }

}
