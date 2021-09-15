using System;
using MVCore;
using OpenTK;
using GLSLHelper;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;

namespace SimpleTextureRenderer
{
    public class TextureRenderer : OpenTK.Windowing.Desktop.GameWindow
    {
        private int texture_id;
        private GLSLShaderConfig shader_conf;
        private int quad_vao_id;

        public TextureRenderer(): base(OpenTK.Windowing.Desktop.GameWindowSettings.Default,
            OpenTK.Windowing.Desktop.NativeWindowSettings.Default)
        {
            VSync = VSyncMode.On;
            RenderFrequency = 60;
        }

        public void compileShader(GLSLShaderConfig config)
        {
            if (config.ProgramID != -1)
                GL.DeleteProgram(config.ProgramID);

            GLShaderHelper.CreateShaders(config);
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            MVCore.Common.Callbacks.SetDefaultCallbacks();

            //Initialize Engine
            Engine e = new Engine(this);
            MVCore.Common.RenderState.engineRef = e;
            e.init(ClientSize.X, ClientSize.Y);

            GL.ClearColor(0.1f, 0.2f, 0.5f, 0.0f);
            //GL.Enable(EnableCap.DepthTest);

            //Setup Texture
            //string texturepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\PLANETS\\BIOMES\\WEIRD\\BEAMSTONE\\BEAMGRADIENT.DDS";
            //string texturepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\PLANETS\\BIOMES\\WEIRD\\BEAMSTONE\\SCROLLINGCLOUD.DDS";
            string texturepath = "E:\\SSD_SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\COMMON\\ROBOTS\\QUADRUPED.DDS";
            Texture tex = new Texture(texturepath, true);

            texture_id = tex.texID;


            string log = "";
            //Compile Necessary Shaders

            string vs_path = "Shaders/Gbuffer_VS.glsl";
            vs_path = System.IO.Path.GetFullPath(vs_path);
            vs_path = System.IO.Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, vs_path);

            string fs_path = "Shaders/PassThrough_FS.glsl";
            fs_path = System.IO.Path.GetFullPath(fs_path);
            fs_path = System.IO.Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, fs_path);

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
            GL.BindTexture(TextureTarget.Texture2DArray, texture_id);

            //Console.WriteLine("3" + GL.GetError());

            //Render quad
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (IntPtr)0);
            GL.BindVertexArray(0);

            //Console.WriteLine("4" + GL.GetError());

            SwapBuffers();
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
