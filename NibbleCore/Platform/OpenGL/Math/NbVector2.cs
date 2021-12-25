using OpenTK.Mathematics;

namespace NbCore.Math
{
    public struct NbVector2
    {
        internal Vector2 _Value;
        
        public float X
        {
            get => _Value.X;
            set => _Value.X = value;
        }
        
        public float Y
        {
            get => _Value.Y;
            set => _Value.Y = value;
        }

        public NbVector2(float v)
        {
            _Value = new Vector2(v);
        }
        
        public NbVector2(float a, float b)
        {
            _Value = new Vector2(a,b);
        }
    }
}