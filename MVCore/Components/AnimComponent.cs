using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore
{
    public class AnimComponent : Component
    {
        //animations list Contains all the animations bound to the locator through Tkanimationcomponent
        public List<Animation> _animations = new();
        public Dictionary<string, Animation> _animDict = new();

        public List<Animation> Animations
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

        public Animation getAnimation(string Name)
        {
            if (!_animDict.ContainsKey(Name))
                return null;
            return _animDict[Name];
        }

        public List<Animation> getActiveAnimations()
        {
            List<Animation> animList = new();
            
            foreach (Animation ad in _animations)
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
            foreach (Animation ad in _animations)
            {
                Animation clone = new Animation(ad);
                ac.Animations.Add(clone);
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
