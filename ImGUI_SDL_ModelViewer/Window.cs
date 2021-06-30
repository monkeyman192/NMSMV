using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using ImGuiNET;
using ImGuiHelper;
using OpenTK.Windowing.Common;
using MVCore;
using MVCore.Engine;
using MVCore.Common;
using MVCore.Utils;
using System.Collections.Generic;
using MVCore.GMDL;
using MVCore.Text;
using System.IO;

namespace ImGUI_SDL_ModelViewer
{
    public class Window : GameWindow
    {
        ImGuiController _controller;

        //Parameters
        private string current_file_path = Environment.CurrentDirectory;
        private string status_string = "Ready";

        //Mouse Pos
        private MouseMovementState mouseState = new MouseMovementState();
        private MouseMovementStatus mouseMovementStatus = MouseMovementStatus.IDLE;

        //Gizmo
        public Gizmo activeGizmo;
        public TranslationGizmo gizTranslate;

        //Scene Stuff
        //public Model rootObject;
        public Model activeModel; //Active Model Reference
        public Queue<Model> modelUpdateQueue = new Queue<Model>();
        public List<Tuple<AnimComponent, AnimData>> activeAnimScenes = new List<Tuple<AnimComponent, AnimData>>();

        //Engine
        private Engine engine;


        //ImGui Variables
        private bool show_open_file_dialog = false;
        private bool show_open_file_dialog_pak = false;
        private bool show_update_libmbin_dialog = false;
        private int active_tab_id = 0;
        private string libMbinOnlineVersion = null;
        private string libMbinLocalVersion = null;

        //ImguiPalette Colors
        //Blue
        private System.Numerics.Vector4 DarkBlue = new System.Numerics.Vector4(0.04f, 0.2f, 0.96f, 1.0f);


        public Window() : base(GameWindowSettings.Default, 
            new NativeWindowSettings() { Size = new Vector2i(800, 600), APIVersion = new Version(4, 5) })
        {
            RenderFrequency = 240;
            UpdateFrequency = RenderFrequency * 2;
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            Title = "NMSMV " + Util.getVersion();
            _controller = new ImGuiController(ClientSize.X, ClientSize.Y);

            //Setup Logger
            Util.loggingSr = new StreamWriter("log.out");

            //SETUP THE CALLBACKS OF MVCORE
            CallBacks.updateStatus = Util.setStatus;
            CallBacks.showInfo = Util.showInfo;
            CallBacks.showError = Util.showError;
            CallBacks.Log = Util.Log;



            //OVERRIDE SETTINGS
            //FileUtils.dirpath = "I:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\PCBANKS";

            //Load Settings
            SettingsForm.loadSettingsStatic();
            CallBacks.Log("* Starting GLControl WorkThreads", LogVerbosityLevel.DEBUG);

            //Initialize Engine backend
            engine = new Engine(this);


            //Populate GLControl
            Scene scene = new Scene()
            {
                type = TYPES.MODEL,
                name = "DEFAULT SCENE"
            };

            //Add default scene to the resource manager
            RenderState.activeResMgr.GLScenes["DEFAULT_SCENE"] = scene;

            //Force rootobject
            RenderState.rootObject = scene;
            glControl.modelUpdateQueue.Enqueue(scene);
            glControl.engine.renderSys.populate(scene);

            //SceneTreeView.Items.Clear();
            //SceneTreeView.Items.Add(scene);

            //Check if Temp folder exists
#if DEBUG
            if (!Directory.Exists("Temp")) Directory.CreateDirectory("Temp");
#endif
            //Set active Components
            Util.activeStatusStrip = StatusLabel;
            Util.activeControl = glControl;
            Util.activeWindow = this;

            //Bind Settings
            //RenderViewOptionsControl.Content = RenderState.renderViewSettings;
            //RenderOptionsControl.Content = RenderState.settings.rendering;

            //Add event handlers to GUI elements
            //sliderzNear.ValueChanged += Sliders_OnValueChanged;
            //sliderzFar.ValueChanged += Sliders_OnValueChanged;
            //sliderFOV.ValueChanged += Sliders_OnValueChanged;
            //sliderLightIntensity.ValueChanged += Sliders_OnValueChanged;
            //sliderlightDistance.ValueChanged += Sliders_OnValueChanged;
            //sliderMovementSpeed.ValueChanged += Sliders_OnValueChanged;
            //sliderMovementFactor.ValueChanged += Sliders_OnValueChanged;

            //Invoke the method in order to setup the control at startup

            //TODO: Bring that back
            //Sliders_OnValueChanged(null, new RoutedPropertyChangedEventArgs<double>(0.0f, 0.0f));
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;


            //Disable Open File Functions
            OpenFileHandle.IsEnabled = false;
            OpenFilePAKHandle.IsEnabled = false;
            //TestOptions.Visibility = Visibility.Hidden; //Hide the test options by default

#if (DEBUG)
            //Enable the Test options if it is a debug version
            //TestOptions.Visibility = Visibility.Visible;
            setTestComponents();
#endif

            CallBacks.Log("* Issuing NMS Archive Preload Request", LogVerbosityLevel.INFO);

            //Issue work request 
            ThreadRequest rq = new ThreadRequest();
            rq.arguments.Add("NMSmanifest");
            rq.arguments.Add(Path.Combine(RenderState.settings.GameDir, "PCBANKS"));
            rq.arguments.Add(RenderState.activeResMgr);
            rq.type = THREAD_REQUEST_TYPE.LOAD_NMS_ARCHIVES_REQUEST;
            workDispatcher.sendRequest(rq);

            issuedRequests.Add(rq);



            //Basic Initialization of ImGui
            InitImGUI();

            
        }

        private void InitImGUI()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable; //Enable Docking
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            // Update the opengl viewport
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            // Tell ImGui of the new size
            _controller.WindowResized(ClientSize.X, ClientSize.Y);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            _controller.Update(this, (float)e.Time);
            Console.WriteLine("Updating Frame");
            
            if (engine.rt_State == EngineRenderingState.ACTIVE)
            {
                engine.handleRequests();
                frameUpdate(e.Time);
            }

        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            
            GL.ClearColor(new Color4(5, 5, 5, 255));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            //Render Shit
            if (engine.rt_State == EngineRenderingState.ACTIVE)
            {
                //CallBacks.Log("* CONTROL : STARTING FRAME UPDATE", LogVerbosityLevel.DEBUG);
                //CallBacks.Log("* CONTROL : FRAME UPDATED", LogVerbosityLevel.DEBUG);
                //CallBacks.Log("* CONTROL : STARTING FRAME RENDER", LogVerbosityLevel.DEBUG);

                ErrorCode err;
                err = GL.GetError();
                if (err != ErrorCode.NoError)
                    Console.WriteLine("test");

                engine.renderSys.testrender(); //Render Everything

                err = GL.GetError();
                if (err != ErrorCode.NoError)
                    Console.WriteLine("test");

                //CallBacks.Log("* CONTROL : FRAME RENDERED", LogVerbosityLevel.DEBUG);
            }


            
            //UI
            DrawUI();
            //ImGui.ShowDemoWindow();
            _controller.Render();


            //ImGuiUtil.CheckGLError("End of frame");

            RenderStats.fpsCount = 1.0f / (float)e.Time;

            SwapBuffers();
        }

        private void frameUpdate(double dt)
        {
            //VSync = RenderState.settings.rendering.UseVSYNC; //Update Vsync 

            //Console.WriteLine(RenderState.renderSettings.RENDERMODE);

            //Gizmo Picking
            //Send picking request
            //Make new request
            activeGizmo = null;
            if (RenderState.renderViewSettings.RenderGizmos)
            {
                ThreadRequest req = new()
                {
                    type = THREAD_REQUEST_TYPE.GIZMO_PICKING_REQUEST
                };
                req.arguments.Clear();
                req.arguments.Add(activeGizmo);
                req.arguments.Add(mouseState.Position);
                engine.issueRenderingRequest(ref req);
            }

            //Set time to the renderManager
            engine.renderSys.progressTime(dt);

            //Reset Stats
            RenderStats.occludedNum = 0;

            //Update moving queue
            while (modelUpdateQueue.Count > 0)
            {
                Model m = modelUpdateQueue.Dequeue();
                m.update();
            }

            //rootObject?.update(); //Update Distances from camera
            RenderState.rootObject?.updateLODDistances(); //Update Distances from camera
            engine.renderSys.clearInstances(); //Clear All mesh instances
            RenderState.rootObject?.updateMeshInfo(); //Reapply frustum culling and re-setup visible instances

            //Update gizmo
            if (activeModel != null)
            {
                //TODO: Move gizmos
                //gizTranslate.setReference(activeModel);
                //gizTranslate.updateMeshInfo();
                //GLMeshVao gz = resMgr.GLPrimitiveMeshVaos["default_translation_gizmo"];
                //GLMeshBufferManager.addInstance(ref gz, TranslationGizmo);
            }

            //Identify dynamic Objects
            foreach (Model s in engine.animationSys.AnimScenes)
            {
                modelUpdateQueue.Enqueue(s.parentScene);
            }

            //Console.WriteLine("Dt {0}", dt);
            if (RenderState.renderViewSettings.EmulateActions)
            {
                engine.actionSys.Update((float)(1000 * dt)); //time is expected in ms
            }

            //Progress animations
            if (RenderState.settings.rendering.ToggleAnimations)
            {
                engine.animationSys.Update((float)(1000 * dt)); //time is expected in ms
            }


            //Camera & Light Positions
            //Update common transforms
            RenderState.activeResMgr.GLCameras[0].aspect = (float) Size.X / Size.Y;

            //Apply extra viewport rotation
            Matrix4 Rotx = Matrix4.CreateRotationX(MathUtils.radians(RenderState.rotAngles.X));
            Matrix4 Roty = Matrix4.CreateRotationY(MathUtils.radians(RenderState.rotAngles.Y));
            Matrix4 Rotz = Matrix4.CreateRotationZ(MathUtils.radians(RenderState.rotAngles.Z));
            RenderState.rotMat = Rotz * Rotx * Roty;
            //RenderState.rotMat = Matrix4.Identity;

            RenderState.activeResMgr.GLCameras[0].Move(dt);
            RenderState.activeResMgr.GLCameras[0].updateViewMatrix();
            RenderState.activeResMgr.GLCameras[1].updateViewMatrix();

            //Update Text Counters
            RenderState.activeResMgr.txtMgr.getText(TextManager.Semantic.FPS).update(string.Format("FPS: {0:000.0}",
                                                                        (float)RenderStats.fpsCount));
            RenderState.activeResMgr.txtMgr.getText(TextManager.Semantic.OCCLUDED_COUNT).update(string.Format("OccludedNum: {0:0000}",
                                                                        RenderStats.occludedNum));
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            _controller.PressChar((char)e.Unicode);
        }
        

        private void DrawUI()
        {
            //Enable docking in main view
            ImGuiDockNodeFlags dockspace_flags = ImGuiDockNodeFlags.None;
            dockspace_flags |= ImGuiDockNodeFlags.PassthruCentralNode;

            ImGuiWindowFlags window_flags = ImGuiWindowFlags.NoResize | 
                                            ImGuiWindowFlags.NoTitleBar |
                                            ImGuiWindowFlags.NoBackground |
                                            ImGuiWindowFlags.NoCollapse;

            ImGuiViewportPtr vp = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(vp.GetWorkPos());
            ImGui.SetNextWindowSize(vp.GetWorkSize());
            ImGui.SetNextWindowViewport(vp.ID);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);

            //Main View
            bool dockspace_open = true;
            ImGui.Begin("DockSpaceDemo", ref dockspace_open, window_flags);

            uint dockSpaceID = ImGui.GetID("MyDockSpace");
            ImGui.DockSpace(dockSpaceID,
                new System.Numerics.Vector2(0.0f, 0.0f), dockspace_flags);

            ImGui.End();


            //Main Menu
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Open", "Ctrl + O"))
                    {
                        show_open_file_dialog = true;
                    }

                    if (ImGui.MenuItem("Open from PAK"))
                    {
                        show_open_file_dialog_pak = true;
                    }

                    if (ImGui.MenuItem("Update LibMBIN"))
                    {
                        //TODO
                        show_update_libmbin_dialog = true;
                    }

                    if (ImGui.MenuItem("Close", "Ctrl + Q"))
                    {
                        //TODO, properly cleanup and close the window
                        Close();
                    }

                    ImGui.EndMenu();
                }

                ImGui.EndMenuBar();
            }

            //ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, Size.Y - 2.0f * ImGui.CalcTextSize("test").Y));
            //ImGui.SetNextWindowSize(new System.Numerics.Vector2(Size.X, Size.Y - 2.0f * ImGui.CalcTextSize("test").Y));

            ImGui.SetCursorPosX(0.0f);
            bool main_view = true;
            
            if (ImGui.Begin("SideBar", ref main_view))
            {
                if (ImGui.BeginChild("RightView"))
                {
                    ImGui.BeginGroup();

                    if (ImGui.TreeNode("SceneGraph"))
                    {
                        //TODO populate scenegraph
                        ImGui.TreePop();
                    }
                    ImGui.Text("End of SceneGraph");
                    ImGui.EndGroup();
                    ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMin(), 0x00FFFFFF);

                    ImGui.Separator();
                    
                    //Draw Tab Controls
                    if (ImGui.BeginTabBar("Tab Control", ImGuiTabBarFlags.None))
                    {
                        if (ImGui.BeginTabItem("Camera"))
                        {
                            //Camera Settings
                            ImGui.BeginGroup();
                            ImGui.TextColored(DarkBlue, "Camera Settings");
                            float test = 0.0f;
                            ImGui.SliderFloat("FOV", ref test, 90.0f, 100.0f);
                            /*
                            ImGui.SliderFloat("FOV", ref MVCore.Common.RenderState.activeCam.fov, 90.0f, 100.0f);
                            ImGui.SliderFloat("MovementSpeed", ref MVCore.Common.RenderState.activeCam.Speed, 1.0f, 20.0f);
                            ImGui.SliderFloat("MovementPower", ref MVCore.Common.RenderState.activeCam.SpeedPower, 1.0f, 10.0f);
                            ImGui.SliderFloat("zNear", ref MVCore.Common.RenderState.activeCam.zNear, 0.01f, 1.0f);
                            ImGui.SliderFloat("zFar", ref MVCore.Common.RenderState.activeCam.zFar, 101.0f, 30000.0f);
                                
                            ImGui.SliderFloat("lightDistance", ????, 0.0f, 20.0f);
                            ImGui.SliderFloat("lightIntensity", ????, 0.0f, 20.0f);
                            */

                            if (ImGui.Button("Reset Camera"))
                            {
                                //TODO
                            }

                            ImGui.SameLine();
                                
                            if (ImGui.Button("Reset Scene Rotation"))
                            {
                                //TODO
                            }

                            ImGui.EndGroup();


                            ImGui.Separator();
                            ImGui.BeginGroup();
                            ImGui.TextColored(DarkBlue, "Camera Controls");
                            
                            ImGui.Columns(2);

                            ImGui.Text("Horizontal Camera Movement");
                            ImGui.Text("Vertical Camera Movement");
                            ImGui.Text("Camera Rotation");
                            ImGui.Text("Scene Rotate (Y Axis)");
                            ImGui.NextColumn();
                            ImGui.Text("W, A, S, D");
                            ImGui.Text("R, F");
                            ImGui.Text("Hold RMB +Move");
                            ImGui.Text("Q, E");
                            ImGui.EndGroup();

                            ImGui.Columns(1);
                            
                            ImGui.EndTabItem();
                        }

                        if (ImGui.BeginTabItem("View Options"))
                        {
                            
                            ImGui.EndTabItem();
                        }

                        if (ImGui.BeginTabItem("Test Options"))
                        {

                            ImGui.EndTabItem();
                        }
                        
                        if (ImGui.BeginTabItem("RenderInfoOptions"))
                        {

                            ImGui.EndTabItem();
                        }
                        
                        if (ImGui.BeginTabItem("Tools"))
                        {

                            if (ImGui.Button("ProcGen", new System.Numerics.Vector2(80.0f, 40.0f)))
                            {
                                //TODO generate proc gen view
                            }

                            ImGui.SameLine();

                            if (ImGui.Button("Reset Pose", new System.Numerics.Vector2(80.0f, 40.0f)))
                            {
                                //TODO Reset The models pose
                            }
                            
                            ImGui.EndTabItem();
                        }

                        if (ImGui.BeginTabItem("Object Info"))
                        {

                            ImGui.EndTabItem();
                        }

                        ImGui.EndTabBar();
                    }

                    ImGui.EndChild();
                }

                ImGui.End();
            }

            

            //StatusBar

            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, Size.Y - 2.0f * ImGui.CalcTextSize("test").Y));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(Size.X, 1.75f * ImGui.CalcTextSize("test").Y));

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            
            if (ImGui.Begin("StatusBar", ImGuiWindowFlags.NoMove |
                                         ImGuiWindowFlags.NoDecoration))
            {
                ImGui.Columns(2, "statusbarColumns", false);
                ImGui.Text(status_string);
                ImGui.NextColumn();
                string text = "Created by gregkwaste";
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize(text).X
                    - ImGui.GetScrollX() - 2 * ImGui.GetStyle().ItemSpacing.X);
                
                ImGui.Text(text);
                ImGui.End();
            }

            //Functionality
                
            if (show_open_file_dialog)
            {
                ImGui.OpenPopup("open-file");
                show_open_file_dialog = false;
            }

            if (show_update_libmbin_dialog)
            {
                ImGui.OpenPopup("update-libmbin");
                show_update_libmbin_dialog = false;
            }

                
            var isOpen = true;
            if (ImGui.BeginPopupModal("open-file", ref isOpen, ImGuiWindowFlags.NoTitleBar))
            {
                var picker = ImGuiHelper.FilePicker.GetFilePicker(this, current_file_path, ".SCENE.MBIN|.SCENE.EXML");
                if (picker.Draw())
                {
                    Console.WriteLine(picker.SelectedFile);
                    current_file_path = picker.CurrentFolder;
                    FilePicker.RemoveFilePicker(this);
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("update-libmbin", ref isOpen, ImGuiWindowFlags.None))
            {
                if (libMbinLocalVersion == null)
                    libMbinLocalVersion = MVCore.Utils.HTMLUtils.queryLibMBINDLLLocalVersion();
                
                if (libMbinOnlineVersion == null)
                {
                    libMbinOnlineVersion = MVCore.Utils.HTMLUtils.queryLibMBINDLLOnlineVersion();
                }
                    
                ImGui.Text("Old Version: " + libMbinLocalVersion);
                ImGui.Text("Online Version: " + libMbinOnlineVersion);
                ImGui.Text("Do you want to update?");

                bool updatelibmbin = false;
                if (ImGui.Button("YES"))
                {
                    updatelibmbin = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("NO"))
                {
                    libMbinLocalVersion = null;
                    libMbinOnlineVersion = null;
                    ImGui.CloseCurrentPopup();
                }

                if (updatelibmbin)
                {
                    //MVCore.Utils.HTMLUtils.fetchLibMBINDLL();
                    libMbinLocalVersion = null;
                    libMbinOnlineVersion = null;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();

            }


        }

    }
}
