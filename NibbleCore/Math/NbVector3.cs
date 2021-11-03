using OpenTK.Mathematics;

namespace NbCore.Math
{
    public class NbVector3
    {
        internal Vector3 _Value;

        public NbVector3()
        {
            _Value = Vector3.Zero;
        }
        
        public NbVector3(Vector3 vec)
        {
            _Value = vec;
        }
        
        public NbVector3(NbVector3 vec)
        {
            _Value = vec._Value;
        }
        
        public NbVector3(float x = 0.0f)
        {
            _Value = new Vector3(x);
        }
        
        public NbVector3(float x, float y, float z)
        {
            _Value.X = x;
            _Value.Y = y;
            _Value.Z = z;
        }
        //Methods
        public void Normalize()
        {
            _Value.Normalize();
        }

        public NbVector3 Cross(NbVector3 a)
        {
            return new NbVector3()
            {
                _Value = Vector3.Cross(_Value, a._Value)
            };
        }
            
        //Exposed Properties
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
        
        public float Z
        {
            get => _Value.Z;
            set => _Value.Z = value;
        }
        
        public static NbVector3 operator +(NbVector3 a)
        {
            return a;
        }
        
        public static NbVector3 operator -(NbVector3 a)
        {
            NbVector3 n = new()
            {
                _Value = -a._Value
            };
            return n;
        }
        
        public static NbVector3 operator +(NbVector3 a, NbVector3 b)
        {
            NbVector3 n = new()
            {
                _Value = a._Value + b._Value
            };
            return n;
        }
        
        public static NbVector3 operator -(NbVector3 a, NbVector3 b)
        {
            NbVector3 n = new()
            {
                _Value = a._Value - b._Value
            };
            return n;
        }

        public static NbVector3 operator *(NbVector3 v, float a)
        {
            return new NbVector3()
            {
                _Value = v._Value * a
            };
        }
        
        public static NbVector3 operator *(float a, NbVector3 v)
        {
            return v * a;
        }
        
        public static NbVector3 Lerp(NbVector3 a, NbVector3 b, float blend)
        {
            return new NbVector3()
            {
                _Value = Vector3.Lerp(a._Value, b._Value, blend)
            };
        }

        public NbVector3 Normalized()
        {
            return new NbVector3()
            {
                _Value = Vector3.Normalize(_Value)
            };
        }
        
        public float Length => _Value.Length;
    }
}