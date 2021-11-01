using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore
{
    public abstract class Trigger
    {
        public Trigger() { }

    }

    public class StateTimeEventTrigger : Trigger
    {
        public float Seconds;
        public float RandomSeconds;
        
        public StateTimeEventTrigger() { }
    }

    public class AnimFrameEventTrigger : Trigger
    {
        public AnimFrameEventTrigger() { }
        public AnimationData AnimData;
        public string Animation;
        public int FrameStart;
        public int FrameEnd;
        public bool StartFromEnd;
    }

    public class PlayerNearbyEventTrigger : Trigger
    {
        public string RequirePlayerAction;
        public float Angle;
        public float AngleOffset;
        public float AnglePlayerRelative;
        public string DistanceCheckType;
        public bool Inverse;
        public float Distance;
        
        public PlayerNearbyEventTrigger()
        {

        }
    }





    public abstract class Action
    {
        public Action() { }

        public abstract Action Clone();

    }

    public enum NodeActivationState
    {
        Activate,
        Deactivate,
        Toggle,
        Remove,
        RemoveChildren
    }
    
    public class NodeActivationAction: Action
    {
        public string TargetName;
        public SceneGraphNode Target;
        public NodeActivationState TargetState;
        public bool UseMasterModel;

        public override Action Clone()
        {
            NodeActivationAction act = new()
            {
                TargetName = TargetName,
                TargetState = TargetState,
                UseMasterModel = UseMasterModel
            };
            return act;
        }
    }

    public class PlayAnimAction: Action
    {
        public string Animation;
        
        public override Action Clone()
        {
            PlayAnimAction act = new()
            {
                Animation = Animation
            };

            return act;
        }
    }

    public class GoToStateAction: Action
    {
        public string State;
        public bool Broadcast;
        public string BroadcastLevel;
        
        public override Action Clone()
        {
            GoToStateAction act = new()
            {
                State = State,
                Broadcast = Broadcast,
                BroadcastLevel = BroadcastLevel
            };

            return act;
        }

    }

    public class ActionTrigger
    {
        public List<Action> Actions;
        public Trigger Trigger;
        
        public ActionTrigger() {
            Actions = new List<Action>();
        }

        public ActionTrigger(ActionTrigger at)
        {
            //Populate Actions
            Actions = new List<Action>();
            foreach (Action a in at.Actions)
            {
                Actions.Add(a.Clone());
            }
        }
    }

    public class ActionTriggerState
    {
        public int StateID;
        public List<ActionTrigger> Triggers;
        public ActionTriggerState() { }

        public ActionTriggerState(ActionTriggerState ats)
        {
            Triggers = new List<ActionTrigger>();
            
            //Populate Triggers
            foreach (ActionTrigger at in ats.Triggers)
            {
                Triggers.Add(new ActionTrigger(at));
            }
        }
    }


   public class TriggerActionComponent : Component
    {
        public List<ActionTriggerState> States;
        public Dictionary<string, ActionTriggerState> StateMap;

        public TriggerActionComponent()
        {
            States = new List<ActionTriggerState>();
            StateMap = new Dictionary<string, ActionTriggerState>();
        }

        public TriggerActionComponent(TriggerActionComponent tacd)
        {
            //Populate States
            States = new List<ActionTriggerState>();
            StateMap = new Dictionary<string, ActionTriggerState>();
            foreach (ActionTriggerState s in tacd.States)
            {
                ActionTriggerState ats = new ActionTriggerState(s);
                States.Add(ats);
            }
        }

        public override Component Clone()
        {
            //TODO: Make sure to properly populate the new object
            return new TriggerActionComponent();
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }

    }
}
