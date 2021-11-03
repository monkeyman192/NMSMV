using System.Xml;
using OpenTK.Mathematics;

namespace NbCore.Math
{
    public class NbQuaternion
    {
        internal Quaternion _Value;

        public NbQuaternion()
        {
            
        }
        
        public NbQuaternion(NbQuaternion q)
        {
            _Value = q._Value;
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
        
        public float W
        {
            get => _Value.W;
            set => _Value.W = value;
        }

        public NbQuaternion Conjugate()
        {
            NbQuaternion nq = new NbQuaternion(this);
            nq.Conjugate();
            return nq;
        }

        public static NbQuaternion FromEulerAngles(float x, float y, float z)
        {
            NbQuaternion n = new();
            n._Value = Quaternion.FromEulerAngles(x, y, z);
            return n;
        }
        
        public static void ToEulerAngles(NbQuaternion q, out NbVector3 v)
        {
            Quaternion.ToEulerAngles(q._Value, out var vt);
            v = new NbVector3(vt);
        }

        public static NbQuaternion Slerp(NbQuaternion a, NbQuaternion b, float c)
        {
            return new NbQuaternion()
            {
                _Value = Quaternion.Slerp(a._Value, b._Value, c)
            };
        }
        
    }
}