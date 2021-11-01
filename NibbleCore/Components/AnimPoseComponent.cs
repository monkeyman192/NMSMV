using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using NbCore.Utils;

namespace NbCore
{
    public class AnimPoseComponent : Component
    {
        public Entity ref_object = null;
        //AnimationPoseData
        public List<AnimPoseData> _poseData = new();
        
        public List<AnimPoseData> poseData
        {
            get
            {
                return _poseData;
            }
        }

        //Default Constructor
        public AnimPoseComponent()
        {

        }

        public override Component Clone()
        {
            return new AnimPoseComponent();
        }

        public override void CopyFrom(Component c)
        {
            if (c is not AnimPoseComponent)
                return;
        }
    }
}
