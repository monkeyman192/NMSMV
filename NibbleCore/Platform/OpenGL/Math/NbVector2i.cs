using OpenTK.Mathematics;

namespace NbCore.Math
{
    public class NbVector2i
    {
        internal Vector2i _Value;
        
        public int X
        {
            get => _Value.X;
            set => _Value.X = value;
        }
        
        public int Y
        {
            get => _Value.Y;
            set => _Value.Y = value;
        }

        public NbVector2i()
        {
            _Value = Vector2i.Zero;
        }
        
        public NbVector2i(int i)
        {
            _Value = new Vector2i(i);
        }
        
        public NbVector2i(int i, int j)
        {
            _Value = new Vector2i(i, j);
        }

        public static bool operator ==(NbVector2i a, NbVector2i b)
        {
            return a._Value == b._Value;
        }
        
        public static bool operator !=(NbVector2i a, NbVector2i b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return (this == (NbVector2i) obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}