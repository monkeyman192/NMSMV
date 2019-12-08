using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using System.Diagnostics;
using System.IO;
using System.Reflection;

//Custom Imports
using MVCore;
using MVCore.Common;
using MVCore.GMDL;
using GLSLHelper;
using OpenTK.Graphics;
using ClearBufferMask = OpenTK.Graphics.OpenGL.ClearBufferMask;
using CullFaceMode = OpenTK.Graphics.OpenGL.CullFaceMode;
using EnableCap = OpenTK.Graphics.OpenGL4.EnableCap;
using PolygonMode = OpenTK.Graphics.OpenGL4.PolygonMode;
using GL = OpenTK.Graphics.OpenGL4.GL;
using System.ComponentModel;
using System.Threading;
using QuickFont;
using ProjProperties = WPFModelViewer.Properties;

namespace Model_Viewer
{
    public class CGLControl : GLControl
    {
        public model rootObject;

        //Common Transforms
        //private Matrix4 rotMat, mvp;

        private Vector3 rot = new Vector3(0.0f, 0.0f, 0.0f);
        //private Camera activeCam;

        //Use public variables for now because getters/setters are so not worth it for our purpose
        public float light_angle_y = 0.0f;
        public float light_angle_x = 0.0f;
        public float light_distance = 5.0f;
        public float light_intensity = 1.0f;
        public float scale = 1.0f;
        
        //Mouse Pos
        private int mouse_x;
        private int mouse_y;
        //Camera Movement Speed
        public int movement_speed = 1;

        //Control Identifier
        private int index;
        
        //Custom Palette
        private Dictionary<string,Dictionary<string,Vector4>> palette;

        //Animation Stuff
        private bool animationStatus = false;
        

        public bool PAnimationStatus
        {
            get
            {
                return animationStatus;
            }

            set
            {
                animationStatus = value;
            }
        }

        public List<model> animScenes = new List<model>();
        public List<Tuple<AnimComponent, AnimData>> activeAnimScenes = new List<Tuple<AnimComponent, AnimData>>();

        //Control private Managers
        public ResourceManager resMgr = new ResourceManager();
        public renderManager renderMgr = new renderManager();
        
        //Init-GUI Related
        private ContextMenuStrip contextMenuStrip1;
        private System.ComponentModel.IContainer components;
        private ToolStripMenuItem exportToObjToolStripMenuItem;
        private ToolStripMenuItem loadAnimationToolStripMenuItem;
        private OpenFileDialog openFileDialog1;
        private Form pform;

        //Timers
        public System.Timers.Timer inputPollTimer;
        public System.Timers.Timer resizeTimer;

        //Private fps Counter
        private int frames = 0;
        private double dt = 0.0f;
        private DateTime oldtime;
        private DateTime prevtime;

        //Gamepad Setup
        public GamepadHandler gpHandler;
        public KeyboardHandler kbHandler;
        private bool disposed;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        //Rendering Thread Stuff
        private Thread rendering_thread;
        private Queue<ThreadRequest> rt_req_queue = new Queue<ThreadRequest>();
        private bool rt_exit;
        
        private void registerFunctions()
        {
            this.Load += new System.EventHandler(genericLoad);
            //this.Paint += new System.Windows.Forms.PaintEventHandler(this.genericPaint);
            this.Resize += new System.EventHandler(OnResize); 
            this.MouseHover += new System.EventHandler(genericHover);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(genericMouseMove);
            this.MouseClick += new System.Windows.Forms.MouseEventHandler(CGLControl_MouseClick);
            //this.glControl1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.glControl1_Scroll);
            this.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(generic_KeyDown);
            this.Enter += new System.EventHandler(genericEnter);
            this.Leave += new System.EventHandler(genericLeave);
        }

        //Default Constructor
        public CGLControl(): base(new GraphicsMode(32, 24, 0, 8))
        {
            registerFunctions();

            //Default Setup
            this.rot.Y = 131;
            this.light_angle_y = 190;

            //Input Polling Timer
            inputPollTimer = new System.Timers.Timer();
            inputPollTimer.Elapsed += new System.Timers.ElapsedEventHandler(input_poller);
            inputPollTimer.Interval = 10;
            
            //Resize Timer
            resizeTimer = new System.Timers.Timer();
            resizeTimer.Elapsed += new System.Timers.ElapsedEventHandler(ResizeControl);
            resizeTimer.Interval = 10;

            //Set properties
            DoubleBuffered = true;
            VSync = false;
        }

        //Constructor
        public CGLControl(int index, Form parent)
        {
            registerFunctions();
            
            //Set Control Identifiers
            this.index = index;
            
            //Default Setup
            this.rot.Y = 131;
            this.light_angle_y = 190;

            //Assign new palette to GLControl
            palette = Model_Viewer.Palettes.createPalettefromBasePalettes();

            //Set parent form
            if (parent != null)
                pform = parent;

            //Control Timer
            inputPollTimer = new System.Timers.Timer();
            inputPollTimer.Elapsed += new System.Timers.ElapsedEventHandler(input_poller);
            inputPollTimer.Interval = 10;
            inputPollTimer.Start();
        }

        private void input_poller(object sender, System.Timers.ElapsedEventArgs e)
        {
            //Console.WriteLine(gpHandler.getAxsState(0, 0).ToString() + " " +  gpHandler.getAxsState(0, 1).ToString());
            //gpHandler.reportButtons();
            //gamepadController(); //Move camera according to input
            bool focused = false;

            this.Invoke((MethodInvoker) delegate
            {
                focused = this.Focused;
            });

            if (focused)
                keyboardController(); //Move camera according to input
        }

        private void rt_render()
        {
            //Update per frame data
            frameUpdate();
            renderMgr.render(); //Render Everything
        }
        
        public void findAnimScenes(model node)
        {
            if (node.animComponentID >= 0)
                animScenes.Add(node);

            foreach (model child in node.children)
            {
                findAnimScenes(child);
            }

        }

        //Per Frame Updates
        private void frameUpdate()
        {
            //Cleanup instances
            renderMgr.clearInstances();

            //Set time to the renderManager
            renderMgr.progressTime(dt);
            
            //Update Scene
            rootObject?.update();

            //Save new updated joint transforms to the corresponding AnimCompoent Structures
            for (int i = 0; i < animScenes.Count; i++)
            {
                model animScene = animScenes[i];
                AnimComponent ac = animScene._components[animScene.hasComponent(typeof(AnimComponent))] as AnimComponent;
                ac.update();
            }

            //Progress animations
            if (MVCore.Common.RenderOptions.ToggleAnimations)
            {
                bool found_first_active_anim = false;
                //Update active animations
                foreach (model anim_scene in animScenes)
                {
                    AnimComponent ac = anim_scene._components[anim_scene.hasComponent(typeof(AnimComponent))] as AnimComponent;

                    foreach (AnimData ad in ac.Animations)
                    {
                        if (ad._animationToggle)
                        {
                            found_first_active_anim = true;
                            //Load updated local joint transforms
                            foreach (libMBIN.NMS.Toolkit.TkAnimNodeData node in ad.animMeta.NodeData)
                            {
                                if (!ac.jointDict.ContainsKey(node.Node))
                                    continue;

                                Joint tj = ac.jointDict[node.Node];
                                ad.applyNodeTransform(tj, node.Node);
                                
                            }

                            //Once the current frame data is fetched, progress to the next frame
                            ad.animate((float) dt);
                        }
                        //TODO: For now I'm just using the first active animation. Blending should be kinda more sophisticated
                        if (found_first_active_anim)
                            break; 
                    }
                    //TODO: For now I'm just using the first active animation. Blending should be kinda more sophisticated
                    if (found_first_active_anim)
                        break;
                }
            }
            
            //Camera & Light Positions
            //Update common transforms
            RenderState.activeCam.aspect = (float) ClientSize.Width / ClientSize.Height;
                
            //Apply extra viewport rotation
            Matrix4 Rotx = Matrix4.CreateRotationX(MathUtils.radians(rot[0]));
            Matrix4 Roty = Matrix4.CreateRotationY(MathUtils.radians(rot[1]));
            Matrix4 Rotz = Matrix4.CreateRotationZ(MathUtils.radians(rot[2]));
            RenderState.rotMat = Rotz * Rotx * Roty;
            RenderState.mvp = RenderState.activeCam.viewMat; //Full mvp matrix
            
            resMgr.GLCameras[0].updateViewMatrix();
            resMgr.GLCameras[1].updateViewMatrix();

            //Update Frame Counter
            fps();
        }

        //Main Rendering Routines
        private void ControlLoop()
        {
            //Setup new Context
            IGraphicsContext new_context = new GraphicsContext(new GraphicsMode(32, 24, 0, 8), this.WindowInfo);
            new_context.MakeCurrent(this.WindowInfo);
            this.MakeCurrent(); //This is essential

            //Add default primitives trying to avoid Vao Request queue traffic
            addDefaultLights();
            addDefaultTextures();
            addCamera();
            addCamera(cull:false); //Add second camera
            setActiveCam(0);
            addDefaultPrimitives();
            addTestObjects();

            renderMgr.setupGBuffer(ClientSize.Width, ClientSize.Height);
            
            bool renderFlag = true; //Toggle rendering on/off

            //Rendering Loop
            while (!rt_exit)
            {
                //Check for new scene request
                if (rt_req_queue.Count > 0)
                {
                    ThreadRequest req;
                    lock (rt_req_queue)
                    {
                        //Try to group  Resizing requests
                        req = rt_req_queue.Dequeue();
                    }

                    lock (req)
                    {
                        switch (req.type)
                        {
                            case THREAD_REQUEST_TYPE.QUERY_GLCONTROL_STATUS_REQUEST:
                                //At this point the renderer is up and running
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST:
                                lock (inputPollTimer)
                                {
                                    inputPollTimer.Stop();
                                    rt_addRootScene((string)req.arguments[0]);
                                    req.status = THREAD_REQUEST_STATUS.FINISHED;
                                    inputPollTimer.Start();
                                }
                                break;
                            case THREAD_REQUEST_TYPE.CHANGE_MODEL_PARENT_REQUEST:
                                model source = (model) req.arguments[0];
                                model target = (model) req.arguments[1];


                                System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
                                {
                                    if (source.parent != null)
                                        source.parent.Children.Remove(source);

                                    //Add to target node
                                    source.parent = target;
                                    target.Children.Add(source);
                                }));
                                
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.UPDATE_SCENE_REQUEST:
                                scene req_scn = (scene) req.arguments[0];
                                req_scn.update();
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.RESIZE_REQUEST:
                                rt_ResizeViewport((int)req.arguments[0], (int)req.arguments[1]);
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.MODIFY_SHADER_REQUEST:
                                GLShaderHelper.modifyShader((GLSLShaderConfig) req.arguments[0],
                                             (GLSLShaderText) req.arguments[1]);
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.TERMINATE_REQUEST:
                                rt_exit = true;
                                renderFlag = false;
                                inputPollTimer.Stop();
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.PAUSE_RENDER_REQUEST:
                                renderFlag = false;
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.RESUME_RENDER_REQUEST:
                                renderFlag = true;
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.NULL:
                                break;
                        }
                    }
                }
                
                if (renderFlag)
                {
                    rt_render();
                }

                Thread.Sleep(1);
                SwapBuffers();

            }
        }

        

        #region GLControl Methods
        private void genericEnter(object sender, EventArgs e)
        {
            //Start Timer when the glControl gets focus
            //Debug.WriteLine("Entered Focus Control " + index);
            inputPollTimer.Start();
        }

        private void genericHover(object sender, EventArgs e)
        {
            //Start Timer when the glControl gets focus
            //this.MakeCurrent(); //Control should have been active on hover
            inputPollTimer.Start();
        }

        private void genericLeave(object sender, EventArgs e)
        {
            //Don't update the control when its not focused
            //Debug.WriteLine("Left Focus of Control "+ index);
            inputPollTimer.Stop();

        }

        private void genericPaint(object sender, PaintEventArgs e)
        {
            //TODO: Should I add more stuff in here?
            //SwapBuffers();
        }

        private void genericLoad(object sender, EventArgs e)
        {

            InitializeComponent();
            MakeCurrent();

            //Once the context is initialized compile the shaders
            compileShaders();

            //Initialize the render manager (Does some pretty lame shit for now)
            renderMgr.init(resMgr);
            
            kbHandler = new KeyboardHandler();
            //gpHandler = new GamepadHandler(); TODO: Add support for PS4 controller

            //Everything ready to swap threads
            setupRenderingThread();

            //Start Timers
            inputPollTimer.Start();
        
        }

        private void genericMouseMove(object sender, MouseEventArgs e)
        {
            //Debug.WriteLine("Mouse moving on {0}", this.TabIndex);
            //int delta_x = (int) (Math.Pow(activeCam.fov, 4) * (e.X - mouse_x));
            //int delta_y = (int) (Math.Pow(activeCam.fov, 4) * (e.Y - mouse_y));
            int delta_x = (e.X - mouse_x);
            int delta_y = (e.Y - mouse_y);

            delta_x = Math.Min(Math.Max(delta_x, -10), 10);
            delta_y = Math.Min(Math.Max(delta_y, -10), 10);

            if (e.Button == MouseButtons.Left)
            {
                //Debug.WriteLine("Deltas {0} {1} {2}", delta_x, delta_y, e.Button);
                RenderState.activeCam.AddRotation(delta_x, delta_y);
            }

            mouse_x = e.X;
            mouse_y = e.Y;
            
        }

        private void generic_KeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            //Debug.WriteLine("Key pressed {0}",e.KeyCode);
            switch (e.KeyCode)
            {
                //Light Rotation
                case Keys.N:
                    this.light_angle_y -= 1;
                    break;
                case Keys.M:
                    this.light_angle_y += 1;
                    break;
                case Keys.Oemcomma:
                    this.light_angle_x -= 1;
                    break;
                case Keys.OemPeriod:
                    this.light_angle_x += 1;
                    break;
                /*
                //Toggle Wireframe
                case Keys.I:
                    if (RenderOptions.RENDERMODE == PolygonMode.Fill)
                        RenderOptions.RENDERMODE = PolygonMode.Line;
                    else
                        RenderOptions.RENDERMODE = PolygonMode.Fill;
                    break;
                //Toggle Texture Render
                case Keys.O:
                    RenderOptions.UseTextures = 1.0f - RenderOptions.UseTextures;
                    break;
                //Toggle Collisions Render
                case Keys.OemOpenBrackets:
                    RenderOptions.RenderCollisions = !RenderOptions.RenderCollisions;
                    break;
                //Toggle Debug Render
                case Keys.OemCloseBrackets:
                    RenderOptions.RenderDebug = !RenderOptions.RenderDebug;
                    break;
                */
                //Switch cameras
                case Keys.NumPad0:
                    if (this.resMgr.GLCameras[0].isActive)
                        setActiveCam(1);
                    else
                        setActiveCam(0);
                    break;
                //Animation playback (Play/Pause Mode) with Space
                case Keys.Space:
                    toggleAnimation();
                    break;
                default:
                    //Console.WriteLine("Not Implemented Yet");
                    break;
            }
        }

        private void ResizeControl(object sender, System.Timers.ElapsedEventArgs e)
        {
            resizeTimer.Stop();
            
            //Make new request
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.RESIZE_REQUEST;
            req.arguments.Clear();
            req.arguments.Add(ClientSize.Width);
            req.arguments.Add(ClientSize.Height);

            issueRequest(ref req);
        }

        
        private void OnResize(object sender, EventArgs e)
        {
            //Check the resizeTimer
            resizeTimer.Stop();
            resizeTimer.Start();
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.exportToObjToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadAnimationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportToObjToolStripMenuItem,
            this.loadAnimationToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(181, 70);
            // 
            // exportToObjToolStripMenuItem
            // 
            this.exportToObjToolStripMenuItem.Name = "exportToObjToolStripMenuItem";
            this.exportToObjToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.exportToObjToolStripMenuItem.Text = "Export to obj";
            this.exportToObjToolStripMenuItem.Click += new System.EventHandler(this.exportToObjToolStripMenuItem_Click);
            // 
            // loadAnimationToolStripMenuItem
            // 
            this.loadAnimationToolStripMenuItem.Name = "loadAnimationToolStripMenuItem";
            this.loadAnimationToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.loadAnimationToolStripMenuItem.Text = "Load Animation";
            this.loadAnimationToolStripMenuItem.Click += new System.EventHandler(this.loadAnimationToolStripMenuItem_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // CGLControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.Name = "CGLControl";
            this.Size = new System.Drawing.Size(314, 213);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

               
        private void CGLControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip1.Show(Control.MousePosition);
            }
            //TODO: ADD SELECT OBJECT FUNCTIONALITY IN THE FUTURE
            //else if ((e.Button == MouseButtons.Left) && (ModifierKeys == Keys.Control))
            //{
            //    selectObject(e.Location);
            //}
        }

        #endregion GLControl Methods

        #region ShaderMethods

        public void issuemodifyShaderRequest(GLSLShaderConfig config, GLSLShaderText shaderText)
        {
            Console.WriteLine("Sending Shader Modification Request");
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.MODIFY_SHADER_REQUEST;
            req.arguments.Add(config);
            req.arguments.Add(shaderText);
            
            //Send request
            issueRequest(ref req);
        }

        //GLPreparation
        private void compileShader(GLSLShaderText vs, GLSLShaderText fs, GLSLShaderText gs, GLSLShaderText tes, GLSLShaderText tcs, SHADER_TYPE type, ref string log)
        {
            GLSLShaderConfig shader_conf = new GLSLShaderConfig(vs, fs, gs, tcs, tes, type);
            //Set modify Shader delegate
            shader_conf.modifyShader = issuemodifyShaderRequest;

            compileShader(shader_conf);
            resMgr.GLShaders[type] = shader_conf;
            log += shader_conf.log; //Append log
        }


        private void compileShaders()
        {

#if(DEBUG)
            //Query GL Extensions
            Console.WriteLine("OPENGL AVAILABLE EXTENSIONS:");
            string[] ext = GL.GetString(StringName.Extensions).Split(' ');
            foreach (string s in ext)
            {
                if (s.Contains("explicit"))
                    Console.WriteLine(s);
                if (s.Contains("texture"))
                    Console.WriteLine(s);
                if (s.Contains("16"))
                    Console.WriteLine(s);
            }

            //Query maximum buffer sizes
            Console.WriteLine("MaxUniformBlock Size {0}", GL.GetInteger(GetPName.MaxUniformBlockSize));
#endif

            //Populate shader list
            string log = "";

            //Geometry Shader
            //Compile Object Shaders
            GLSLShaderText geometry_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText geometry_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            GLSLShaderText geometry_shader_gs = new GLSLShaderText(ShaderType.GeometryShader);
            geometry_shader_vs.addStringFromFile("Shaders/Simple_VSEmpty.glsl");
            geometry_shader_fs.addStringFromFile("Shaders/Simple_FSEmpty.glsl");
            geometry_shader_gs.addStringFromFile("Shaders/Simple_GS.glsl");

            compileShader(geometry_shader_vs, geometry_shader_fs, geometry_shader_gs,null,null,
                            GLSLHelper.SHADER_TYPE.DEBUG_MESH_SHADER, ref log);

            //Picking Shaders
            GLSLShaderText picking_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText picking_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            picking_shader_vs.addString(ProjProperties.Resources.pick_vert);
            picking_shader_fs.addString(ProjProperties.Resources.pick_frag);
            compileShader(picking_shader_vs, picking_shader_fs, null, null, null,
                GLSLHelper.SHADER_TYPE.PICKING_SHADER, ref log);


            //Main Object Shader - Deferred Shading
            GLSLShaderText main_deferred_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText main_deferred_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            main_deferred_shader_vs.addString("#define _DEFERRED_RENDERING\n");
            main_deferred_shader_vs.addStringFromFile("Shaders/Simple_VS.glsl");
            main_deferred_shader_fs.addString("#define _DEFERRED_RENDERING\n");
            main_deferred_shader_fs.addStringFromFile("Shaders/Simple_FS.glsl");
            
            compileShader(main_deferred_shader_vs, main_deferred_shader_fs, null, null, null,
                GLSLHelper.SHADER_TYPE.MESH_DEFERRED_SHADER, ref log);

            //Main Object Shader - Deferred Shading
            GLSLShaderText main_forward_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText main_forward_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            main_forward_shader_vs.addStringFromFile("Shaders/Simple_VS.glsl");
            main_forward_shader_fs.addStringFromFile("Shaders/Simple_FS.glsl");

            compileShader(main_forward_shader_vs, main_forward_shader_fs, null, null, null,
                GLSLHelper.SHADER_TYPE.MESH_FORWARD_SHADER, ref log);

            //BoundBox Shader
            GLSLShaderText bbox_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText bbox_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            bbox_shader_vs.addStringFromFile("Shaders/Bound_VS.glsl");
            bbox_shader_fs.addStringFromFile("Shaders/Bound_FS.glsl");
            compileShader(bbox_shader_vs, bbox_shader_fs, null, null, null,
                GLSLHelper.SHADER_TYPE.BBOX_SHADER, ref log);

            //Texture Mixing Shader
            GLSLShaderText texture_mixing_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText texture_mixing_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            texture_mixing_shader_vs.addStringFromFile("Shaders/texture_mixer_VS.glsl");
            texture_mixing_shader_fs.addStringFromFile("Shaders/texture_mixer_FS.glsl");
            compileShader(texture_mixing_shader_vs, texture_mixing_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER, ref log);

            //GBuffer Shaders
            GLSLShaderText gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/Gbuffer_FS.glsl");
            compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.GBUFFER_SHADER, ref log);

            //Decal Shaders
            GLSLShaderText decal_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText decal_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            decal_shader_vs.addStringFromFile("Shaders/decal_VS.glsl");
            decal_shader_fs.addStringFromFile("Shaders/Decal_FS.glsl");
            compileShader(decal_shader_vs, decal_shader_fs, null,null,null,
                            GLSLHelper.SHADER_TYPE.DECAL_SHADER, ref log);

            //Locator Shaders
            GLSLShaderText locator_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText locator_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            locator_shader_vs.addString(ProjProperties.Resources.locator_vert);
            locator_shader_fs.addString(ProjProperties.Resources.locator_frag);
            compileShader(locator_shader_vs, locator_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.LOCATOR_SHADER, ref log);

            //Joint Shaders
            GLSLShaderText joint_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText joint_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            joint_shader_vs.addString(ProjProperties.Resources.joint_vert);
            joint_shader_fs.addString(ProjProperties.Resources.joint_frag);
            compileShader(joint_shader_vs, joint_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.JOINT_SHADER, ref log);

            //Text Shaders
            GLSLShaderText text_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText text_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            text_shader_vs.addString(ProjProperties.Resources.text_vert);
            text_shader_fs.addString(ProjProperties.Resources.text_frag);
            compileShader(text_shader_vs, text_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.TEXT_SHADER, ref log);

            //Light Shaders
            GLSLShaderText light_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText light_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            light_shader_vs.addString(ProjProperties.Resources.text_vert);
            light_shader_fs.addString(ProjProperties.Resources.text_frag);
            compileShader(light_shader_vs, light_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.LIGHT_SHADER, ref log);

            //Camera Shaders
            GLSLShaderText camera_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText camera_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            camera_shader_vs.addString(ProjProperties.Resources.camera_vert);
            camera_shader_fs.addString(ProjProperties.Resources.camera_frag);
            compileShader(camera_shader_vs, camera_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.CAMERA_SHADER, ref log);

            //FILTERS - EFFECTS

            //Pass Shader
            GLSLShaderText passthrough_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            passthrough_shader_fs.addStringFromFile("Shaders/PassThrough_FS.glsl");
            compileShader(gbuffer_shader_vs, passthrough_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.PASSTHROUGH_SHADER, ref log);

            //Gaussian Blur Shaders
            GLSLShaderText gaussian_blur_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            passthrough_shader_fs.addStringFromFile("Shaders/gaussian_blur_FS.glsl");
            compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.GAUSSIAN_BLUR_SHADER, ref log);
        
        }

        public void compileShader(GLSLShaderConfig config)
        {
            if (config.program_id != -1)
                GL.DeleteProgram(config.program_id);

            GLShaderHelper.CreateShaders(config);
        }

        
        #endregion ShaderMethods

        #region ContextMethods

        private void exportToObjToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Exporting to obj");
            SaveFileDialog sv = new SaveFileDialog();
            sv.Filter = "OBJ Files | *.obj";
            sv.DefaultExt = "obj";
            DialogResult res = sv.ShowDialog();

            if (res != DialogResult.OK)
                return;

            StreamWriter obj = new StreamWriter(sv.FileName);

            obj.WriteLine("# No Mans Model Viewer OBJ File:");
            obj.WriteLine("# www.3dgamedevblog.com");

            //Iterate in objects
            uint index = 1;
            findGeoms(rootObject, obj, ref index);
            
            obj.Close();
            
        }

        private void findGeoms(model m, StreamWriter s, ref uint index)
        {
            if (m.type == TYPES.MESH || m.type==TYPES.COLLISION)
            {
                //Get converted text
                meshModel me = (meshModel) m;
                me.writeGeomToStream(s, ref index);

            }
            foreach (model c in m.children)
                if (c.renderable) findGeoms(c, s, ref index);
        }

        private void loadAnimationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AnimationSelectForm aform = new AnimationSelectForm(animScenes);
            aform.Show();
        }

        #endregion ContextMethods

        #region ControlSetup_Init

        //Setup
        
        public void setupRenderingThread()
        {
            
            //Setup rendering thread
            Context.MakeCurrent(null);
            rendering_thread = new Thread(ControlLoop);
            rendering_thread.IsBackground = true;
            rendering_thread.Priority = ThreadPriority.AboveNormal;

            //Start RT Thread
            rendering_thread.Start();
        }

        #endregion ControlSetup_Init

        #region Camera Update Functions
        public void setActiveCam(int index)
        {
            if (RenderState.activeCam != null)
                RenderState.activeCam.isActive = false;
            RenderState.activeCam = resMgr.GLCameras[index];
            RenderState.activeCam.isActive = true;
            Console.WriteLine("Switching Camera to {0}", index);
        }

        public void updateActiveCam(int FOV, float zNear, float zFar)
        {
            //TODO: REMOVE, FOR TESTING I"M WORKING ONLY ON THE FIRST CAM
            resMgr.GLCameras[0].setFOV(FOV);
            resMgr.GLCameras[0].zFar = zFar;
            resMgr.GLCameras[0].zNear = zNear;
        }

        public void updateActiveCam(Vector3 pos)
        {
            RenderState.activeCam.Position = pos;
        }

        #endregion

        public void updateControlRotation(float rx, float ry)
        {
            rot.X = rx;
            rot.Y = ry;
        }

        #region AddObjectMethods

        private void addCamera(bool cull = true)
        {
            //Set Camera position
            Camera cam = new Camera(60, resMgr.GLShaders[SHADER_TYPE.BBOX_SHADER].program_id, 0, cull);
            for (int i = 0; i < 20; i++)
                cam.Move(0.0f, -0.1f, 0.0f);
            cam.isActive = false;
            resMgr.GLCameras.Add(cam);
        }

        private void addDefaultTextures()
        {
            string execpath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            //Add Default textures
            //White tex
            string texpath = Path.Combine(execpath, "default.dds");
            Texture tex = new Texture(texpath);
            tex.name = "default.dds";
            resMgr.texMgr.addTexture(tex);
            //Transparent Mask
            texpath = Path.Combine(execpath, "default_mask.dds");
            tex = new Texture(texpath);
            tex.name = "default_mask.dds";
            resMgr.texMgr.addTexture(tex);
        }

        private void addDefaultPrimitives()
        {
            //Default quad
            MVCore.Primitives.Quad q = new MVCore.Primitives.Quad(1.0f, 1.0f);
            resMgr.GLPrimitiveVaos["default_quad"] = q.getVAO();

            //Default render quad
            q = new MVCore.Primitives.Quad();
            resMgr.GLPrimitiveVaos["default_renderquad"] = q.getVAO();

            //Default cross
            MVCore.Primitives.Cross c = new MVCore.Primitives.Cross();
            resMgr.GLPrimitiveVaos["default_cross"] = c.getVAO();

            //Default cube
            MVCore.Primitives.Box bx = new MVCore.Primitives.Box(1.0f, 1.0f, 1.0f);
            resMgr.GLPrimitiveVaos["default_box"] = bx.getVAO();

            //Default sphere
            MVCore.Primitives.Sphere sph = new MVCore.Primitives.Sphere(new Vector3(0.0f,0.0f,0.0f), 100.0f);
            resMgr.GLPrimitiveVaos["default_sphere"] = sph.getVAO();
        }

        private void addTestObjects()
        {
            
        }

        #endregion AddObjectMethods


        public void issueRequest(ref ThreadRequest r)
        {
            lock (rt_req_queue)
            {
                rt_req_queue.Enqueue(r);
            }
        }

        private void rt_ResizeViewport(int w, int h)
        {
            renderMgr.resize(w, h);
        }

        private void rt_addRootScene(string filename)
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            Palettes.set_palleteColors();

            //Clear Form Resources
            resMgr.Cleanup();
            MVCore.Common.RenderState.activeResMgr = resMgr;
            ModelProcGen.procDecisions.Clear();
            //Clear animScenes
            animScenes.Clear();
            rootObject = null;

            //Clear RenderStats
            RenderStats.clearStats();
            
            //Stop animation if on
            if (RenderOptions.ToggleAnimations)
                toggleAnimation();
            
            //Add defaults
            addDefaultLights();
            addDefaultTextures();
            addCamera();
            addCamera(cull: false); //Add second camera
            setActiveCam(0);
            addDefaultPrimitives();

            //Setup new object
            rootObject = GEOMMBIN.LoadObjects(filename, false);

            //Explicitly add default light to the rootObject
            rootObject.children.Add(resMgr.GLlights[0]);

            //Populate RenderManager
            renderMgr.populate(rootObject);
            
            //find Animation Capable nodes
            findAnimScenes(rootObject);

            //Refresh all transforms
            rootObject.update();

            //Restart anim worker if it was active
            if (!RenderOptions.ToggleAnimations)
                toggleAnimation();
        }

        //Light Functions
        private void addDefaultLights()
        {
            //Add one and only light for now
            Light light = new Light();
            light.name = "Default Light";
            light.intensity = 50000;
            light.shader_programs = new GLSLHelper.GLSLShaderConfig[] { this.resMgr.GLShaders[GLSLHelper.SHADER_TYPE.LIGHT_SHADER] };

            /*
            light.localPosition = new Vector3((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                Math.Cos(this.light_angle_y * Math.PI / 180.0)));
            */
            light.localPosition = new Vector3(10.0f, 10.0f, 10.0f);
            light.main_Vao = new MVCore.Primitives.LineSegment(1, new Vector3(1.0f, 1.0f, 1.0f)).getVAO();

            resMgr.GLlights.Add(light);
        }


        public void updateLightPosition(int light_id)
        {
            Light light = resMgr.GLlights[light_id];
            light.updatePosition(new Vector3 ((float)(light_distance * Math.Cos(MathUtils.radians(light_angle_x)) *
                                                            Math.Sin(MathUtils.radians(light_angle_y))),
                                                (float)(light_distance * Math.Sin(MathUtils.radians(light_angle_x))),
                                                (float)(light_distance * Math.Cos(MathUtils.radians(light_angle_x)) *
                                                            Math.Cos(MathUtils.radians(light_angle_y)))));
        }

        private void fps()
        {
            //Get FPS
            DateTime now = DateTime.UtcNow;
            TimeSpan time = now - oldtime;
            dt = (now - prevtime).TotalMilliseconds;
            
            if (time.TotalMilliseconds > 1000)
            {
                RenderStats.fpsCount = frames;
                //Console.WriteLine("{0} {1} {2}", frames, RenderStats.fpsCount, time.TotalMilliseconds);
                //Reset
                frames = 0;
                oldtime = now;
            }
            else
            {
                frames += 1;
                prevtime = now;
            }
        }


        #region INPUT_HANDLERS

        //Gamepad handler
        private void gamepadController()
        {
            if (gpHandler == null) return;
            
            //This Method handles and controls the gamepad input
            gpHandler.updateState();
            //gpHandler.reportAxes();
            
            //Move camera
            //Console.WriteLine(gpHandler.getBtnState(1) - gpHandler.getBtnState(0));
            //Console.WriteLine(gpHandler.getAxsState(0, 1));
            for (int i = 0; i < movement_speed; i++)
                RenderState.activeCam.Move(0.1f * gpHandler.getAxsState(0, 0),
                               0.1f * gpHandler.getAxsState(0, 1),
                               gpHandler.getBtnState(1) - gpHandler.getBtnState(0));

            //Rotate Camera
            //for (int i = 0; i < movement_speed; i++)
            RenderState.activeCam.AddRotation(-3.0f * gpHandler.getAxsState(1, 0), 3.0f * gpHandler.getAxsState(1, 1));
            //Console.WriteLine("Camera Orientation {0} {1}", activeCam.Orientation.X,
            //    activeCam.Orientation.Y,
            //    activeCam.Orientation.Z);
        }

        //Keyboard handler
        private void keyboardController()
        {
            if (kbHandler == null) return;

            //This Method handles and controls the gamepad input
            
            kbHandler.updateState();
            //gpHandler.reportAxes();

            //Camera Movement
            float step = movement_speed * 0.01f;
            RenderState.activeCam.Move(
                    step * (kbHandler.getKeyStatus(OpenTK.Input.Key.D) - kbHandler.getKeyStatus(OpenTK.Input.Key.A)),
                    step * (kbHandler.getKeyStatus(OpenTK.Input.Key.W) - kbHandler.getKeyStatus(OpenTK.Input.Key.S)),
                    step * (kbHandler.getKeyStatus(OpenTK.Input.Key.R) - kbHandler.getKeyStatus(OpenTK.Input.Key.F)));

            
            //Rotate Axis
            rot.Y += 50 * step * (kbHandler.getKeyStatus(OpenTK.Input.Key.E) - kbHandler.getKeyStatus(OpenTK.Input.Key.Q));
            rot.X += 50 * step * (kbHandler.getKeyStatus(OpenTK.Input.Key.C) - kbHandler.getKeyStatus(OpenTK.Input.Key.Z));
            
        }

        #endregion

        #region ANIMATION_PLAYBACK
        //Animation Playback

        public void toggleAnimation()
        {
            RenderOptions.ToggleAnimations = !RenderOptions.ToggleAnimations;
        }

        #endregion ANIMATION_PLAYBACK

        #region DISPOSE_METHODS

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                //Free other resources here
                rootObject.Dispose();
            }

            //Free unmanaged resources
            disposed = true;
        }

        #endregion DISPOSE_METHODS

    }

}
