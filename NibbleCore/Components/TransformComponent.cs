using System;
using OpenTK.Mathematics;

namespace NbCore
{
    //TODO: Make sure that every entity (previous model) uses this component by default

    

    public unsafe class TransformComponent : Component {

        public TransformData Data;
        
        public TransformComponent(TransformData data): base()
        {
            Data = data;
        }

        public override Component Clone()
        {
            //Use the same Data reference to the clone as well (not sure if this is correct)
            TransformComponent n = new TransformComponent(Data);
            
            return n;
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }
    }
}
