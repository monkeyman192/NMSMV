using System.Security.Claims;
using OpenTK.Mathematics;

namespace NbCore.Math
{
    public class NbMatrix4
    {
        internal Matrix4 _Value;

        public NbMatrix4()
        {
            _Value = new();
        }

        public NbMatrix4(NbVector4 v1, NbVector4 v2, NbVector4 v3, NbVector4 v4)
        {
            _Value = new Matrix4(v1._Value, v2._Value, v3._Value, v4._Value);
        }
        

        public static NbMatrix4 Identity()
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.Identity
            };
        }
        
        public static NbMatrix4 LookAt(NbVector3 eye, NbVector3 target, NbVector3 up)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.LookAt(eye._Value, target._Value, up._Value)
            };
        }

        public static NbMatrix4 CreatePerspectiveFieldOfView(float fovy, float aspect, 
            float depthNear, float depthFar)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.CreatePerspectiveFieldOfView(fovy, aspect, depthNear, depthFar) 
            };
        }
        
        public static NbMatrix4 CreateOrthographic(float width, float height, 
            float depthNear, float depthFar)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.CreateOrthographic(width, height, depthNear, depthFar) 
            };
        }

        public static NbMatrix4 CreateTranslation(float x, float y, float z)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.CreateTranslation(x, y, z)
            };
        }
        public static NbMatrix4 CreateTranslation(NbVector3 v)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.CreateTranslation(v._Value)
            };
        }
        
        public static NbMatrix4 CreateScale(NbVector3 v)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.CreateScale(v._Value)
            };
        }
        
        public static NbMatrix4 CreateScale(float s)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.CreateScale(s)
            };
        }
        
        public static NbMatrix4 CreateFromQuaternion(NbQuaternion q)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.CreateFromQuaternion(q._Value)
            };
        }
        
        public static NbMatrix4 CreateRotationX(float r)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.CreateRotationX(r)
            };
        } 
        
        public static NbMatrix4 CreateRotationY(float r)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.CreateRotationY(r)
            };
        } 
        
        public static NbMatrix4 CreateRotationZ(float r)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.CreateRotationZ(r)
            };
        } 

        public NbMatrix4 Inverted()
        {
            return new NbMatrix4()
            {
                _Value = _Value.Inverted()
            };
        }

        public static NbMatrix4 Transpose(NbMatrix4 mat)
        {
            return new NbMatrix4()
            {
                _Value = Matrix4.Transpose(mat._Value)
            };
        }

        public void Transpose()
        {
            _Value.Transpose();
        }
        
        public NbVector4 Column0 => new NbVector4(_Value.Column0);
        public NbVector4 Column1=> new NbVector4(_Value.Column1);
        public NbVector4 Column2=> new NbVector4(_Value.Column2);
        public NbVector4 Column3=> new NbVector4(_Value.Column3);
        
        public NbVector4 Row0 => new NbVector4(_Value.Row0);
        public NbVector4 Row1=> new NbVector4(_Value.Row1);
        public NbVector4 Row2=> new NbVector4(_Value.Row2);
        public NbVector4 Row3=> new NbVector4(_Value.Row3);

        public float M11
        {
            get => _Value.M11;
            set => _Value.M11 = value;
        }

        public float M12
        {
            get => _Value.M12;
            set => _Value.M12 = value;
        }

        public float M13
        {
            get => _Value.M13;
            set => _Value.M13 = value;
        }
        
        public float M14
        {
            get => _Value.M14;
            set => _Value.M14 = value;
        }
        
        public float M21
        {
            get => _Value.M21;
            set => _Value.M21 = value;
        }
        
        public float M22
        {
            get => _Value.M22;
            set => _Value.M22 = value;
        }
        
        public float M23
        {
            get => _Value.M23;
            set => _Value.M23 = value;
        }
        
        public float M24
        {
            get => _Value.M24;
            set => _Value.M24 = value;
        }
        
        public float M31
        {
            get => _Value.M31;
            set => _Value.M31 = value;
        }
        
        public float M32
        {
            get => _Value.M32;
            set => _Value.M32 = value;
        }
        
        public float M33
        {
            get => _Value.M33;
            set => _Value.M33 = value;
        }
        
        public float M34
        {
            get => _Value.M34;
            set => _Value.M34 = value;
        }
        
        public float M41
        {
            get => _Value.M41;
            set => _Value.M41 = value;
        }
        
        public float M42
        {
            get => _Value.M42;
            set => _Value.M42 = value;
        }
        
        public float M43
        {
            get => _Value.M43;
            set => _Value.M43 = value;
        }
        
        public float M44
        {
            get => _Value.M44;
            set => _Value.M44 = value;
        }

        public float this[int k1, int k2]
        {
            get => _Value[k1, k2];
            set => _Value[k1, k2] = value;
        }

        public static NbMatrix4 operator *(NbMatrix4 a, NbMatrix4 b)
        {
            return new NbMatrix4()
            {
                _Value = a._Value * b._Value
            };
        }
        
        public static NbMatrix4 operator +(NbMatrix4 a, NbMatrix4 b)
        {
            return new NbMatrix4()
            {
                _Value = a._Value + b._Value
            };
        }
        
        public static NbMatrix4 operator -(NbMatrix4 a, NbMatrix4 b)
        {
            return new NbMatrix4()
            {
                _Value = a._Value - b._Value
            };
        }
        
        
    }
    
    
}