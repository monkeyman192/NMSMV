using System;
using libMBIN.NMS.Toolkit;

namespace MVCore
{
    public class PhysicsData : TkPhysicsData
    {
        public PhysicsData() : base()
        {
            
        }

        public PhysicsData(TkPhysicsData pd)
        {
            AngularDamping = pd.AngularDamping;
            Friction = pd.Friction;
            RollingFriction = pd.RollingFriction;
            Gravity = pd.Gravity;
            LinearDamping = pd.LinearDamping;
            Mass = pd.Mass;
        }

    }

    

    
    public class PhysicsComponent : Component
    {
        public TkPhysicsComponentData Template;
        public PhysicsData Data;

        //Default Constructor
        public PhysicsComponent()
        {
            Template = new TkPhysicsComponentData();
            Data = new PhysicsData();
        }
        
        public PhysicsComponent(PhysicsComponent pc)
        {
            Template = new TkPhysicsComponentData()
            {
                AllowTeleporter = pc.Template.AllowTeleporter,
                BlockTeleporter = pc.Template.BlockTeleporter,
                Data = pc.Template.Data,
                SpinOnCreate = pc.Template.SpinOnCreate,
                DisableGravity = pc.Template.DisableGravity,
                InvisibleForInteraction = pc.Template.InvisibleForInteraction,
                CameraInvisible = pc.Template.CameraInvisible,
                NoPlayerCollide = pc.Template.NoPlayerCollide,
                NoVehicleCollide = pc.Template.NoVehicleCollide,
                IgnoreModelOwner = pc.Template.IgnoreModelOwner,
                Floor = pc.Template.Floor,
                Climbable = pc.Template.Climbable,
                TriggerVolume = pc.Template.TriggerVolume,
                SurfaceProperties = pc.Template.SurfaceProperties,
                VolumeTriggerType = pc.Template.VolumeTriggerType,
                RagdollData = pc.Template.RagdollData
            };
            Data = new PhysicsData(pc.Data);
        }

        public PhysicsComponent(TkPhysicsComponentData pcd)
        {
            Template = pcd;
            Data = new PhysicsData(pcd.Data);
        }
        
        public override Component Clone()
        {
            return new PhysicsComponent(this);
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }
    }
}
