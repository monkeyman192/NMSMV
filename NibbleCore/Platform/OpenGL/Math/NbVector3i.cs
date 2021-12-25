using OpenTK.Mathematics;

namespace NbCore.Math
{
    public struct NbVector3i
    {
        internal Vector3i _Value;

        public NbVector3i(Vector3i vec)
        {
            _Value = vec;
        }
        
        public NbVector3i(NbVector3i vec)
        {
            _Value = vec._Value;
        }
        
        public NbVector3i(int x = 0)
        {
            _Value = new Vector3i(x);
        }
        
        public NbVector3i(int x, int y, int z)
        {
            _Value.X = x;
            _Value.Y = y;
            _Value.Z = z;
        }
        //Methods
        public NbVector3 Cross(NbVector3 a)
        {
            return new NbVector3()
            {
                _Value = Vector3.Cross(_Value, a._Value)
            };
        }
            
        //Exposed Properties
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
        
        public int Z
        {
            get => _Value.Z;
            set => _Value.Z = value;
        }
        
        public static NbVector3i operator +(NbVector3i a)
        {
            return a;
        }
        
        public static NbVector3i operator -(NbVector3i a)
        {
            NbVector3i n = new()
            {
                _Value = -a._Value
            };
            return n;
        }
        
        public static NbVector3i operator +(NbVector3i a, NbVector3i b)
        {
            NbVector3i n = new()
            {
                _Value = a._Value + b._Value
            };
            return n;
        }
        
        public static NbVector3i operator -(NbVector3i a, NbVector3i b)
        {
            NbVector3i n = new()
            {
                _Value = a._Value - b._Value
            };
            return n;
        }

        public static NbVector3i operator *(NbVector3i v, int a)
        {
            return new NbVector3i()
            {
                _Value = v._Value * a
            };
        }
        
        public static NbVector3i operator *(int a, NbVector3i v)
        {
            return v * a;
        }
        
    }
}