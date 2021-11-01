using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Drawing;
using NbCore.Common;
using System.Linq;

namespace NbCore
{
    public class Sampler
    {
        public string Name = "";
        public string Map = "";
        public Texture Tex = null;
        public bool IsCube = false;
        public bool IsSRGB = true;
        public bool UseCompression = false;
        public bool UseMipMaps = false;
        public TextureUnit texUnit;
        public int SamplerID; // Shader sampler ID
        public int ShaderLocation = -1;
        public bool isProcGen = false; //TODO : to be removed once we are done with the stupid proc gen texture parsing

        //Override Properties
        public Sampler()
        {
            
        }

        public Sampler Clone()
        {
            Sampler newsampler = new()
            {
                Name = Name,
                Map = Map,
                IsSRGB = IsSRGB,
                IsCube = IsCube,
                UseCompression = UseCompression,
                UseMipMaps = UseMipMaps,
                Tex = Tex,
                texUnit = texUnit
            };

            return newsampler;
        }
        
        public static int generate2DTexture(PixelInternalFormat fmt, int w, int h, PixelFormat pix_fmt, PixelType pix_type, int mipmap_count)
        {
            int tex_id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex_id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, fmt, w, h, 0, pix_fmt, pix_type, IntPtr.Zero);
            return tex_id;
        }

        public static int generateTexture2DArray(PixelInternalFormat fmt, int w, int h, int d, PixelFormat pix_fmt, PixelType pix_type, int mipmap_count)
        {
            int tex_id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, tex_id);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, fmt, w, h, d, 0, pix_fmt, pix_type, IntPtr.Zero);
            return tex_id;
        }

        public static void generateTexture2DMipmaps(int texture)
        {
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }

        public static void generateTexture2DArrayMipmaps(int texture)
        {
            GL.BindTexture(TextureTarget.Texture2DArray, texture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
        }

        public static void setupTextureParameters(TextureTarget texTarget, int texture, int wrapMode, int magFilter, int minFilter, float af_amount)
        {

            GL.BindTexture(texTarget, texture);
            GL.TexParameter(texTarget, TextureParameterName.TextureWrapS, wrapMode);
            GL.TexParameter(texTarget, TextureParameterName.TextureWrapT, wrapMode);
            GL.TexParameter(texTarget, TextureParameterName.TextureMagFilter, magFilter);
            GL.TexParameter(texTarget, TextureParameterName.TextureMinFilter, minFilter);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4.0f);

            //Use anisotropic filtering
            af_amount = Math.Max(af_amount, GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy));
            GL.TexParameter(texTarget, (TextureParameterName)0x84FE, af_amount);
        }
        
    }
}