using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using OpenTK;
using OpenTK.Input;
using OpenTK.Graphics;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using MVCore;
using MVCore.Systems;
using MVCore.Common;
using MVCore.Input;
using MVCore.Primitives;
using MVCore.Utils;
using MVCore.Plugins;
using System.Timers;
using GLSLHelper;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;
using System.IO;
using System.Reflection;
using Font = MVCore.Text.Font;
using Image = System.Drawing.Image;

namespace MVCore
{
    public enum EngineRenderingState
    {
        EXIT = 0x0,
        UNINITIALIZED,
        PAUSED,
        ACTIVE
    }

    public class Engine : EngineSystem
    {
        //Init Systems
        private EntityRegistrySystem registrySys;
        public TransformationSystem transformSys;
        public ActionSystem actionSys;
        public AnimationSystem animationSys;
        public SceneManagementSystem sceneMgmtSys;
        public RenderingSystem renderSys; //TODO: Try to make it private. Noone should have a reason to access it
        private readonly RequestHandler reqHandler;
        
        private Dictionary<EngineSystemEnum, EngineSystem> _engineSystemMap = new(); //TODO fill up

        //Rendering 
        public EngineRenderingState rt_State;

        //Input
        public BaseGamepadHandler gpHandler;

        private KeyboardState ActiveKbState;
        private MouseState ActiveMsState;
        private Queue<KeyboardState> kbStates = new();
        private Queue<MouseState> MsStates = new();
        
        //Camera Stuff
        public CameraPos targetCameraPos;
        public Vector2 prevMousePos;

        //Event Handlers
        public event EventHandler<AddSceneEventData> AddSceneEventHandler;
    
        //Plugin List
        public Dictionary<string, PluginBase> Plugins = new();

        //Use public variables for now because getters/setters are so not worth it for our purpose
        public float light_angle_y = 0.0f;
        public float light_angle_x = 0.0f;
        public float light_distance = 5.0f;
        public float light_intensity = 1.0f;
        public float scale = 1.0f;

        public Engine(NativeWindow win) : base(EngineSystemEnum.CORE_SYSTEM)
        {
            //TODO : I don't like using the win as an argument but
            //seems like I can't initialize empmty keyboard, mouse states
            ActiveKbState = win.KeyboardState;
            ActiveMsState = win.MouseState;
            prevMousePos = ActiveMsState.Position;
            
            //gpHandler = new PS4GamePadHandler(0); //TODO: Add support for PS4 controller
            reqHandler = new RequestHandler();
            
            //Systems Init
            renderSys = new RenderingSystem(); //Init renderManager of the engine
            registrySys = new EntityRegistrySystem();
            actionSys = new ActionSystem();
            animationSys = new AnimationSystem();
            transformSys = new TransformationSystem();
            sceneMgmtSys = new SceneManagementSystem();
            
            
            renderSys.SetEngine(this);
            registrySys.SetEngine(this);
            actionSys.SetEngine(this);
            animationSys.SetEngine(this);
            transformSys.SetEngine(this);
            sceneMgmtSys.SetEngine(this);
            
            //Set Start Status
            rt_State = EngineRenderingState.UNINITIALIZED;
            
            //Try to load plugins
            foreach (string filename in Directory.GetFiles("Plugins"))
            {
                if (!filename.EndsWith(("dll")))
                    continue;
                //Load Assembly
                try
                {
                    Assembly a = Assembly.LoadFile(Path.GetFullPath(filename));
                    
                    //Try to find the type the derived plugin class
                    foreach (Type t in a.GetTypes())
                    {
                        if (t.IsSubclassOf(typeof(PluginBase)))
                        {
                            Console.WriteLine("Plugin class detected! {0}", t.Name);
                            object c = Activator.CreateInstance(t, new object[] {this});
                            Plugins[Path.GetFileName(filename)] = c as PluginBase;
                            //Call Dll initializers
                            t.GetMethod("OnLoad").Invoke(c, new object[] {});
                            break;
                        }
                    }
                    
                    
                }
                catch (Exception ex)
                {
                    Log("Error during loading of plugin " + filename, LogVerbosityLevel.INFO);
                    Log("Exception type " + ex.Data, LogVerbosityLevel.INFO);
                }
                
            }

            LoadDefaultResources();
            
        }
        
        ~Engine()
        {
            Log("Goodbye!", LogVerbosityLevel.INFO);
        }

        public void LoadDefaultResources()
        {
            //Iterate in local folder and load existing resources
            string fontPath = "Fonts";

            if (Directory.Exists(fontPath))
            {
                foreach (string fontFileName in Directory.GetFiles(fontPath))
                {
                    string ext = Path.GetExtension(fontFileName).ToUpper();
                    if (ext == "FNT")
                    {
                        string fontAtlasName = fontFileName;
                        Path.ChangeExtension(fontAtlasName, "png");

                        if (File.Exists(Path.Combine(fontPath, fontAtlasName)))
                        {
                            AddFont(Path.Combine(fontPath, fontFileName),
                                Path.Combine(fontPath, fontAtlasName));
                        }
                        else
                        {
                            Log(string.Format("Cannot load font {0}. Missing font atas", fontFileName), 
                                LogVerbosityLevel.WARNING);
                        }
                    }
                }    
            }
            
        }

        public Font AddFont(string fontPath, string fontAtlas)
        {
            byte[] fontData = File.ReadAllBytes(fontPath);
            byte[] fontAtlasData = File.ReadAllBytes(fontAtlas);
            MemoryStream ms = new MemoryStream(fontAtlasData);
            Bitmap FontAtlas = new(ms);
            
            Font f = new Font(fontData, FontAtlas, 1);
            renderSys.FontMgr.addFont(f);
            return f;
        }

        public void RegisterEntity(Entity e, bool controllable = false, bool isDynamic = false)
        {
            //Add Entity to main registry
            if (registrySys.RegisterEntity(e))
            {
                if (e.HasComponent<TransformComponent>())
                    transformSys.RegisterEntity(e, controllable, isDynamic);

                //TODO Register to the rest systems if necessary
            }
        }

        public void RegisterSceneGraph(SceneGraphNode node)
        {
            RegisterEntity(node);
            foreach (SceneGraphNode child in node.Children)
                RegisterSceneGraph(child);
        }
        
        public Scene CreateScene()
        {
            return sceneMgmtSys.CreateScene();
        }

        #region ResourceManager

        public void InitializeResources()
        {
            AddDefaultShaders();
        }

        private void AddDefaultShaders()
        {
            //Local function
            void WalkDirectory(DirectoryInfo dir)
            {
                FileInfo[] files = dir.GetFiles("*.glsl");
                DirectoryInfo[] subdirs = dir.GetDirectories();

                if (subdirs.Length != 0)
                {
                    foreach (DirectoryInfo subdir in subdirs)
                        WalkDirectory(subdir);
                }

                if (files.Length != 0)
                {
                    foreach (FileInfo file in files)
                    {
                        //Add source file
                        Console.WriteLine("Working On {0}", file.FullName);
                        if (GetShaderSourceByFilePath(file.FullName) == null)
                        {
                            //Construction includes registration
                            GLSLShaderSource ss = new(file.FullName, true); 
                        }
                    }
                }
            }

            DirectoryInfo dirInfo = new("Shaders");
            WalkDirectory(dirInfo);

            //Now that all sources are loaded we can start processing all of them
            //Step 1: Process Shaders
            List<Entity> sourceList = GetEntityTypeList(EntityType.ShaderSource);
            int i = 0;
            while (i < sourceList.Count) //This way can account for new entries 
            {
                ((GLSLShaderSource) sourceList[i]).Process();
                i++;
            }
            
            //Step 2: Resolve Shaders
            i = 0;
            while (i < sourceList.Count)
            {
                ((GLSLShaderSource) sourceList[i]).Resolve();
                i++;
            }
        }

        #endregion

        //Asset Setters
        public void AddTexture(Texture tex)
        {
            renderSys.TextureMgr.AddTexture(tex);
        }

        //Asset Getter
        public Texture GetTexture(string name)
        {
            return renderSys.TextureMgr.Get(name);
        }

        public GLInstancedMesh GetPrimitiveMesh(string name)
        {
            return renderSys.GeometryMgr.GetPrimitiveMesh(name);
        }

        public MeshMaterial GetMaterialByName(string name)
        {
            return renderSys.MaterialMgr.GetByName(name);
        }

        public SceneGraphNode GetSceneNodeByName(string name)
        {
            return registrySys.GetEntityTypeList(EntityType.SceneNode).Find(x=>((SceneGraphNode) x).Name == name) as SceneGraphNode;
        }
        
        public SceneGraphNode GetSceneNodeByNameType(SceneNodeType type, string name)
        {
            EntityType etype = EntityType.SceneNode;
            switch (type)
            {
                case SceneNodeType.LOCATOR:
                    etype = EntityType.SceneNodeLocator;
                    break;
                case SceneNodeType.MODEL:
                    etype = EntityType.SceneNodeModel;
                    break;
                case SceneNodeType.MESH:
                    etype = EntityType.SceneNodeMesh;
                    break;
                case SceneNodeType.LIGHT:
                    etype = EntityType.SceneNodeLight;
                    break;
            }
            return registrySys.GetEntityTypeList(etype).Find(x=>((SceneGraphNode) x).Name == name && ((SceneGraphNode) x).Type == type) as SceneGraphNode;
        }

        public GLSLShaderSource GetShaderSourceByFilePath(string path)
        {
            return registrySys.GetEntityTypeList(EntityType.ShaderSource)
                .Find(x => ((GLSLShaderSource)x).SourceFilePath == path) as GLSLShaderSource;
        }

        public GLSLShaderConfig GetShaderByHash(int hash)
        {
            return registrySys.GetEntityTypeList(EntityType.Shader)
                .Find(x => ((GLSLShaderConfig) x).Hash == hash) as GLSLShaderConfig;
        }

        public GLSLShaderConfig GetShaderByType(SHADER_TYPE typ)
        {
            return renderSys.ShaderMgr.GetGenericShader(typ);
        }

        public int GetEntityListCount(EntityType type)
        {
            return registrySys.GetEntityTypeList(type).Count;
        }

        public int GetShaderSourceCount()
        {
            return GetEntityListCount(EntityType.ShaderSource);
        }

        public int GetLightCount()
        {
            return GetEntityListCount(EntityType.LightComponent);
        }

        public List<Entity> GetEntityTypeList(EntityType type)
        {
            return registrySys.GetEntityTypeList(type);
        }
        
    

        public void init(int width, int height)
        {
            //Initialize Resource Manager
            InitializeResources();

            //Add Camera
            Camera cam = new(90, -1, 0, true)
            {
                isActive = false
            };

            //Add Necessary Components to Camera
            TransformationSystem.AddTransformComponentToEntity(cam);
            RegisterEntity(cam, true, true); //Register Entity
            
            //Set global reference to cam
            RenderState.activeCam = cam;

            //Set Camera Initial State
            TransformController tcontroller = transformSys.GetEntityTransformController(cam);
            tcontroller.AddFutureState(new Vector3(), Quaternion.FromEulerAngles(0.0f, -3.14f/2.0f, 0.0f), new Vector3(1.0f));

            //Initialize the render manager
            renderSys.init(width, height);
            rt_State = EngineRenderingState.ACTIVE;

            Log("Initialized", LogVerbosityLevel.INFO);
        }
        
        

        public void handleRequests()
        {
            //Log(string.Format(" {0} Open Requests ", reqHandler.getOpenRequestNum()), LogVerbosityLevel.HIDEBUG);
            if (reqHandler.HasOpenRequests())
            {
                ThreadRequest req = reqHandler.Fetch();
                THREAD_REQUEST_STATUS req_status = THREAD_REQUEST_STATUS.FINISHED;
                Log("Handling Request " + req.Type, LogVerbosityLevel.HIDEBUG);

                lock (req)
                {
                    switch (req.Type)
                    {
                        case THREAD_REQUEST_TYPE.ENGINE_QUERY_GLCONTROL_STATUS:
                            if (rt_State == EngineRenderingState.UNINITIALIZED)
                                req_status = THREAD_REQUEST_STATUS.ACTIVE;
                            else
                                req_status = THREAD_REQUEST_STATUS.FINISHED;
                                //At this point the renderer is up and running
                            break;
#if DEBUG               
                        
#endif
                        case THREAD_REQUEST_TYPE.ENGINE_CHANGE_MODEL_PARENT:
                            throw new Exception("Not Implemented");
                            /*
                            Model source = (Model) req.arguments[0];
                            Model target = (Model) req.arguments[1];

                            System.Windows.Application.Current.Dispatcher.Invoke((System.Action)(() =>
                            {
                                if (source.parent != null)
                                    source.parent.Children.Remove(source);

                                //Add to target node
                                source.parent = target;
                                target.Children.Add(source);
                            }));
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                        case THREAD_REQUEST_TYPE.ENGINE_UPDATE_SCENE:
                            throw new Exception("Not Implemented Yet!");
                        case THREAD_REQUEST_TYPE.ENGINE_MOUSEPOSITION_INFO:
                            Vector4[] t = (Vector4[])req.arguments[2];
                            renderSys.getMousePosInfo((int)req.arguments[0], (int)req.arguments[1],
                                ref t);
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.ENGINE_RESIZE_VIEWPORT:
                            rt_ResizeViewport((int)req.arguments[0], (int)req.arguments[1]);
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.ENGINE_MODIFY_SHADER:
                            GLShaderHelper.modifyShader((GLSLShaderConfig)req.arguments[0],
                                         (GLSLShaderSource) req.arguments[1]);
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        */

                        case THREAD_REQUEST_TYPE.ENGINE_GIZMO_PICKING:
                            throw new Exception("not yet implemented");
                        case THREAD_REQUEST_TYPE.ENGINE_TERMINATE_RENDER:
                            rt_State = EngineRenderingState.EXIT;
                            CleanUp(); //Free Resources
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.ENGINE_PAUSE_RENDER:
                            rt_State = EngineRenderingState.PAUSED;
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.ENGINE_RESUME_RENDER:
                            rt_State = EngineRenderingState.ACTIVE;
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.NULL: //Is this ever used?
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        default:
                            Log(string.Format("Not supported Request {0}", req.Type), LogVerbosityLevel.HIDEBUG);
                            break;
                    }
                }

                req.Status = req_status;
                Log("Request Handled", LogVerbosityLevel.HIDEBUG);
            }
        }

        //Main Rendering Routines

        private void rt_ResizeViewport(int w, int h)
        {
            renderSys.resize(w, h);
        }

#if DEBUG

        private void rt_SpecularTestScene()
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            //Import.NMS.Palettes.set_palleteColors();

            //Clear Systems
            actionSys.CleanUp();
            animationSys.CleanUp();

            //Clear Resources
            //ModelProcGen.procDecisions.Clear();

            //Clear RenderStats
            RenderStats.ClearStats();

            //Stop animation if on
            bool animToggleStatus = RenderState.settings.renderSettings.ToggleAnimations;
            RenderState.settings.renderSettings.ToggleAnimations = false;

            //Setup new object
            SceneGraphNode scene = new(SceneNodeType.MODEL)
            {
                Name = "DEFAULT SCENE"
            };

            //Add Lights
            SceneGraphNode l = CreateLightNode("Light 1", 100.0f,
                ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);

            TransformationSystem.SetEntityLocation(l, new Vector3(0.2f, 0.2f, -2.0f));
            RegisterEntity(l);
            scene.Children.Add(l);

            SceneGraphNode l1 = CreateLightNode("Light 2", 100.0f,
                ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);

            TransformationSystem.SetEntityLocation(l1, new Vector3(0.2f, -0.2f, -2.0f));
            RegisterEntity(l1);
            scene.Children.Add(l1);

            SceneGraphNode l2 = CreateLightNode("Light 3", 100.0f,
                ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);

            TransformationSystem.SetEntityLocation(l2, new Vector3(-0.2f, 0.2f, -2.0f));
            RegisterEntity(l2);
            scene.Children.Add(l2);

            SceneGraphNode l3 = CreateLightNode("Light 4", 100.0f,
                ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);

            TransformationSystem.SetEntityLocation(l3, new Vector3(-0.2f, -0.2f, -2.0f));
            RegisterEntity(l3);
            scene.Children.Add(l3);

            //Generate a Sphere and center it in the scene

            SceneGraphNode sphere = new(SceneNodeType.MESH)
            {
                Name = "Test Sphere"
            };
            
            sphere.SetParent(scene);


            //Add Mesh Component
            int bands = 80;
            MeshComponent mc = new()
            {
                MetaData = new()
                {
                    BatchCount = bands * bands * 6,
                    BatchStartGraphics = 0,
                    VertrStartGraphics = 0,
                    VertrEndGraphics = (bands + 1) * (bands + 1) - 1,
                    IndicesLength = DrawElementsType.UnsignedInt
                }
            };
            
            mc.MeshVao = new GLInstancedMesh(mc.MetaData);
            mc.MeshVao.type = SceneNodeType.MESH;
            mc.MeshVao.vao = (new Sphere(new Vector3(), 2.0f, 40)).getVAO();
            
            //Sphere Material
            MeshMaterial mat = new();
            mat.Name = "default_scn";
            
            Uniform uf = new();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new(1.0f,0.0f,0.0f,1.0f);
            mat.Uniforms.Add(uf);

            uf = new();
            uf.Name = "gMaterialParamsVec4";
            uf.Values = new(0.15f, 0.0f, 0.2f, 0.0f);
            //x: roughness
            //z: metallic
            mat.Uniforms.Add(uf);
            mat.CompileShader("Shaders/Simple_VS.glsl", "Shaders/Simple_FS.glsl");
            
            RegisterEntity(mat);
            
            scene.Children.Add(sphere);

            //Explicitly add default light to the rootObject
            scene.Children.Add(l);

            //Populate RenderManager
            renderSys.populate(null);

            scene.IsSelected = true;
            //RenderState.activeModel = root; //Set the new scene as the new activeModel

            //Restart anim worker if it was active
            RenderState.settings.renderSettings.ToggleAnimations = animToggleStatus;

        }

        private void rt_addTestScene(int sceneID)
        {
            
            switch (sceneID)
            {
                case 0:
                    rt_SpecularTestScene();
                    break;
                default:
                    Console.WriteLine("Non Implemented Test Scene");
                    break;
            }

        }

#endif

        private void rt_addScene(SceneGraphNode node)
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            //Import.NMS.Palettes.set_palleteColors();

            //Clear Systems
            actionSys.CleanUp();
            animationSys.CleanUp();

            //Clear Resources
            //ModelProcGen.procDecisions.Clear();
            
            RenderState.itemCounter = 0;
            
            //Clear RenderStats
            RenderStats.ClearStats();

            //Stop animation if on
            bool animToggleStatus = RenderState.settings.renderSettings.ToggleAnimations;
            RenderState.settings.renderSettings.ToggleAnimations = false;
            
            //Register Scene to Entity Registry
            
            //Explicitly add default light to the rootObject
            SceneGraphNode l = CreateLightNode();
            node.AddChild(l);
            
            RegisterSceneGraph(node);
            
            //root.update(); //Refresh all transforms
            //root.setupSkinMatrixArrays();

            //Populate RenderManager
            renderSys.populate(null);

            //Restart anim worker if it was active
            RenderState.settings.renderSettings.ToggleAnimations = animToggleStatus;
        }
        
        
        
        
        public void SendRequest(ref ThreadRequest r)
        {
            reqHandler.AddRequest(ref r);
        }

        public override void OnFrameUpdate(double dt)
        {
            //Update input
            UpdateInput();
            
            handleRequests(); //Handle engine requests

            //Update systems
            transformSys.OnFrameUpdate(dt);
            sceneMgmtSys.OnFrameUpdate(dt);
            
            //Reset Stats
            RenderStats.occludedNum = 0;

            
            //Enable Action System
            if (RenderState.settings.viewSettings.EmulateActions)
                actionSys.OnFrameUpdate(dt); 
            //Enable Animation System
            if (RenderState.settings.renderSettings.ToggleAnimations)
                animationSys.OnFrameUpdate(dt);
            

            //Camera & Light Positions
            //Update common transforms

            
            //Apply extra viewport rotation
            Matrix4 Rotx = Matrix4.CreateRotationX(MathUtils.radians(RenderState.rotAngles.X));
            Matrix4 Roty = Matrix4.CreateRotationY(MathUtils.radians(RenderState.rotAngles.Y));
            Matrix4 Rotz = Matrix4.CreateRotationZ(MathUtils.radians(RenderState.rotAngles.Z));
            RenderState.rotMat = Rotz * Rotx * Roty;
            //RenderState.rotMat = Matrix4.Identity;
        }

        public override void OnRenderUpdate(double dt)
        {
            //Per Frame System Updates
            transformSys.OnRenderUpdate(dt);
            sceneMgmtSys.OnRenderUpdate(dt);

            //Render Shit
            if (rt_State == EngineRenderingState.ACTIVE)
            {
                //Callbacks.Log("* CONTROL : STARTING FRAME UPDATE", LogVerbosityLevel.DEBUG);
                //Callbacks.Log("* CONTROL : FRAME UPDATED", LogVerbosityLevel.DEBUG);
                //Callbacks.Log("* CONTROL : STARTING FRAME RENDER", LogVerbosityLevel.DEBUG);

                renderSys.testrender(dt); //Render Everything

                //Callbacks.Log("* CONTROL : FRAME RENDERED", LogVerbosityLevel.DEBUG);
            }


        }


        #region INPUT_HANDLERS

        //Gamepad handler
        private void gamepadController()
        {
            if (gpHandler == null) return;
            if (!gpHandler.isConnected()) return;

            //Camera Movement
            float cameraSensitivity = 2.0f;
            float x, y, z, rotx, roty;

            x = gpHandler.getAction(ControllerActions.MOVE_X);
            y = gpHandler.getAction(ControllerActions.ACCELERATE) - gpHandler.getAction(ControllerActions.DECELERATE);
            z = gpHandler.getAction(ControllerActions.MOVE_Y_NEG) - gpHandler.getAction(ControllerActions.MOVE_Y_POS);
            rotx = -cameraSensitivity * gpHandler.getAction(ControllerActions.CAMERA_MOVE_H);
            roty = cameraSensitivity * gpHandler.getAction(ControllerActions.CAMERA_MOVE_V);

            targetCameraPos.PosImpulse.X = x;
            targetCameraPos.PosImpulse.Y = y;
            targetCameraPos.PosImpulse.Z = z;
            targetCameraPos.Rotation.X = rotx;
            targetCameraPos.Rotation.Y = roty;
        }

        //Keyboard handler
        private int keyDownStateToInt(Keys k)
        {
            bool state = ActiveKbState.IsKeyDown(k);
            return state ? 1 : 0;
        }

        public void UpdateInput()
        {
            bool kbStateUpdated = false;
            bool msStateUpdated = false;

            //Reset Mouse Inputs
            targetCameraPos.Reset();
            
            if (kbStates.Count > 0)
            {
                ActiveKbState = kbStates.Dequeue();
                kbStateUpdated = true;
                keyboardController();
            }

            if (MsStates.Count > 0)
            {
                ActiveMsState = MsStates.Dequeue();
                msStateUpdated = true;
                mouseController();
            }
            
            //TODO: Re-add controller support
                
            if (kbStateUpdated || msStateUpdated)
                Camera.CalculateNextCameraState(RenderState.activeCam, targetCameraPos);
            
            //gpController();
            
        }
        
        //Public Input Handlers
        public void AddKeyboardState(KeyboardState state)
        {
            //lock (kbStates)
            {
                kbStates.Enqueue(state);
            }
        }

        public void AddMouseState(MouseState state)
        {
            //lock (MsStates)
            {
                MsStates.Enqueue(state);
            }
        }

        private void keyboardController()
        {
            //Camera Movement
            float step = 0.002f;
            float x, y, z;

            x = keyDownStateToInt(Keys.D) - keyDownStateToInt(Keys.A);
            y = keyDownStateToInt(Keys.W) - keyDownStateToInt(Keys.S);
            z = keyDownStateToInt(Keys.R) - keyDownStateToInt(Keys.F);

            //Camera rotation is done exclusively using the mouse
            
            //rotx = 50 * step * (kbHandler.getKeyStatus(OpenTK.Input.Key.E) - kbHandler.getKeyStatus(OpenTK.Input.Key.Q));
            //float roty = (kbHandler.getKeyStatus(Key.C) - kbHandler.getKeyStatus(Key.Z));

            RenderState.rotAngles.Y += 100 * step * (keyDownStateToInt(Keys.E) - keyDownStateToInt(Keys.Q));
            RenderState.rotAngles.Y %= 360;
            
            //Move Camera
            targetCameraPos.PosImpulse.X = x;
            targetCameraPos.PosImpulse.Y = y;
            targetCameraPos.PosImpulse.Z = z;

            //Console.WriteLine("{0} {1} {2}", x, y, z);
        }

        //Mouse Methods

        private int mouseDownStateToInt(MouseButton k)
        {
            bool state = ActiveMsState.IsButtonDown(k);
            return state ? 1 : 0;
        }

        public void mouseController()
        {
            //targetCameraPos.Rotation.Xy += new Vector2(0.55f, 0);
            if (ActiveMsState.WasButtonDown(MouseButton.Left))
            {
                Vector2 deltaVec = ActiveMsState.Position - prevMousePos;
                
                //Console.WriteLine("Mouse Delta {0} {1}", deltax, deltay);
                targetCameraPos.Rotation = deltaVec;
            }

            prevMousePos = ActiveMsState.Position;

        }

        public override void CleanUp()
        {
            actionSys.CleanUp();
            animationSys.CleanUp();
            transformSys.CleanUp();
            
            renderSys.CleanUp();
            sceneMgmtSys.CleanUp();
            registrySys.CleanUp();

        }


        #endregion
        
        
        //API
        //The following static methods should be used to expose
        //functionality to the user abstracted from engine systems and other
        //iternals. The idea is to pass a reference to an instantiated
        //engine object (whenever needed) and let the method do the rest
        
        #region NodeGenerators

        public SceneGraphNode CreateLocatorNode(string name)
        {
            SceneGraphNode n = new(SceneNodeType.LOCATOR)
            {
                Name = name
            };

            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Create MeshComponent
            MeshComponent mc = new()
            {
                MeshVao = GetPrimitiveMesh("default_cross"),
                Material = GetMaterialByName("crossMat")
            };
            
            //Register new instance in the meshVao
            mc.InstanceID = GLMeshBufferManager.AddMeshInstance(ref mc.MeshVao, mc);

            n.AddComponent<MeshComponent>(mc);

            return n;
        }
        
        public SceneGraphNode CreateSceneNode(string name)
        {
            SceneGraphNode n = new(SceneNodeType.MODEL)
            {
                Name = name
            };

            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Create MeshComponent
            MeshComponent mc = new()
            {
                MeshVao = GetPrimitiveMesh("default_cross"),
                Material = GetMaterialByName("crossMat")
            };
            
            //Register new instance in the meshVao
            mc.InstanceID = GLMeshBufferManager.AddMeshInstance(ref mc.MeshVao, mc);
            
            n.AddComponent<MeshComponent>(mc);

            //Create SceneComponent
            SceneComponent sc = new();
            n.AddComponent<SceneComponent>(sc);

            return n;
        }

        public static SceneGraphNode CreateJointNode()
        {
            SceneGraphNode n = new(SceneNodeType.JOINT);
            
            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Add Mesh Component
            MeshComponent mc = new();
            mc.MeshVao = new()
            {
                type = SceneNodeType.JOINT
            };

            mc.MeshVao.vao = new Primitives.LineSegment(n.Children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            mc.Material = Common.RenderState.engineRef.GetMaterialByName("jointMat");

            //Add Joint Component
            JointComponent jc = new();
            n.AddComponent<JointComponent>(jc);
            
            return n;
        }

        
        public SceneGraphNode CreateLightNode(string name="default light", float intensity=1.0f, 
                                                ATTENUATION_TYPE attenuation=ATTENUATION_TYPE.QUADRATIC,
                                                LIGHT_TYPE lighttype = LIGHT_TYPE.POINT)
        {
            SceneGraphNode n = new(SceneNodeType.LIGHT)
            {
                Name = name
            };
            
            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Add Mesh Component
            LineSegment ls = new LineSegment(n.Children.Count, new Vector3(1.0f, 0.0f, 0.0f));
            MeshComponent mc = new()
            {
                MeshVao = new()
                {
                    type = SceneNodeType.LIGHT,
                    vao = ls.getVAO(),
                    MetaData = new()
                    {
                        BatchCount = 2
                    },
                },
                Material = GetMaterialByName("lightMat")
            };
            
            n.AddComponent<MeshComponent>(mc);
            ls.Dispose();
            
            //Add Light Component
            LightComponent lc = new()
            {
                Intensity = intensity,
                Falloff = attenuation,
                LightType = lighttype
            };
            n.AddComponent<LightComponent>(lc);

            return n;
        }

        
        #endregion
        
        #region GLRelatedRequests

        public Texture AddTexture(string filepath)
        {
            byte[] data = File.ReadAllBytes(filepath);
            return AddTexture(data, Path.GetFileName(filepath));
        }
        
        public Texture AddTexture(byte[] data, string name)
        {
            //TODO: Possibly move that to a separate rendering thread
            Texture tex = new();
            tex.Name = name;
            string ext = Path.GetExtension(name).ToUpper();
            tex.textureInit(data, ext); //Manually load data
            renderSys.TextureMgr.AddTexture(tex);
            return tex;
        }
        
        #endregion
        
        
        
        #region StateQueries

        public Scene GetActiveScene()
        {
            return sceneMgmtSys.ActiveScene;
        }
        
        #endregion
        
        
        
    }
}
