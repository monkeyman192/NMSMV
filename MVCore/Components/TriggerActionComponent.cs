using System;
using System.Collections.Generic;
using System.Text;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS;

namespace MVCore
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
        public NodeActivationState TargetState;
        public bool UseMasterModel;

        public NodeActivationAction()
        {

        }
        
    }

    public class PlayAnimAction: Action
    {
        public string Animation;
        
        public PlayAnimAction()
        {

        }

        //Expose Properties
        
    }

    public class GoToStateAction: Action
    {
        public string State;
        public bool Broadcast;
        public string BroadcastLevel;
        
        public GoToStateAction()
        {

        }

    }

    public class ActionTrigger
    {
        public List<Action> Actions;
        public Trigger Trigger;
        
        public ActionTrigger() {
            Actions = new List<Action>();
        }

        public ActionTrigger(GcActionTrigger at)
        {
            //Populate Actions
            Actions = new List<Action>();
            foreach (NMSTemplate t in at.Action)
            {
                if (t is GcNodeActivationAction)
                {
                    NodeActivationAction nAA = new(t);
                    Actions.Add(nAA);
                }   
                else if (t is GcGoToStateAction)
                    Actions.Add(new GoToStateAction(t));
                else if (t is GcPlayAnimAction)
                    Actions.Add(new PlayAnimAction(t));
                else if (t is EmptyNode)
                    continue;
                else
                    Console.WriteLine("Non Implemented Action");
            }
            
            //Set Trigger
            if (at.Trigger is GcPlayerNearbyEvent)
            {
                Trigger = new PlayerNearbyEventTrigger(at.Trigger);
            } 
            else if (at.Trigger is GcStateTimeEvent)
            {
                Trigger = new StateTimeEventTrigger(at.Trigger);
            }
            else if (at.Trigger is GcAnimFrameEvent)
            {
                Trigger = new AnimFrameEventTrigger(at.Trigger);
            }
            else
            {
                Console.WriteLine("Non Implemented Trigger");
                Trigger = null;
            }
            


        }

        //Exposed Properties


    }

    public class ActionTriggerState
    {
        GcActionTriggerState _template;
        public List<ActionTrigger> Triggers;
        public ActionTriggerState() { }

        public ActionTriggerState(GcActionTriggerState ats)
        {
            _template = ats;
            Triggers = new List<ActionTrigger>();
            
            //Populate Triggers
            foreach (GcActionTrigger at in _template.Triggers)
            {
                Triggers.Add(new ActionTrigger(at));
            }
        }

        //Exposed Properties
        public string StateID
        {
            get
            {
                return _template.StateID;
            }
        }

    }


   public class TriggerActionComponent : Component
    {
        public GcTriggerActionComponentData _template;
        public List<ActionTriggerState> States;
        public Dictionary<string, ActionTriggerState> StateMap;

        public TriggerActionComponent()
        {
            States = new List<ActionTriggerState>();
            StateMap = new Dictionary<string, ActionTriggerState>();
        }

        public TriggerActionComponent(GcTriggerActionComponentData tacd)
        {
            _template = tacd;
            //Populate States
            States = new List<ActionTriggerState>();
            StateMap = new Dictionary<string, ActionTriggerState>();
            foreach (GcActionTriggerState s in tacd.States)
            {
                ActionTriggerState ats = new ActionTriggerState(s);
                States.Add(ats);
                StateMap[ats.StateID] = ats;
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

        //Exposed Properties

        public bool HideModel
        {
            get { return _template.HideModel; }
        }

        public bool StartInteractive
        {
            get { return _template.StartInactive; }
        }

    }
}
