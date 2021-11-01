using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Mathematics;

namespace NbCore.Systems
{
    public class AnimationSystem : EngineSystem
    {
        public List<Entity> AnimScenes = new();
        public Dictionary<string, Animation> Animations = new();
        
        public AnimationSystem() : base(EngineSystemEnum.ANIMATION_SYSTEM)
        {
            
        }

        public override void CleanUp()
        {
            AnimScenes.Clear();
            Animations.Clear();
        }

        public override void OnRenderUpdate(double dt)
        {
            throw new NotImplementedException();
        }

        public override void OnFrameUpdate(double dt)
        {
            /* REWRITE
            //Clear queues for all the joints
            foreach (Model anim_model in AnimScenes)
            {
                foreach (Joint jt in anim_model.parentScene.jointDict.Values)
                {
                    jt.PositionQueue.Clear();
                    jt.ScaleQueue.Clear();
                    jt.RotationQueue.Clear();
                }
            }
                
            foreach (Model anim_model in AnimScenes)
            {
                AnimComponent ac = anim_model.GetComponent<AnimComponent>() as AnimComponent;
                
                bool found_first_active_anim = false;

                foreach (AnimData ad in ac.Animations)
                {
                    if (ad.IsPlaying)
                    {
                        if (!ad.loaded)
                            ad.LoadData();
                        
                        found_first_active_anim = true;
                        //Load updated local joint transforms
                        foreach (libMBIN.NMS.Toolkit.TkAnimNodeData node in ad.animMeta.NodeData)
                        {
                            if (!anim_model.parentScene.jointDict.ContainsKey(node.Node))
                                continue;

                            Joint jt = anim_model.parentScene.jointDict[node.Node];

                            //Transforms
                            Vector3 p = new Vector3();
                            Vector3 s = new Vector3();
                            Quaternion q = new Quaternion();

                            ad.GetCurrentTransform(ref p, ref s, ref q, node.Node);

                            jt.RotationQueue.Add(q);
                            jt.PositionQueue.Add(p);
                            jt.ScaleQueue.Add(s);

                            //ad.applyNodeTransform(tj, node.Node);
                        }

                        //Once the current frame data is fetched, progress to the next frame
                        ad.Update(dt);
                    } 
                }

                //Calculate Blending Factors
                List<float> blendingFactors = new List<float>();
                float totalWeight = 1.0f;
                foreach (AnimData ad in ac.Animations)
                {
                    if (ad.AnimType == libMBIN.NMS.Toolkit.TkAnimationData.AnimTypeEnum.OneShot)
                    {
                        //Calculate blending factor based on the animation progress
                        //float bF = ad.ActiveFrame / (ad.FrameEnd - ad.FrameStart);
                        float bF = 0.0f;
                        blendingFactors.Add(bF);
                        totalWeight -= bF;
                    }
                    else
                    {
                        blendingFactors.Add(1.0f);
                    }
                        
                }

                //Blend Transforms and apply
                foreach (Joint jt in anim_model.parentScene.jointDict.Values)
                {
                    if (jt.PositionQueue.Count == 0)
                    {
                        //Keep last transforms
                        //jt.localPosition = jt._localPosition;
                        //jt.localScale = jt._localScale;
                        //jt.localRotation = jt._localRotation;
                        continue;
                    }
                        
                    float blendFactor = 1.0f / jt.PositionQueue.Count;

                    Vector3 p = new Vector3();
                    Vector3 s = new Vector3();
                    Quaternion q = new Quaternion();

                    
                    for (int i = 0; i < jt.PositionQueue.Count; i++)
                    {
                        q += blendFactor * jt.RotationQueue[i];
                        p += blendFactor * jt.PositionQueue[i];
                        s += blendFactor * jt.ScaleQueue[i];
                    }

                    TransformationSystem.SetEntityLocation(jt, p);
                    TransformationSystem.SetEntityRotation(jt, q);
                    TransformationSystem.SetEntityScale(jt, s);
                }
            }

            */
        }

        public static void StartAnimation(Entity anim_model, string Anim)
        {
            AnimComponent ac = anim_model.GetComponent<AnimComponent>() as AnimComponent;
            Animation ad = ac.getAnimation(Anim);

            if (ad != null)
            {
                if (!ad.IsPlaying)
                    ad.IsPlaying = true;
            }
        }

        public static void StopActiveAnimations(SceneGraphNode anim_model)
        {
            AnimComponent ac = anim_model.GetComponent<AnimComponent>() as AnimComponent;
            List<Animation> ad_list = ac.getActiveAnimations();
          
            foreach (Animation ad in ad_list)
                ad.IsPlaying = false;
        }

        public static void StopActiveLoopAnimations(Entity anim_model)
        {
            AnimComponent ac = anim_model.GetComponent<AnimComponent>() as AnimComponent;
            List<Animation> ad_list = ac.getActiveAnimations();

            foreach (Animation ad in ad_list)
            {
                if (ad.animData.AnimType == AnimationType.Loop)
                    ad.IsPlaying = false;
            }
                
        }

        public static int queryAnimationFrame(Entity anim_model, string Anim)
        {
            AnimComponent ac = anim_model.GetComponent<AnimComponent>() as AnimComponent;
            Animation ad = ac.getAnimation(Anim);

            if (ad != null)
            {
                return ad.GetActiveFrameIndex();
            }
            return -1;
        }

        public static int queryAnimationFrameCount(Entity anim_model, string Anim)
        {
            AnimComponent ac = anim_model.GetComponent<AnimComponent>() as AnimComponent;
            Animation ad = ac.getAnimation(Anim);

            if (ad != null)
            {
                return ad.animData.FrameCount;
            }
            return -1;
        }

        public void Add(Entity m)
        {
            AnimScenes.Add(m);
        }

        
    }
}
