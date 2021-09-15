using OpenTK.Mathematics;

namespace MVCore
{
    public class Uniform
    {
        public string Name;
        public Vector4 Values;
        public int ShaderLocation = -1;
        
        public Uniform()
        {
            Values = new Vector4(0.0f);
        }

        public Uniform(string name)
        {
            Name = name;
            Values = new Vector4(0.0f);
        }
    }

}