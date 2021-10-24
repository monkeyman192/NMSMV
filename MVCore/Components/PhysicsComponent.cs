using System;

namespace MVCore
{
    public class PhysicsData
    {
        public float Mass;
        public float Friction;
        public float RollingFriction;
        public float Gravity;
        
        public PhysicsData()
        {
            
        }

        public void CopyFrom(PhysicsData pd)
        {
            Mass = pd.Mass;
            Friction = pd.Friction;
            RollingFriction = pd.RollingFriction;
            Gravity = pd.Gravity;
        }

        public PhysicsData(PhysicsData pd)
        {
            CopyFrom(pd);
        }

    }

    

    
    public class PhysicsComponent : Component
    {
        public PhysicsData Data;

        //Default Constructor
        public PhysicsComponent()
        {
            Data = new PhysicsData();
        }
        
        public PhysicsComponent(PhysicsComponent pc)
        {
            Data = new PhysicsData(pc.Data);
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
