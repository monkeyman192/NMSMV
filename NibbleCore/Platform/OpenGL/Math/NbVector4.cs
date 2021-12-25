using OpenTK.Mathematics;

namespace NbCore.Math
{
    public struct NbVector4
    {
        internal Vector4 _Value;

        public NbVector4(Vector4 vec)
        {
            _Value = vec;
        }
        
        public NbVector4(NbVector3 vec, float w)
        {
            _Value = new Vector4(vec._Value, w);
        }
        
        public NbVector4(float x = 0.0f, float y = 0.0f, float z = 0.0f, float w = 0.0f)
        {
            _Value.X = x;
            _Value.Y = y;
            _Value.Z = z;
            _Value.W = w;
        }
        //Methods
        
            
        //Exposed Properties
        public NbVector3 Xyz
        {
            get => new NbVector3()
            {
                _Value = _Value.Xyz
            };

            set =>_Value.Xyz = value._Value;
            
        }

        public float this[int i]
        {
            get => _Value[i];
            set => _Value[i] = value;
        }
        
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
        
        public float W
        {
            get => _Value.W;
            set => _Value.W = value;
        }

        public float Length => _Value.Length;

        public void Normalize()
        {
            _Value.Normalize();
        }
        
        public static NbVector4 operator +(NbVector4 a)
        {
            return a;
        }
        
        public static NbVector4 operator -(NbVector4 a)
        {
            NbVector4 n = new()
            {
                _Value = new Vector4(-a._Value.X,
                    -a._Value.Y,-a._Value.Z,-a._Value.W)
            };
            return n;
        }
        
        public static NbVector4 operator +(NbVector4 a, NbVector4 b)
        {
            NbVector4 n = new()
            {
                _Value = new Vector4(a._Value.X + b._Value.X,
                    a._Value.Y + b._Value.Y,
                    a._Value.Z + b._Value.Z,
                    a._Value.W + b._Value.W)
            };
            return n;
        }

        public static NbVector4 operator *(NbVector4 a, NbMatrix4 mat)
        {
            return new(a._Value * mat._Value);
        }
        
        public static NbVector4 operator *(NbVector4 v, float a)
        {
            return new NbVector4()
            {
                _Value = v._Value * a
            };
        }
        
        public static NbVector4 operator *(float a, NbVector4 v)
        {
            return v * a;
        }
        
        public static float Dot(NbVector4 a, NbVector4 b)
        {
            return Vector4.Dot(a._Value, b._Value);
        }
        
        public static NbVector4 operator -(NbVector4 a, NbVector4 b)
        {
            NbVector4 n = new()
            {
                _Value = new Vector4(a._Value.X - b._Value.X,
                    a._Value.Y - b._Value.Y,
                    a._Value.Z - b._Value.Z,
                    a._Value.W - b._Value.W)
            };
            return n;
        }
    }
}