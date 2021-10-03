using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore
{
    public class AnimComponent : Component
    {
        //animations list Contains all the animations bound to the locator through Tkanimationcomponent
        public List<AnimData> _animations = new();
        public Dictionary<string, AnimData> _animDict = new();

        public List<AnimData> Animations
        {
            get
            {
                return _animations;
            }
        }

        //Default Constructor
        public AnimComponent()
        {

        }

        public AnimData getAnimation(string Name)
        {
            if (!_animDict.ContainsKey(Name))
                return null;
            return _animDict[Name];
        }

        public List<AnimData> getActiveAnimations()
        {
            List<AnimData> animList = new();
            
            foreach (AnimData ad in _animations)
            {
                if (ad.IsPlaying)
                    animList.Add(ad);
            }
                
            return animList;
        }

        public void copyFrom(AnimComponent input)
        {
            //Base class is dummy
            //base.copyFrom(input); //Copy stuff from base class

            //TODO: Copy Animations

        }

        public override Component Clone()
        {
            AnimComponent ac = new();

            //Copy Animations
            foreach (AnimData ad in _animations)
            {
                AnimData clone = ad.Clone();
                ac.Animations.Add(clone);
                ac._animDict[clone.PName] = clone;
            }
                
            return ac;
        }

        public void update()
        {

        }

        protected AnimComponent(AnimComponent input)
        {
            this.copyFrom(input);
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }

    }
}
