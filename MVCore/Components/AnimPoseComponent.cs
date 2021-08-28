using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using libMBIN.NMS.Toolkit;
using MVCore.Utils;

namespace MVCore
{
    public class AnimPoseComponent : Component
    {
        public Entity ref_object = null;
        public TkAnimMetadata animMeta = null;
        //AnimationPoseData
        public List<AnimPoseData> _poseData = new();
        public TkAnimMetadata _poseFrameData = null; //Stores the actual poseFrameData
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

        public AnimPoseComponent(TkAnimPoseComponentData apcd)
        {
            _poseFrameData = (TkAnimMetadata) NMSUtils.LoadNMSTemplate(apcd.Filename,
                ref Common.RenderState.activeResMgr);

            //Load PoseAnims
            for (int i = 0; i < apcd.PoseAnims.Count; i++)
            {
                AnimPoseData my_apd = new(apcd.PoseAnims[i]);
                poseData.Add(my_apd);
            }
        }

        public override Component Clone()
        {
            return new AnimPoseComponent();
        }

        public override void CopyFrom(Component c)
        {
            if (c is not AnimPoseComponent)
                return;
            
            //TODO: Copy stuff

            
        }
    }
}
