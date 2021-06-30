using System;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using MVCore.Utils;
using System.IO;
using KUtility;
using MVCore.Common;

namespace MVCore.GMDL
{
    public class Texture : IDisposable
    {
        private bool disposed = false;
        public int texID = -1;
        public int pboID = -1;
        public TextureTarget target;
        public string name;
        public int width;
        public int height;
        public InternalFormat pif;
        public PaletteOpt palOpt;
        public Vector4 procColor;
        public Vector3 avgColor;

        //Empty Initializer
        public Texture() { }
        //Path Initializer
        public Texture(string path, bool isCustom = false)
        {
            Stream fs;
            byte[] image_data;
            int data_length;
            try
            {
                if (!isCustom)
                {
                    try
                    {
                        fs = NMSUtils.LoadNMSFileStream(path, ref Common.RenderState.activeResMgr);
                    }
                    catch (FileNotFoundException)
                    {
                        //FileNotFoundExceptions during texture loading, are caught so that default textures are loaded
                        fs = null;
                    }
                }

                else
                    fs = new FileStream(path, FileMode.Open);

                if (fs == null)
                {
                    //throw new System.IO.FileNotFoundException();
                    Console.WriteLine("Texture {0} Missing. Using default.dds", path);

                    //Load default.dds from resources
                    image_data = File.ReadAllBytes("default.dds");
                    data_length = image_data.Length;
                }
                else
                {
                    data_length = (int)fs.Length;
                    image_data = new byte[data_length];
                }

                fs.Read(image_data, 0, data_length);

            }
            catch (FileNotFoundException)
            {
                //Fallback to the default.dds
                image_data = (byte[]) AppResourceManager.getEmbeddedResource("default_tex");
            }

            textureInit(image_data, path);
        }

        public void textureInit(byte[] imageData, string _name)
        {
            DDSImage ddsImage;
            name = _name;

            ddsImage = new DDSImage(imageData);
            RenderStats.texturesNum += 1; //Accumulate settings

            Console.WriteLine("Sampler Name Path " + name + " Width {0} Height {1}", ddsImage.header.dwWidth, ddsImage.header.dwHeight);
            width = ddsImage.header.dwWidth;
            height = ddsImage.header.dwHeight;
            int blocksize = 16;
            switch (ddsImage.header.ddspf.dwFourCC)
            {
                //DXT1
                case (0x31545844):
                    pif = InternalFormat.CompressedSrgbAlphaS3tcDxt1Ext;
                    blocksize = 8;
                    break;
                case (0x35545844):
                    pif = InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext;
                    break;
                case (0x32495441): //ATI2A2XY
                    pif = InternalFormat.CompressedRgRgtc2; //Normal maps are probably never srgb
                    break;
                //DXT10 HEADER
                case (0x30315844):
                    {
                        switch (ddsImage.header10.dxgiFormat)
                        {
                            case (DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM):
                                pif = InternalFormat.CompressedSrgbAlphaBptcUnorm;
                                break;
                            default:
                                throw new ApplicationException("Unimplemented DX10 Texture Pixel format");
                        }
                        break;
                    }
                default:
                    throw new ApplicationException("Unimplemented Pixel format");
            }

            //Temp Variables
            int w = width;
            int h = height;
            int mm_count = Math.Max(1, ddsImage.header.dwMipMapCount); //Fix the counter to 1 to handle textures with single mipmaps
            int depth_count = Math.Max(1, ddsImage.header.dwDepth); //Fix the counter to 1 to fit the texture in a 3D container
            int temp_size = ddsImage.header.dwPitchOrLinearSize;

            //Generate PBO
            pboID = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboID);
            GL.BufferData(BufferTarget.PixelUnpackBuffer, ddsImage.bdata.Length, ddsImage.bdata, BufferUsageHint.StaticDraw);
            //GL.BufferSubData(BufferTarget.PixelUnpackBuffer, IntPtr.Zero, ddsImage.bdata.Length, ddsImage.bdata);

            //Upload to GPU
            texID = GL.GenTexture();
            target = TextureTarget.Texture2DArray;

            GL.BindTexture(target, texID);
            //When manually loading mipmaps, levels should be loaded first
            GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0);
            //GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mm_count - 1);

            int offset = 0;
            for (int i = 0; i < mm_count; i++)
            {
                GL.CompressedTexImage3D(target, i, pif, w, h, depth_count, 0, temp_size * depth_count, IntPtr.Zero + offset);
                offset += temp_size * depth_count;

                w = Math.Max(w >> 1, 1);
                h = Math.Max(h >> 1, 1);

                temp_size = Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * blocksize;
                //This works only for square textures
                //temp_size = Math.Max(temp_size/4, blocksize);
            }

            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureLodBias, -0.2f);
            GL.TexParameter(target, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(target, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(target, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(target, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            //Console.WriteLine(GL.GetError());

            //Use anisotropic filtering
            float af_amount = GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy);
            af_amount = (float)Math.Max(af_amount, 4.0f);
            //GL.TexParameter(TextureTarget.Texture2D,  (TextureParameterName) 0x84FE, af_amount);
            GL.GetTexParameter(target, GetTextureParameter.TextureMaxLevel, out int max_level);
            GL.GetTexParameter(target, GetTextureParameter.TextureBaseLevel, out int base_level);

            int maxsize = Math.Max(height, width);
            int p = (int)Math.Floor(Math.Log(maxsize, 2)) + base_level;
            int q = Math.Min(p, max_level);

#if (DEBUGNONO)
            //Get all mipmaps
            temp_size = ddsImage.header.dwPitchOrLinearSize;
            for (int i = 0; i < q; i++)
            {
                //Get lowest calculated mipmap
                byte[] pixels = new byte[temp_size];
                
                //Save to disk
                GL.GetCompressedTexImage(TextureTarget.Texture2D, i, pixels);
                File.WriteAllBytes("Temp\\level" + i.ToString(), pixels);
                temp_size = Math.Max(temp_size / 4, 16);
            }
#endif

#if (DUMP_TEXTURESNONO)
            Sampler.dump_texture(name.Split('\\').Last().Split('/').Last(), width, height);
#endif
            //avgColor = getAvgColor(pixels);
            ddsImage = null;
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0); //Unbind texture PBO
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //Free other resources here
                if (texID != -1) GL.DeleteTexture(texID);
                GL.DeleteBuffer(pboID);

            }

            //Free unmanaged resources
            disposed = true;
        }

        private Vector3 getAvgColor(byte[] pixels)
        {
            //Assume that I have the 4x4 mipmap
            //I need to fetch the first 2 colors and calculate the Average

            MemoryStream ms = new MemoryStream(pixels);
            BinaryReader br = new BinaryReader(ms);

            int color0 = br.ReadUInt16();
            int color1 = br.ReadUInt16();

            br.Close();

            //int rmask = 0x1F << 11;
            //int gmask = 0x3F << 5;
            //int bmask = 0x1F;
            uint temp;

            temp = (uint)(color0 >> 11) * 255 + 16;
            char r0 = (char)((temp / 32 + temp) / 32);
            temp = (uint)((color0 & 0x07E0) >> 5) * 255 + 32;
            char g0 = (char)((temp / 64 + temp) / 64);
            temp = (uint)(color0 & 0x001F) * 255 + 16;
            char b0 = (char)((temp / 32 + temp) / 32);

            temp = (uint)(color1 >> 11) * 255 + 16;
            char r1 = (char)((temp / 32 + temp) / 32);
            temp = (uint)((color1 & 0x07E0) >> 5) * 255 + 32;
            char g1 = (char)((temp / 64 + temp) / 64);
            temp = (uint)(color1 & 0x001F) * 255 + 16;
            char b1 = (char)((temp / 32 + temp) / 32);

            char red = (char)(((int)(r0 + r1)) / 2);
            char green = (char)(((int)(g0 + g1)) / 2);
            char blue = (char)(((int)(b0 + b1)) / 2);


            return new Vector3(red / 256.0f, green / 256.0f, blue / 256.0f);

        }

        private ulong PackRGBA(char r, char g, char b, char a)
        {
            return (ulong)((r << 24) | (g << 16) | (b << 8) | a);
        }

        // void DecompressBlockDXT1(): Decompresses one block of a DXT1 texture and stores the resulting pixels at the appropriate offset in 'image'.
        //
        // unsigned long x:						x-coordinate of the first pixel in the block.
        // unsigned long y:						y-coordinate of the first pixel in the block.
        // unsigned long width: 				width of the texture being decompressed.
        // unsigned long height:				height of the texture being decompressed.
        // const unsigned char *blockStorage:	pointer to the block to decompress.
        // unsigned long *image:				pointer to image where the decompressed pixel data should be stored.

        private void DecompressBlockDXT1(ulong x, ulong y, ulong width, byte[] blockStorage, byte[] image)
        {

        }

    }

}
