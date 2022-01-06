using System;

namespace NbCore
{
    public class GUIDComponent : Component
    {
        public long ID = 0xFFFFFFFF;
        public long testID = 0; //TODO: remove that when we're happy with memory disposal
        public bool Initialized = false;
        public static long test_counter = 1;

        public void Init(long id)
        {
            if (Initialized)
                return;
            ID = id;
            Initialized = true;
        }

        public GUIDComponent()
        {
            testID = test_counter++;
            if (testID == 120)
                Console.WriteLine("break");
        }
        
        public override Component Clone()
        {
            return new GUIDComponent(); //Create a brand new clone
        }

        public override void CopyFrom(Component c)
        {
            //Copying the same values to a copy will lead to errors
            return; 
        }
        
    }
}