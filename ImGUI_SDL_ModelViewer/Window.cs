using System;
using System.Threading;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using ImGuiNET;
using ImGuiHelper;
using OpenTK.Windowing.Common;
using MVCore;
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
        public Entity activeModel; //Active Model Reference
        public Queue<Entity> modelUpdateQueue = new();
        public List<Tuple<AnimComponent, AnimData>> activeAnimScenes = new();
        
        //Engine
        private Engine engine;

        //Workers
        private WorkThreadDispacher workDispatcher = new();
        private RequestHandler requestHandler = new();
        
        private Vector2i SceneViewSize = new();
        private bool isSceneViewActive = false;

        static private bool open_file_enabled = false;

        public Window() : base(GameWindowSettings.Default, 
            new NativeWindowSettings() { Size = new Vector2i(800, 600), APIVersion = new Version(4, 5) })
        {
            //Set Window Title
            Title = "NMSMV " + Util.getVersion();

            //Setup Logger
            Util.loggingSr = new StreamWriter("log.out");

            //SETUP THE Callbacks FOR THE MVCORE ENVIRONMENT
            Callbacks.updateStatus = Util.setStatus;
            Callbacks.showInfo = Util.showInfo;
            Callbacks.showError = Util.showError;
            Callbacks.Log = Util.Log;
            Callbacks.Assert = Util.Assert;
            Callbacks.getResource = Util.getResource;
            Callbacks.getBitMapResource = Util.getBitMapResource;
            Callbacks.getTextResource = Util.getTextResource;
            
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

            ImGuiManager.SetWindowRef(this);

            //OVERRIDE SETTINGS
            //FileUtils.dirpath = "I:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\PCBANKS";

            //Load Settings
            if (!File.Exists("settings.json"))
                ImGuiManager.ShowSettingsWindow();
            
            RenderState.settings = Settings.loadFromDisk();

            //Pass rendering settings to the Window
            RenderFrequency = RenderState.settings.renderSettings.FPS;
            UpdateFrequency = 60;

            //Initialize Engine backend
            engine = new Engine(this);
            engine.init(Size.X, Size.Y); //Initialize Engine
            RenderState.engineRef = engine; //Set reference to engine
            
            //Populate GLControl
            Scene scene = new Scene()
            {
                Name = "DEFAULT SCENE"
            };

            Locator test1 = new()
            {
                Name = "Test Locator 1"
            };
            
            scene.AddChild(test1);

            Locator test2 = new()
            {
                Name = "Test Locator 2"
            };

            scene.AddChild(test2);

            //Add default scene to the resource manager
            RenderState.activeResMgr.GLScenes["DEFAULT_SCENE"] = scene;

            //Force rootobject
            RenderState.rootObject = scene;
            modelUpdateQueue.Enqueue(scene);
            engine.renderSys.populate(scene);


            //Populate SceneGraphView
            ImGuiManager.PopulateSceneGraph(scene);

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

            
            Callbacks.Log("* Issuing NMS Archive Preload Request", LogVerbosityLevel.INFO);
            
            //Issue work request 
            ThreadRequest rq = new ThreadRequest();
            rq.arguments.Add("NMSmanifest");
            rq.arguments.Add(Path.Combine(RenderState.settings.GameDir, "PCBANKS"));
            rq.arguments.Add(RenderState.activeResMgr);
            rq.type = THREAD_REQUEST_TYPE.WINDOW_LOAD_NMS_ARCHIVES;
            requestHandler.AddRequest(ref rq); 
            workDispatcher.sendRequest(ref rq); //Generate worker
            
            //Basic Initialization of ImGui
            ImGuiManager.InitImGUI();
    
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
            engine.handleRequests(); //Handle engine requests
            handleRequests(); //Handle window requests
            
            if (engine.rt_State == EngineRenderingState.ACTIVE)
            {
                //Capture Keyboard Presses
                engine.UpdateInput(e.Time, isSceneViewActive);
                frameUpdate(e.Time);
            }
                
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            
            _controller.Update(this, (float) e.Time);
            
            //Per Frame System Updates
            engine.transformSys.Update(e.Time);

            Camera.UpdateCameraDirectionalVectors(RenderState.activeCam);

            //Console.WriteLine("Rendering Frame");
            GL.ClearColor(new Color4(5, 5, 5, 255));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            //Render Shit
            if (engine.rt_State == EngineRenderingState.ACTIVE)
            {
                //Callbacks.Log("* CONTROL : STARTING FRAME UPDATE", LogVerbosityLevel.DEBUG);
                //Callbacks.Log("* CONTROL : FRAME UPDATED", LogVerbosityLevel.DEBUG);
                //Callbacks.Log("* CONTROL : STARTING FRAME RENDER", LogVerbosityLevel.DEBUG);

                engine.renderSys.testrender(); //Render Everything

                //Callbacks.Log("* CONTROL : FRAME RENDERED", LogVerbosityLevel.DEBUG);
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
            //Pass Global rendering settings
            VSync = RenderState.settings.renderSettings.UseVSync ? VSyncMode.On : VSyncMode.Off;
            RenderFrequency = RenderState.settings.renderSettings.FPS;
            
            //Gizmo Picking
            //Send picking request
            //Make new request
            activeGizmo = null;
            if (RenderState.settings.viewSettings.ViewGizmos)
            {
                ThreadRequest req = new()
                {
                    type = THREAD_REQUEST_TYPE.ENGINE_GIZMO_PICKING
                };
                req.arguments.Clear();
                req.arguments.Add(activeGizmo);
                req.arguments.Add(mouseState.Position);
                engine.SendRequest(ref req);
            }
            
            //Calculate new Camera State
            Camera.CalculateNextCameraState(RenderState.activeCam, engine.targetCameraPos, dt);
            
            RenderState.activeCam.aspect = (float)SceneViewSize.X / SceneViewSize.Y;
            RenderState.activeCam.updateViewMatrix();
            
            //Set time to the renderManager
            engine.renderSys.progressTime(dt);
            
            //Reset Stats
            RenderStats.occludedNum = 0;

            //Update moving queue
            //TODO: Let the transformation system do that
            //while (modelUpdateQueue.Count > 0)
            //{
            //    Entity m = modelUpdateQueue.Dequeue();
            //    m.update();
            //}

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
            if (RenderState.settings.viewSettings.EmulateActions)
            {
                engine.actionSys.Update((float)(1000 * dt)); //time is expected in ms
            }

            //Progress animations
            if (RenderState.settings.renderSettings.ToggleAnimations)
            {
                engine.animationSys.Update((float)(1000 * dt)); //time is expected in ms
            }

            //Camera & Light Positions
            //Update common transforms
            

            //Apply extra viewport rotation
            Matrix4 Rotx = Matrix4.CreateRotationX(MathUtils.radians(RenderState.rotAngles.X));
            Matrix4 Roty = Matrix4.CreateRotationY(MathUtils.radians(RenderState.rotAngles.Y));
            Matrix4 Rotz = Matrix4.CreateRotationZ(MathUtils.radians(RenderState.rotAngles.Z));
            RenderState.rotMat = Rotz * Rotx * Roty;
            //RenderState.rotMat = Matrix4.Identity;

            

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
            
        private void OpenFile(string filename, bool testScene, int testSceneID)
        {
            Log("Importing " + filename, LogVerbosityLevel.INFO);
            ThreadRequest req;
            
            //Pause renderer
            req = new()
            {
                type = THREAD_REQUEST_TYPE.ENGINE_PAUSE_RENDER
            };
            req.arguments.Clear();

            //Send request to engine
            engine.SendRequest(ref req);

            waitForRequest(ref req);

            RenderState.rootObject?.Dispose();

            if (testScene)
                addTestScene(testSceneID);
            else
                addScene(filename);
            
            //Populate 
            Util.setStatus("Creating SceneGraph...");

            ImGuiManager.PopulateSceneGraph(RenderState.rootObject);

            //Add to UI
            Util.setStatus("Ready");

            //Generate Request for resuming rendering
            ThreadRequest req2 = new()
            {
                type = THREAD_REQUEST_TYPE.ENGINE_RESUME_RENDER
            };

            engine.SendRequest(ref req2);
        
        }

        private void Log(string msg, LogVerbosityLevel lvl)
        {
            Callbacks.Log("* WINDOW : " + msg, lvl);
        }

        public void SendRequest(ref ThreadRequest r)
        {
            requestHandler.AddRequest(ref r);
        }

        public void handleRequests()
        {
            if (requestHandler.HasOpenRequests())
            {
                ThreadRequest req = requestHandler.Peek();
                Log("Peeking Request " + req.type, LogVerbosityLevel.HIDEBUG);
                
                //Do stuff with requests that need extra work to get started
                if (req.status == THREAD_REQUEST_STATUS.NULL)
                {
                    switch (req.type)
                    {
                        case THREAD_REQUEST_TYPE.WINDOW_LOAD_NMS_ARCHIVES:
                            workDispatcher.sendRequest(ref req);
                            break;
                        case THREAD_REQUEST_TYPE.WINDOW_OPEN_FILE:
                            string filename = req.arguments[0] as string;
                            OpenFile(filename, false, 0);
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        default:
                            break; 
                    }
                }
                else if (req.status != THREAD_REQUEST_STATUS.FINISHED)
                    return;
                
                //Finalize finished requests
                switch (req.type)
                {
                    case THREAD_REQUEST_TYPE.WINDOW_LOAD_NMS_ARCHIVES:
                        open_file_enabled = true;
                        break;
                    case THREAD_REQUEST_TYPE.ENGINE_TERMINATE_RENDER:
                        Close();
                        break;
                }

                //At this point the peeked request is finished so its safe to pop it from the queue
                requestHandler.Fetch(); 
            }
        }
        
        public void waitForRequest(ref ThreadRequest req)
        {
            while (true)
            {
                engine.handleRequests(); //Force engine to handle requests
                lock (req)
                {
                    if (req.status == THREAD_REQUEST_STATUS.FINISHED)
                        return;
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
                type = THREAD_REQUEST_TYPE.ENGINE_OPEN_TEST_SCENE
            };
            req1.arguments.Add(sceneID);

            engine.SendRequest(ref req1);

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
                type = THREAD_REQUEST_TYPE.ENGINE_OPEN_NEW_SCENE
            };
            req1.arguments.Clear();
            req1.arguments.Add(filename);

            engine.SendRequest(ref req1);
            
            //Wait for requests to finish before return
            waitForRequest(ref req1);
            
            //find Animation Capable nodes
            activeModel = null; //TODO: Fix that with the gizmos
            findAnimScenes(RenderState.rootObject); //Repopulate animScenes
            findActionScenes(RenderState.rootObject); //Re-populate actionSystem
        }
        
        public void findAnimScenes(Entity node)
        {
            if (node.HasComponent<AnimComponent>())
            {
                engine.animationSys.Add(node);
            }

            foreach (Entity child in node.Children)
                findAnimScenes(child);
        }

        public void findActionScenes(Entity node)
        {
            if (node.HasComponent<TriggerActionComponent>())
            {
                //Find SceneGraphNode
                SceneGraphNode n = engine.sceneManagementSys.FindEntitySceneGraphNode(node);
                engine.actionSys.Add(n);
            }
            
            foreach (Model child in node.Children)
                findActionScenes(child);
        }

        public void DrawUI()
        {
            //Enable docking in main view
            ImGuiDockNodeFlags dockspace_flags = ImGuiDockNodeFlags.None;
            dockspace_flags |= ImGuiDockNodeFlags.PassthruCentralNode;

            ImGuiWindowFlags window_flags = ImGuiWindowFlags.NoBackground |
                                            ImGuiWindowFlags.NoCollapse |
                                            ImGuiWindowFlags.NoResize |
                                            ImGuiWindowFlags.NoDocking;

            ImGuiViewportPtr vp = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(vp.GetWorkPos());
            ImGui.SetNextWindowSize(vp.GetWorkSize());
            ImGui.SetNextWindowViewport(vp.ID);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, 0.0f);

            int statusBarHeight = (int) (1.75f * ImGui.CalcTextSize("Status").Y);

            //DockSpace
            bool dockspace_open = true;
            ImGui.Begin("DockSpaceDemo", ref dockspace_open, window_flags);

            ImGui.PopStyleVar(2);
            
            uint dockSpaceID = ImGui.GetID("MainDockSpace");
            //System.Numerics.Vector2 dockSpaceSize = vp.GetWorkSize();
            System.Numerics.Vector2 dockSpaceSize = new(0.0f, -statusBarHeight);
            ImGui.DockSpace(dockSpaceID,
                dockSpaceSize, dockspace_flags);

            //Main Menu
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Open", "Ctrl + O", false, open_file_enabled))
                    {
                        ImGuiManager.ShowOpenFileDialog();
                    }

                    if (ImGui.MenuItem("Open from PAK", "", false, open_file_enabled))
                    {
                        ImGuiManager.ShowOpenFileDialogPak();
                    }

                    if (ImGui.MenuItem("Update LibMBIN"))
                    {
                        ImGuiManager.ShowUpdateLibMBINDialog();
                    }

                    if (ImGui.MenuItem("Settings"))
                    {
                        ImGuiManager.ShowSettingsWindow();
                    }

                    if (ImGui.MenuItem("Close", "Ctrl + Q"))
                    {
                        //Stop the renderer
                        ThreadRequest req = new ThreadRequest();
                        req.type = THREAD_REQUEST_TYPE.ENGINE_TERMINATE_RENDER;
                        engine.SendRequest(ref req);

                        //Send event to close the window
                        ThreadRequest req1 = new ThreadRequest();
                        req1.type = THREAD_REQUEST_TYPE.WINDOW_CLOSE;
                        requestHandler.AddRequest(ref req1);
                        
                    }

                    ImGui.EndMenu();
                }

                if (ImGui.MenuItem("About"))
                {
                    ImGuiManager.ShowAboutWindow();
                }

                ImGui.EndMenuBar();
            }

            //Generate StatusBar
            //StatusBar

            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, Size.Y - statusBarHeight));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(Size.X, statusBarHeight));
            
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            

            if (ImGui.Begin("StatusBar", ImGuiWindowFlags.NoMove | 
                                         ImGuiWindowFlags.NoDocking |
                                         ImGuiWindowFlags.NoDecoration))
            {
                ImGui.Columns(2);
                ImGui.SetCursorPosY(0.25f * statusBarHeight);
                ImGui.Text(status_string);
                ImGui.NextColumn();
                string text = "Created by gregkwaste";
                ImGui.SetCursorPosY(0.25f * statusBarHeight);
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize(text).X
                    - ImGui.GetScrollX() - 2 * ImGui.GetStyle().ItemSpacing.X);
                
                ImGui.Text(text);
                ImGui.End();
            }
            ImGui.PopStyleVar();

            ImGui.End();

            ImGui.SetNextWindowDockID(dockSpaceID, ImGuiCond.Once);
            ImGui.SetCursorPosX(0.0f);
            bool main_view = true;

            //Scene Render
            bool scene_view = true;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(0.0f, 0.0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, new System.Numerics.Vector2(0.0f, 0.0f));

            //Cause of ImguiNET that does not yet support DockBuilder. The main Viewport will be docked to the main window.
            //All other windows will be separate.

            if (ImGui.Begin("Scene", ref scene_view, ImGuiWindowFlags.NoScrollbar))
            {
                //Update RenderSize
                System.Numerics.Vector2 csize = ImGui.GetContentRegionAvail();
                Vector2i csizetk = new Vector2i((int) csize.X,
                                               (int) csize.Y);
                ImGui.Image(new IntPtr(engine.renderSys.getRenderFBO().channels[0]),
                                csize,
                                new System.Numerics.Vector2(0.0f, 1.0f),
                                new System.Numerics.Vector2(1.0f, 0.0f));

                isSceneViewActive = ImGui.IsItemHovered();
                
                if (csizetk != SceneViewSize)
                {
                    SceneViewSize = csizetk;
                    engine.renderSys.resize(csizetk);
                }

                ImGui.PopStyleVar();
                ImGui.End();
            }

            //SideBar
            if (ImGui.Begin("SideBar", ref main_view))
            {
                ImGui.BeginGroup();

                ImGui.Text("SceneGraph");
                ImGuiManager.DrawSceneGraph();
                ImGui.EndGroup();
                //ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMin(), 0x00FFFFFF);

                ImGui.Separator();
                
                ////Draw Tab Controls
                if (ImGui.BeginTabBar("TabControl1", ImGuiTabBarFlags.None))
                {
                    if (ImGui.BeginTabItem("Object Info"))
                    {
                        ImGuiManager.DrawObjectInfoViewer();
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

#if (DEBUG)
                    if (ImGui.BeginTabItem("Test Options"))
                    {
                        ImGui.DragFloat("Test Option 1", ref RenderState.settings.renderSettings.testOpt1);
                        ImGui.DragFloat("Test Option 2", ref RenderState.settings.renderSettings.testOpt2);
                        ImGui.DragFloat("Test Option 3", ref RenderState.settings.renderSettings.testOpt3);
                        ImGui.EndTabItem();
                    }
#endif
                    ImGui.EndTabBar();
                }

                ImGui.Separator();

                if (ImGui.BeginTabBar("TabControl2", ImGuiTabBarFlags.None))
                {
                    if (ImGui.BeginTabItem("Camera"))
                    {
                        //Camera Settings
                        ImGui.BeginGroup();
                        ImGui.TextColored(ImGuiManager.DarkBlue, "Camera Settings");
                        ImGui.SliderFloat("FOV", ref RenderState.activeCam.fov, 15.0f, 100.0f);
                        ImGui.SliderFloat("Sensitivity", ref RenderState.activeCam.Sensitivity, 0.1f, 10.0f);
                        ImGui.InputFloat("MovementSpeed", ref RenderState.activeCam.Speed, 1.0f, 500000.0f);
                        ImGui.SliderFloat("zNear", ref RenderState.activeCam.zNear, 0.01f, 1.0f);
                        ImGui.SliderFloat("zFar", ref RenderState.activeCam.zFar, 101.0f, 30000.0f);

                        if (ImGui.Button("Reset Camera"))
                        {
                            RenderState.activeCam.Position = new Vector3(0.0f);
                        }
                        
                        ImGui.SameLine();

                        if (ImGui.Button("Reset Scene Rotation"))
                        {
                            //TODO :Maybe enclose all settings in a function
                            RenderState.activeCam.pitch = 0.0f;
                            RenderState.activeCam.yaw = -90.0f;
                        }

                        ImGui.EndGroup();
                        
                        ImGui.Separator();
                        ImGui.BeginGroup();
                        ImGui.TextColored(ImGuiManager.DarkBlue, "Camera Controls");

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
                        ImGui.Checkbox("Show Lights", ref RenderState.settings.viewSettings.ViewLights);
                        ImGui.Checkbox("Show Light Volumes", ref RenderState.settings.viewSettings.ViewLightVolumes);
                        ImGui.Checkbox("Show Joints", ref RenderState.settings.viewSettings.ViewJoints);
                        ImGui.Checkbox("Show Locators", ref RenderState.settings.viewSettings.ViewLocators);
                        ImGui.Checkbox("Show Collisions", ref RenderState.settings.viewSettings.ViewCollisions);
                        ImGui.Checkbox("Show Bounding Hulls", ref RenderState.settings.viewSettings.ViewBoundHulls);
                        ImGui.Checkbox("Emulate Actions", ref RenderState.settings.viewSettings.EmulateActions);

                        ImGui.EndTabItem();
                    }
                
                    if (ImGui.BeginTabItem("Rendering Options"))
                    {
                        ImGui.Checkbox("Use Textures", ref RenderState.settings.renderSettings.UseTextures);
                        ImGui.Checkbox("Use Lighting", ref RenderState.settings.renderSettings.UseLighting);
                        ImGui.Checkbox("Use VSYNC", ref RenderState.settings.renderSettings.UseVSync);
                        ImGui.Checkbox("Show Animations", ref RenderState.settings.renderSettings.ToggleAnimations);
                        ImGui.Checkbox("Wireframe", ref RenderState.settings.renderSettings.RenderWireFrame);
                        ImGui.Checkbox("FXAA", ref RenderState.settings.renderSettings.UseFXAA);
                        ImGui.Checkbox("Bloom", ref RenderState.settings.renderSettings.UseBLOOM);
                        ImGui.Checkbox("LOD Filtering", ref RenderState.settings.renderSettings.LODFiltering);

                        ImGui.InputInt("FPS", ref RenderState.settings.renderSettings.FPS);
                        ImGui.InputFloat("HDR Exposure", ref RenderState.settings.renderSettings.HDRExposure);

                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.End();
            }

            ImGuiManager.ProcessModals(this, current_file_path);

            //Debugging Information
            if (ImGui.Begin("Statistics"))
            {
                ImGui.Text(string.Format("FPS : {0, 3:F1}", RenderStats.fpsCount));
                ImGui.Text(string.Format("VertexCount : {0}", RenderStats.vertNum));
                ImGui.Text(string.Format("TrisCount : {0}", RenderStats.trisNum));
                ImGui.End();
            }



        }
        

    }




}
