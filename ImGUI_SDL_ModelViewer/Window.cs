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
        public Queue<Model> modelUpdateQueue = new();
        public List<Tuple<AnimComponent, AnimData>> activeAnimScenes = new();

        //Engine
        private Engine engine;

        //Workers
        private WorkThreadDispacher workDispatcher = new();
        private List<Task> workTasks = new(); //Keep track of the issued tasks

        //ImGui Variables
        private ImGUIPakBrowser PackBrowser;
        private ImGuiAboutWindow AboutWindow;
        private bool show_open_file_dialog = false;
        private bool show_open_file_dialog_pak = false;
        private bool open_file_enabled = false;
        private bool show_update_libmbin_dialog = false;
        private bool show_settings_window = false;
        private bool show_about_window = false;
        private bool show_test_components = false;
        private string libMbinOnlineVersion = null;
        private string libMbinLocalVersion = null;


        private Vector2i SceneViewSize = new();
        private int ItemCounter = 0;
        //ImguiPalette Colors
        //Blue
        private System.Numerics.Vector4 DarkBlue = new(0.04f, 0.2f, 0.96f, 1.0f);

        
        public Window() : base(GameWindowSettings.Default, 
            new NativeWindowSettings() { Size = new Vector2i(800, 600), APIVersion = new Version(4, 5) })
        {
            //Set Window Title
            Title = "NMSMV " + Util.getVersion();

            //Setup Logger
            Util.loggingSr = new StreamWriter("log.out");

            //SETUP THE CALLBACKS FOR THE MVCORE ENVIRONMENT
            CallBacks.updateStatus = Util.setStatus;
            CallBacks.showInfo = Util.showInfo;
            CallBacks.showError = Util.showError;
            CallBacks.Log = Util.Log;
            CallBacks.getResource = Util.getResource;
            CallBacks.getBitMapResource = Util.getBitMapResource;
            CallBacks.getTextResource = Util.getTextResource;

            RenderFrequency = 240;
            UpdateFrequency = RenderFrequency * 2;
            SceneViewSize = Size;

            //Start worker thread
            workDispatcher.Start();

            //Initialize Resource Manager
            RenderState.activeResMgr = new ResourceManager();
            RenderState.activeResMgr.Init();
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            
            _controller = new ImGuiController(ClientSize.X, ClientSize.Y);
            
            //OVERRIDE SETTINGS
            //FileUtils.dirpath = "I:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\PCBANKS";

            //Load Settings
            if (!File.Exists("settings.json"))
                show_settings_window = true;
            
            RenderState.settings = Settings.loadFromDisk();
            
            //Initialize Engine backend
            engine = new Engine(this);
            engine.init(Size.X, Size.Y); //Initialize Engine
            
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
            modelUpdateQueue.Enqueue(scene);
            engine.renderSys.populate(scene);

            //SceneTreeView.Items.Clear();
            //SceneTreeView.Items.Add(scene);

            //Check if Temp folder exists
#if DEBUG
            if (!Directory.Exists("Temp")) Directory.CreateDirectory("Temp");
#endif
            //Set active Components
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
            

            
#if (DEBUG)
            //Enable the Test options if it is a debug version
            //TestOptions.Visibility = Visibility.Visible;
            show_test_components = true;
#endif

            //Initialize ImGUi Objectives
            PackBrowser = new ImGUIPakBrowser();
            AboutWindow = new ImGuiAboutWindow();

            CallBacks.Log("* Issuing NMS Archive Preload Request", LogVerbosityLevel.INFO);

            //Issue work request 
            ThreadRequest rq = new ThreadRequest();
            rq.arguments.Add("NMSmanifest");
            rq.arguments.Add(Path.Combine(RenderState.settings.GameDir, "PCBANKS"));
            rq.arguments.Add(RenderState.activeResMgr);
            rq.type = THREAD_REQUEST_TYPE.LOAD_NMS_ARCHIVES_REQUEST;
            workTasks.Add(workDispatcher.sendRequest(rq));

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

            // Tell ImGui of the new size
            _controller.WindowResized(ClientSize.X, ClientSize.Y);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            //Console.WriteLine("Updating Frame");
            
            if (engine.rt_State == EngineRenderingState.ACTIVE)
            {
                engine.handleRequests(); //Handle engine requests
                handleRequests(); //Handle window requests
                frameUpdate(e.Time);
            }
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            _controller.Update(this, (float) e.Time);
            //Console.WriteLine("Rendering Frame");
            GL.ClearColor(new Color4(5, 5, 5, 255));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            //Render Shit
            if (engine.rt_State == EngineRenderingState.ACTIVE)
            {
                //CallBacks.Log("* CONTROL : STARTING FRAME UPDATE", LogVerbosityLevel.DEBUG);
                //CallBacks.Log("* CONTROL : FRAME UPDATED", LogVerbosityLevel.DEBUG);
                //CallBacks.Log("* CONTROL : STARTING FRAME RENDER", LogVerbosityLevel.DEBUG);

                engine.renderSys.testrender(); //Render Everything

                //CallBacks.Log("* CONTROL : FRAME RENDERED", LogVerbosityLevel.DEBUG);
            }
            
            //Bind Default Framebuffer
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            
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
                engine.sendRequest(ref req);
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
                    if (ImGui.MenuItem("Open", "Ctrl + O", false, open_file_enabled))
                    {
                        show_open_file_dialog = true;
                    }

                    if (ImGui.MenuItem("Open from PAK", "", false, open_file_enabled))
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

                if (ImGui.MenuItem("About"))
                {
                    show_about_window = true;
                }

                ImGui.EndMenuBar();
            }

            //ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, Size.Y - 2.0f * ImGui.CalcTextSize("test").Y));
            //ImGui.SetNextWindowSize(new System.Numerics.Vector2(Size.X, Size.Y - 2.0f * ImGui.CalcTextSize("test").Y));

            ImGui.SetCursorPosX(0.0f);
            bool main_view = true;

            
            //Scene Render
            bool scene_view = true;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(0.0f, 0.0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, new System.Numerics.Vector2(0.0f, 0.0f));
            if (ImGui.Begin("Scene", ref scene_view, ImGuiWindowFlags.NoScrollbar))
            {
                //Update RenderSize
                System.Numerics.Vector2 csize = ImGui.GetContentRegionAvail();
                Vector2i csizetk = new Vector2i( (int) ImGui.GetContentRegionAvail().X,
                                               (int) ImGui.GetContentRegionAvail().Y);

                ImGui.Image(new IntPtr(engine.renderSys.getRenderFBO().channels[0]),
                                csize,
                                new System.Numerics.Vector2(0.0f, 1.0f),
                                new System.Numerics.Vector2(1.0f, 0.0f));
                
                if (csizetk != SceneViewSize)
                {
                    SceneViewSize = csizetk;
                    engine.renderSys.resize(csizetk);
                }
                
                ImGui.End();
                ImGui.PopStyleVar();
                ImGui.PopStyleVar();

            }

            //SideBar

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

            if (show_open_file_dialog_pak)
            {
                ImGui.OpenPopup("open-file-pak");
                show_open_file_dialog_pak = false;
            }

            if (show_update_libmbin_dialog)
            {
                ImGui.OpenPopup("update-libmbin");
                show_update_libmbin_dialog = false;
            }

            if (show_about_window)
            {
                ImGui.OpenPopup("show-about");
                show_about_window = false;
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

            if (ImGui.BeginPopupModal("open-file-pak", ref isOpen))
            {
                if (PackBrowser.isFinished())
                {
                    PackBrowser.Clear();
                    ImGui.CloseCurrentPopup();
                    //Issue File Open Request
                } else
                {
                    PackBrowser.Draw();
                }
                
                if (ImGui.IsKeyPressed(ImGui.GetKeyIndex(ImGuiKey.Escape)))
                {
                    PackBrowser.Clear();
                    ImGui.CloseCurrentPopup();
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

            
            if (ImGui.BeginPopupModal("show-about", ref isOpen, ImGuiWindowFlags.NoResize)){

                ImGuiNative.igSetNextWindowSize(new System.Numerics.Vector2(256 + 36, 256 + 60), ImGuiCond.Appearing);
                if (ImGui.IsKeyPressed(ImGui.GetKeyIndex(ImGuiKey.Escape)))
                {
                    ImGui.CloseCurrentPopup();
                }

                AboutWindow.Draw();

                ImGui.EndPopup();
            }

            //Debugging Tools

            if (ImGui.Begin("Statistics"))
            {
                ImGui.Text(string.Format("FPS : {0, 3:F1}", RenderStats.fpsCount));
                ImGui.Text(string.Format("VertexCount : {0}", RenderStats.vertNum));
                ImGui.Text(string.Format("TrisCount : {0}", RenderStats.trisNum));
                ImGui.End();
            }


        }

        private void OpenFile(string filename, bool testScene, int testSceneID)
        {
            Console.WriteLine("Importing " + filename);
            ThreadRequest req;

            //Pause renderer
            req = new()
            {
                type = THREAD_REQUEST_TYPE.GL_PAUSE_RENDER_REQUEST
            };
            req.arguments.Clear();

            //Send request to engine
            engine.sendRequest(ref req);
            
            //Ask window to wait for the request to Finish
            //For now the engine and the window belong to the same thread so this
            //does not make any difference
            waitForRequest(ref req);

            RenderState.rootObject?.Dispose();

            if (testScene)
                addTestScene(testSceneID);
            else
                addScene(filename);

            //Populate 
            RenderState.rootObject.ID = ItemCounter;
            Util.setStatus("Creating Treeview...");
            
            //Cache the treeview and pass it to ImGUI

            //Add to UI
            Util.setStatus("Ready");

            //Generate Request for resuming rendering
            ThreadRequest req2 = new()
            {
                type = THREAD_REQUEST_TYPE.GL_RESUME_RENDER_REQUEST
            };

            engine.sendRequest(ref req2);
        
        }


        public void handleRequests()
        {
            int i = 0;
            while (i < workTasks.Count){

                Task t = workTasks[i];


                //Finalize tasks
                if (!t.thread.IsAlive)
                {
                    switch (t.thread_request.type)
                    {
                        case THREAD_REQUEST_TYPE.LOAD_NMS_ARCHIVES_REQUEST:
                            open_file_enabled = true;
                            break;

                    }
                    workTasks.RemoveAt(i);
                    continue;
                } 
                i++;
            }

        }

        public void waitForRequest(ref ThreadRequest req)
        {
            while (true)
            {
                int a = 0;
                lock (req)
                {
                    if (req.status == THREAD_REQUEST_STATUS.FINISHED)
                        return;
                    else
                        a++; // Do some dummy stuff
                }
            }
        }


        //Scene Loading
        public void addTestScene(int sceneID)
        {
            //Cleanup first
            modelUpdateQueue.Clear(); //Clear Update Queues

            //Generate Request for rendering thread
            ThreadRequest req1 = new()
            {
                type = THREAD_REQUEST_TYPE.NEW_TEST_SCENE_REQUEST
            };
            req1.arguments.Add(sceneID);

            engine.sendRequest(ref req1);

            //Wait for requests to finish before return
            waitForRequest(ref req1);

            //find Animation Capable nodes
            activeModel = null; //TODO: Fix that with the gizmos
            findAnimScenes(RenderState.rootObject); //Repopulate animScenes
            findActionScenes(RenderState.rootObject); //Re-populate actionSystem

        }

        public void addScene(string filename)
        {
            //Cleanup first
            modelUpdateQueue.Clear(); //Clear Update Queues

            //Generate Request for rendering thread
            ThreadRequest req1 = new()
            {
                type = THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST
            };
            req1.arguments.Clear();
            req1.arguments.Add(filename);

            engine.sendRequest(ref req1);

            //Wait for requests to finish before return
            waitForRequest(ref req1);

            //find Animation Capable nodes
            activeModel = null; //TODO: Fix that with the gizmos
            findAnimScenes(RenderState.rootObject); //Repopulate animScenes
            findActionScenes(RenderState.rootObject); //Re-populate actionSystem
        }

        public void findAnimScenes(Model node)
        {
            if (node.animComponentID >= 0)
                engine.animationSys.Add(node);
            foreach (Model child in node.children)
                findAnimScenes(child);
        }

        public void findActionScenes(Model node)
        {
            if (node.actionComponentID >= 0)
                engine.actionSys.Add(node);

            foreach (Model child in node.children)
                findActionScenes(child);
        }

    }


    unsafe public class ImGUIPakBrowser
    {
        ImGuiTextFilterPtr _filter;
        string selected_item = "";
        bool DialogFinished;

        public ImGUIPakBrowser()
        {
            var filterPtr = ImGuiNative.ImGuiTextFilter_ImGuiTextFilter(null);
            _filter = new ImGuiTextFilterPtr(filterPtr);
            DialogFinished = false;
        }

        public void Draw()
        {
            _filter.Draw("Filter");
            //Draw listbox
            ImGui.BeginChild("ListBox", new System.Numerics.Vector2(ImGui.GetWindowSize().X, 250), true);
            foreach (var line in RenderState.activeResMgr.NMSSceneFilesList)
            {
                if (_filter.PassFilter(line))
                {
                    if (ImGui.Selectable(line, line == selected_item)){
                        selected_item = line;
                    }
                }
                    
            }
            ImGui.EndChild();
            ImGui.Text(string.Format("Selected Item: {0}", selected_item));
            ImGui.SameLine();
            if (ImGui.Button("Open"))
                DialogFinished = true;
        }

        public void Clear()
        {
            _filter.Clear();
            selected_item = "";
            DialogFinished = false;
        }

        public bool isFinished()
        {
            return DialogFinished;
        }

        public void Destroy()
        {
            ImGuiNative.ImGuiTextFilter_destroy(_filter.NativePtr);
        }
    }


    public class ImGuiAboutWindow
    {
        Texture tex;

        public ImGuiAboutWindow()
        {
            //Load Logo Texture to the GPU
            byte[] imgData = CallBacks.getResource("ianm32logo_border.png");

            tex = new Texture();
            tex.textureInit(imgData, "ianm32logo_border.png");
        }

        private void TextCenter(string text, bool ishyperlink, string url = "")
        {
            float font_size = ImGui.GetFontSize() * text.Length / 2;
            ImGui.SameLine(
                ImGui.GetColumnWidth() / 2 -
                font_size + (font_size / 2)
            );

            ImGui.Text(text);

            if (ishyperlink)
            {
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                min.Y = max.Y;

                if (ImGui.IsItemHovered())
                {
                    if (ImGui.IsMouseClicked(0))
                    {
                        
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                        {
                            Verb = "open",
                            UseShellExecute = true
                        });
                    }
                        
                    
                    //System.Diagnostics.Process.Start("explorer.exe", new Uri(url).ToString());
                    ImGui.GetWindowDrawList().AddLine(min, max, 0x0010FFFF);
                }
                else
                {
                    ImGui.GetWindowDrawList().AddLine(min, max, 0xFFFFFFFF);
                }
            }
            
        }



        private void Text(string text, bool ishyperlink, string url = "")
        {
            ImGui.Text(text);

            if (ishyperlink)
            {
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                min.Y = max.Y;

                if (ImGui.IsItemHovered())
                {
                    if (ImGui.IsMouseClicked(0))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                        {
                            Verb = "open",
                            UseShellExecute = true
                        });
                    }
                    
                    ImGui.GetWindowDrawList().AddLine(min, max, 0x0010FFFF);
                }
                else
                {
                    ImGui.GetWindowDrawList().AddLine(min, max, 0xFFFFFFFF);
                }
            }
            
        }

        
        public void Draw()
        {
            
            //Assume that a Popup has begun
            ImGui.BeginChild("AboutWindow", ImGui.GetContentRegionAvail(), 
                true, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

            ImGui.Image(new IntPtr(tex.texID),
                        new System.Numerics.Vector2(256, 256),
                        new System.Numerics.Vector2(0, 0),
                        new System.Numerics.Vector2(1, 1));
            
            if (ImGui.BeginChildFrame(0, ImGui.GetContentRegionAvail(), ImGuiWindowFlags.NoBackground)){
                TextCenter("No Man's Sky Model Viewer", false);
                ImGui.NewLine();
                TextCenter(Util.getVersion(), false);
                ImGui.NewLine();
                ImGui.Columns(2, "Links", false);
                //Donation link
                TextCenter("Donate", true, Util.DonateLink);
                ImGui.NextColumn();
                TextCenter("Github", true, "https://github.com/gregkwaste/NMSMV");
                ImGui.NewLine();
                ImGui.Columns(1);
                TextCenter("Created by gregkwaste", false);
                ImGui.EndChildFrame();
            }
            ImGui.EndChild();

        }

        ~ImGuiAboutWindow()
        {
            tex.Dispose();
        }
    }

}
