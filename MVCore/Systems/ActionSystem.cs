using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using libMBIN.NMS;
using libMBIN.NMS.GameComponents;
using MathNet.Numerics;
using MVCore.Common;


namespace MVCore.Systems
{
    public class ActionSystem : EngineSystem
    {
        public List<SceneGraphNode> ActionSceneNodes = new();
        public Dictionary<long, string> ActionSceneStateMap = new();
        public Dictionary<long, List<Action>> ActionsExecutedInState = new();
        public Dictionary<long, string> PrevActionSceneStateMap = new();
        private readonly float timeInterval = 1000.0f / 60.0f;
        private float time = 0.0f;

        public ActionSystem() : base(EngineSystemEnum.ACTION_SYSTEM)
        {

        }

        public override void CleanUp()
        {
            ActionSceneStateMap.Clear();
            ActionSceneNodes.Clear();
        }

        public override void Update(double dt)
        {
            time += (float) dt;

            if (time < timeInterval)
                return;
            else
                time = 0.0f;
            
            foreach (SceneGraphNode m in ActionSceneNodes)
            {
                TriggerActionComponent tac = m.GetComponent<TriggerActionComponent>() as TriggerActionComponent;
                ActionTriggerState activeState = tac.StateMap[ActionSceneStateMap[m.ID]];
                
                //Apply Actions of state
                Console.WriteLine("Current State {0}", activeState.StateID);

                foreach (ActionTrigger at in activeState.Triggers)
                {
                    //Check if Trigger is activated
                    bool trigger_active = TestTrigger(m, at.Trigger);

                    if (trigger_active)
                    {
                        //Execute actions
                        foreach (Action a in at.Actions)
                        {
                            if (!ActionsExecutedInState[m.ID].Contains(a))
                                ExecuteAction(m, a);
                        }
                    }   
                }
            }
        }
        
        private static bool TestTrigger(SceneGraphNode m, Trigger t)
        {
            if (t is null)
            {
                return true;
            }
            else if (t is PlayerNearbyEventTrigger)
            {
                return TestPlayerNearbyEventTrigger(m, t as PlayerNearbyEventTrigger);
            }
            else if (t is AnimFrameEventTrigger)
            {
                return TestAnimFrameEventTrigger(m, t as AnimFrameEventTrigger);
            }
            else if (t is StateTimeEventTrigger)
            {
                return true; //I don't have the timers implemented to properly add support for that yet
            }
            return false;
        }

        private static bool TestAnimFrameEventTrigger(SceneGraphNode m, AnimFrameEventTrigger t)
        {
            int target_frame = 0;
            int anim_frameCount = AnimationSystem.queryAnimationFrameCount(m, t.Anim);
            
            if (t.StartFromEnd)
            {
                target_frame = anim_frameCount - t.FrameStart;
            } 
            else
                target_frame = t.FrameStart;

            int active_frame = AnimationSystem.queryAnimationFrame(m, t.Anim);

            if (active_frame >= target_frame)
                return true;

            return false;
        }

        private static bool TestPlayerNearbyEventTrigger(SceneGraphNode m, PlayerNearbyEventTrigger t)
        {
            //Check the distance of the activeCamera from the model
            float distanceFromCam = (RenderState.activeCam.Position - 
                                    TransformationSystem.GetEntityWorldPosition(m).Xyz).Length;

            //TODO: Check all the inverse shit and the rest trigger parameters

            if (t.Inverse)
            {
                if (distanceFromCam > t.Distance)
                    return true;
            } else
            {
                if (distanceFromCam < t.Distance)
                    return true;
            }
            
            return false;
        }

        private void ExecuteAction(SceneGraphNode m, Action action)
        {
            switch (action.GetType().Name)
            {
                case nameof(NodeActivationAction):
                    ExecuteNodeActivationAction(m, action as NodeActivationAction);
                    break;
                case nameof(GoToStateAction):
                    ExecuteGoToStateAction(m, action as GoToStateAction);
                    break;
                case nameof(PlayAnimAction):
                    ExecutePlayAnimAction(m, action as PlayAnimAction);
                    break;
                default:
                    //Console.WriteLine("unimplemented Action execution");
                    break;
            }
        }

        private void ExecuteGoToStateAction(SceneGraphNode m, GoToStateAction action)
        {
            //Change State
            PrevActionSceneStateMap[m.ID] = ActionSceneStateMap[m.ID];
            ActionSceneStateMap[m.ID] = action.State;
            ActionsExecutedInState[m.ID] = new List<Action>(); //Reset executed actions 
        }

        private void ExecutePlayAnimAction(SceneGraphNode m, PlayAnimAction action)
        {
            AnimationSystem.StopActiveLoopAnimations(m); //Not sure if this is correct
            AnimationSystem.StartAnimation(m, action.Anim);
            ActionsExecutedInState[m.ID].Add(action);
        }

        private void ExecuteNodeActivationAction(SceneGraphNode m, NodeActivationAction action)
        {
            
            if (action.Target == null)
            {
                //Find action target
                if (action.UseMasterModel)
                {
                    //Find Parent Scene
                    m.Parent.findNodeByName(action.Name, ref action.Target);
                }
                else
                {
                    m.findNodeByName(action.Name, ref action.Target);
                }
            }
            
            if (action.Target == null)
            {
                Log("Node Not Found", LogVerbosityLevel.ERROR);
                return;
            }

            switch (action.NodeActiveState)
            {
                case "Activate":
                    action.Target.IsRenderable = true;
                    break;
                case "Deactivate":
                    action.Target.IsRenderable = false;
                    break;
                case "Toggle":
                    action.Target.IsRenderable = !action.Target.IsRenderable;
                    break;
                default:
                    Console.WriteLine("Not implemented");
                    break;
            }

        }

        public void Add(SceneGraphNode scn)
        {
            if (scn.HasComponent<TriggerActionComponent>())
            {
                ActionSceneNodes.Add(scn);
                ActionSceneStateMap[scn.ID] = "BOOT"; //Add Default State
                PrevActionSceneStateMap[scn.ID] = "NONE"; //Add Default State
                ActionsExecutedInState[scn.ID] = new();
            }
        }
    }
}
