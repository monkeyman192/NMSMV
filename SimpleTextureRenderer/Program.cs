using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MVCore;
using OpenTK;
using GLSLHelper;
using MVCore.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using ImGuiNET;
using ImGuiHelper;

namespace SimpleTextureRenderer
{
    public class TextureRenderer : OpenTK.Windowing.Desktop.GameWindow
    {
        private Texture _texture;
        private DDSImage _ddsImage;
        private int mipmap_id = 0;
        private int depth_id = 0;
        private GLSLShaderConfig shader_conf;
        private int quad_vao_id;
        ImGuiManager _ImGuiManager;
        
        public TextureRenderer(): base(OpenTK.Windowing.Desktop.GameWindowSettings.Default,
            OpenTK.Windowing.Desktop.NativeWindowSettings.Default)
        {
            Title = "DDS Texture Viewer v1.0";
            VSync = VSyncMode.On;
            RenderFrequency = 60;
            _ImGuiManager = new(this);
        }
        
        public void compileShader(GLSLShaderConfig config)
        {
            if (config.ProgramID != -1)
                GL.DeleteProgram(config.ProgramID);

            GLShaderHelper.CreateShaders(config);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            _ImGuiManager.Resize(ClientSize.X, ClientSize.Y);
            base.OnResize(e);
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            Callbacks.SetDefaultCallbacks();

            //Initialize Engine
            Engine e = new Engine(this);
            RenderState.engineRef = e;
            e.init(ClientSize.X, ClientSize.Y);

            GL.ClearColor(0.1f, 0.2f, 0.5f, 0.0f);
            //GL.Enable(EnableCap.DepthTest);

            //Setup Texture
            //string texturepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\PLANETS\\BIOMES\\WEIRD\\BEAMSTONE\\BEAMGRADIENT.DDS";
            //string texturepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\PLANETS\\BIOMES\\WEIRD\\BEAMSTONE\\SCROLLINGCLOUD.DDS";
            //string texturepath = "E:\\SSD_SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\COMMON\\ROBOTS\\QUADRUPED.DDS";
            string texturepath = "D:\\Downloads\\TILEMAP.DDS";
            //string texturepath = "D:\\Downloads\\TILEMAP.HSV.DDS";
            //string texturepath = "D:\\Downloads\\TILEMAP.NORMAL.DDS";
            _texture = new Texture(texturepath, true);
            
            //Compile Necessary Shaders

            string vs_path = "Shaders/Gbuffer_VS.glsl";
            vs_path = Path.GetFullPath(vs_path);
            vs_path = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, vs_path);

            string fs_path = "Shaders/texture_shader_fs.glsl";
            fs_path = Path.GetFullPath(fs_path);
            fs_path = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, fs_path);

            GLSLShaderSource vs = new GLSLShaderSource(vs_path, true);
            GLSLShaderSource fs = new GLSLShaderSource(fs_path, true);

            //Pass Shader
            shader_conf = GLShaderHelper.compileShader(vs, fs, null, null, null,
                new(), new(), SHADER_TYPE.MATERIAL_SHADER, SHADER_MODE.DEFAULT);

            compileShader(shader_conf);

            //Generate Geometry

            //Default render quad
            MVCore.Primitives.Quad q = new MVCore.Primitives.Quad();
            quad_vao_id = q.getVAO().vao_id;
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            
            base.OnRenderFrame(e);
            //Update Imgui
            _ImGuiManager.Update(e.Time);

            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //GL.ClearColor(0.1f, 0.2f, 0.5f, 0.0f);
            GL.ClearColor(1.0f, 1.0f, 0.0f, 0.0f);
            
            GL.UseProgram(shader_conf.ProgramID);
            //Console.WriteLine("1" + GL.GetError());

            GL.BindVertexArray(quad_vao_id);
            //Console.WriteLine("2" + GL.GetError());

            //Upload texture
            GL.Uniform1(shader_conf.uniformLocations["InTex"], 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, _texture.texID);

            //Upload Uniforms
            GL.Uniform1(shader_conf.uniformLocations["texture_depth"], (float)depth_id);
            GL.Uniform1(shader_conf.uniformLocations["mipmap"], (float) mipmap_id);

            //Console.WriteLine("3" + GL.GetError());

            //Render quad
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (IntPtr)0);
            GL.BindVertexArray(0);


            //Draw UI
            //_ImGuiManager.Render();
            DrawUI();
            //ImGui.ShowDemoWindow();
            _ImGuiManager.Render();

            //Console.WriteLine("4" + GL.GetError());

            SwapBuffers();
        }

        private void DrawUI()
        {
            if (ImGui.Begin("Texture Properties"))
            {
                ImGui.Text("Info:");
                ImGui.Columns(2);
                ImGui.Text("Width");
                ImGui.Text("Height");
                ImGui.Text("Depth");
                ImGui.Text("MipMapCount");
                ImGui.Text("Format");
                ImGui.NextColumn();
                ImGui.Text(_texture.Width.ToString());
                ImGui.Text(_texture.Height.ToString());
                ImGui.Text(_texture.Depth.ToString());
                ImGui.Text(_texture.MipMapCount.ToString());

                //Make format output a bit friendlier
                switch (_texture.pif)
                {
                    case InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext:
                        ImGui.Text("DXT5");
                        break;
                    case InternalFormat.CompressedSrgbAlphaS3tcDxt1Ext:
                        ImGui.Text("DXT1");
                        break;
                    case InternalFormat.CompressedRgRgtc2:
                        ImGui.Text("ATI2A2XY");
                        break;
                    case InternalFormat.CompressedSrgbAlphaBptcUnorm:
                        ImGui.Text("BC7 (DX10 Header)");
                        break;
                    default:
                        ImGui.Text("UNKNOWN");
                        break;
                }

                ImGui.NextColumn();
                ImGui.Separator();
                //Prepare depth options
                ImGui.Text("Active Depth:");
                ImGui.NextColumn();
                
                
                string[] opts = new string[_texture.Depth];
                for (int i = 0; i < opts.Length; i++)
                    opts[i] = i.ToString();
                ImGui.Combo("##0", ref depth_id, opts, _texture.Depth, 12);

                ImGui.NextColumn();
                ImGui.Text("Active Mipmap:");

                opts = new string[_texture.MipMapCount];
                for (int i = 0; i < opts.Length; i++)
                    opts[i] = i.ToString();
                
                ImGui.NextColumn();
                ImGui.Combo("##1", ref mipmap_id, opts, _texture.MipMapCount, 12);

                ImGui.Columns(1);
                ImGui.End();
            }
        }

        [STAThread]
        public static void Main()
        {
            using (TextureRenderer tx = new TextureRenderer())
            {
                tx.Run();
            }
            
            Console.WriteLine("All Good");
        }
    }
}
