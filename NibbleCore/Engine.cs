using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using OpenTK;
using OpenTK.Input;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using NbCore;
using NbCore.Systems;
using NbCore.Common;
using NbCore.Input;
using NbCore.Math;
using NbCore.Primitives;
using NbCore.Utils;
using NbCore.Plugins;
using System.Timers;
using NbCore.Platform.Graphics.OpenGL; //Add an implementation independent shader definition
using OpenTK.Windowing.GraphicsLibraryFramework; //TODO: figure out how to remove that shit
using OpenTK.Windowing.Desktop; //TODO: figure out how to remove that shit
using System.IO;
using System.Reflection;
using Font = NbCore.Text.Font;
using Image = System.Drawing.Image;
using System.Linq;

namespace NbCore
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
        private bool CaptureInput; //Toggle to enable Capture of Mouse/Keyboard/Controller input
        private Queue<KeyboardState> kbStates = new();
        private Queue<MouseState> MsStates = new();
        
        //Camera Stuff
        public CameraPos targetCameraPos;
        public OpenTK.Mathematics.Vector2 prevMousePos;

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

            InitSystems();

            //Set Start Status
            rt_State = EngineRenderingState.UNINITIALIZED;

            LoadPlugins();
            LoadDefaultResources();
            
        }
        
        ~Engine()
        {
            Log("Goodbye!", LogVerbosityLevel.INFO);
        }

        private void InitSystems()
        {
            //Systems Init
            renderSys = new RenderingSystem(); //Init renderManager of the engine
            registrySys = new EntityRegistrySystem();
            actionSys = new ActionSystem();
            animationSys = new AnimationSystem();
            transformSys = new TransformationSystem();
            sceneMgmtSys = new SceneManagementSystem();

            SetEngine(this);
            renderSys.SetEngine(this);
            registrySys.SetEngine(this);
            actionSys.SetEngine(this);
            animationSys.SetEngine(this);
            transformSys.SetEngine(this);
            sceneMgmtSys.SetEngine(this);
        }

        public void SetCaptureInputStatus(bool status)
        {
            CaptureInput = status;
        }

        #region plugin_loader

        private AssemblyName GetAssemblyName(string name)
        {
            //Fetch AssemblyName
            AssemblyName aName = null;
            try
            {
                aName = AssemblyName.GetAssemblyName(name);
            }
            catch (FileNotFoundException)
            {
                var plugindirectory = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Plugins");
                var path = Path.Join(plugindirectory, name);

                if (File.Exists(path))
                {
                    aName = AssemblyName.GetAssemblyName(path);
                } else
                {
                    Log($"Unable to find assembly {name}", LogVerbosityLevel.WARNING);
                }
            }

            return aName;
        }

        private Assembly GetAssembly(AssemblyName aName)
        {
            Assembly a = null;
            try
            {
                //First try to load using the assembly name just in case its a system dll    
                a = Assembly.Load(aName);
            }
            catch (FileNotFoundException ex)
            {
                Log($"Unable to load assembly {aName.Name}, Looking in plugin directory...", LogVerbosityLevel.WARNING);
                //Look in plugin directory
                var plugindirectory = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Plugins");
                var path = Path.Join(plugindirectory, aName.Name + ".dll");

                if (File.Exists(path))
                {
                    a = Assembly.LoadFrom(path);
                } else
                {
                    Log($"Unable to load assembly {aName.Name}, Error: {ex.Message}", LogVerbosityLevel.WARNING);
                }
            }

            return a;
        }

        private void LoadAssembly(string name)
        {
            AssemblyName aName = GetAssemblyName(name);
            if (aName == null)
                return;
            
            Assembly test = GetAssembly(aName);
            if (test == null)
                return;

            //FetchAssembly
            Log($"Loaded Assembly {test.GetName()}", LogVerbosityLevel.WARNING);
            AppDomain.CurrentDomain.Load(test.GetName());
            
            //Load Referenced Assemblies
            AssemblyName[] l = test.GetReferencedAssemblies();
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (AssemblyName a2 in l)
            {
                var asm = loadedAssemblies.FirstOrDefault(a => a.FullName == a2.FullName);

                if (asm == null)
                {
                    LoadAssembly(a2.Name + ".dll");
                }
            }

        }

        public void LoadPlugin(string filepath)
        {
            //Load Assembly
            try
            {
                Assembly a = Assembly.LoadFile(Path.GetFullPath(filepath));
                
                //Try to find the type the derived plugin class
                foreach (Type t in a.GetTypes())
                {
                    if (t.IsSubclassOf(typeof(PluginBase)))
                    {
                        Log($"Plugin class detected! {t.Name}", LogVerbosityLevel.INFO);
                        
                        LoadAssembly(filepath);

                        object c = Activator.CreateInstance(t, new object[] { this });
                        Plugins[Path.GetFileName(filepath)] = c as PluginBase;
                        //Call Dll initializers
                        t.GetMethod("OnLoad").Invoke(c, new object[] { });
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error during loading of plugin {filepath}", LogVerbosityLevel.INFO);
                Log("Exception type " + ex.Data, LogVerbosityLevel.INFO);
            }
        }


        private void LoadPlugins()
        {
            foreach (string filename in Directory.GetFiles("Plugins"))
            {
                if (!filename.EndsWith(("dll")))
                    continue;

                if (!Path.GetFileName(filename).StartsWith(("Nibble")))
                    continue;

                LoadPlugin(filename);
            }
        }
        private void LoadDefaultResources()
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

        #endregion

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

        public void RegisterEntity(Entity e)
        {
            //Add Entity to main registry
            if (registrySys.RegisterEntity(e))
            {
                //Register to transformation System
                if (e.HasComponent<TransformComponent>())
                    transformSys.RegisterEntity(e);

                //Register to rendering System
                if (e.HasComponent<MeshComponent>())
                {

                    //Register mesh, material and the corresponding shader if necessary
                    MeshComponent mc = e.GetComponent<MeshComponent>() as MeshComponent;
                    
                    RegisterEntity(mc.Mesh);
                    RegisterEntity(mc.Material);
                    RegisterEntity(mc.Material.Shader);
                    
                    renderSys.RegisterEntity(e); //Register Mesh
                }
                    
                //TODO Register to the rest systems if necessary
            }
        }

        public void RegisterSceneGraphNode(SceneGraphNode node)
        {
            RegisterEntity(node);
            sceneMgmtSys.ActiveScene.AddNode(node); //Add to the active scene
            foreach (SceneGraphNode child in node.Children)
                RegisterSceneGraphNode(child);
        }
        
        public Scene CreateScene()
        {
            Scene scn = sceneMgmtSys.CreateScene();
            //Register Entities
            RegisterEntity(scn.Root);
            transformSys.RequestEntityUpdate(scn.Root);
            
            return scn;
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


        #region EngineQueries

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

        public NbMesh GetPrimitiveMesh(ulong hash)
        {
            return renderSys.GeometryMgr.GetPrimitiveMesh(hash);
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

        #endregion

        public void init(int width, int height)
        {
            //Initialize Resource Manager
            InitializeResources();

            //Add Camera
            Camera cam = new(90, 0, true)
            {
                isActive = false
            };

            //Add Necessary Components to Camera
            TransformationSystem.AddTransformComponentToEntity(cam);
            TransformComponent tc = cam.GetComponent<TransformComponent>() as TransformComponent;
            tc.IsControllable = true;
            tc.IsDynamic = true;
            RegisterEntity(cam);
            
            //Set global reference to cam
            RenderState.activeCam = cam;

            //Set Camera Initial State
            TransformController tcontroller = transformSys.GetEntityTransformController(cam);
            tcontroller.AddFutureState(new NbVector3(), NbQuaternion.FromEulerAngles(0.0f, -3.14f/2.0f, 0.0f), new NbVector3(1.0f));

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
            renderSys.Resize(w, h);
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

            TransformationSystem.SetEntityLocation(l, new NbVector3(0.2f, 0.2f, -2.0f));
            RegisterEntity(l);
            scene.Children.Add(l);

            SceneGraphNode l1 = CreateLightNode("Light 2", 100.0f,
                ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);

            TransformationSystem.SetEntityLocation(l1, new NbVector3(0.2f, -0.2f, -2.0f));
            RegisterEntity(l1);
            scene.Children.Add(l1);

            SceneGraphNode l2 = CreateLightNode("Light 3", 100.0f,
                ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);

            TransformationSystem.SetEntityLocation(l2, new NbVector3(-0.2f, 0.2f, -2.0f));
            RegisterEntity(l2);
            scene.Children.Add(l2);

            SceneGraphNode l3 = CreateLightNode("Light 4", 100.0f,
                ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);

            TransformationSystem.SetEntityLocation(l3, new NbVector3(-0.2f, -0.2f, -2.0f));
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
                Mesh = new()
                {
                    MetaData = new()
                    {
                        BatchCount = bands * bands * 6,
                        BatchStartGraphics = 0,
                        VertrStartGraphics = 0,
                        VertrEndGraphics = (bands + 1) * (bands + 1) - 1
                    },
                    Data = (new Sphere(new NbVector3(), 2.0f, 40)).GetData()
                }
                
            };

            GLSLShaderConfig shader = null;

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
            shader = renderSys.Renderer.CompileMaterialShader(mat, SHADER_MODE.DEFFERED);
            renderSys.Renderer.AttachShaderToMaterial(mat, shader);

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
            
            RegisterSceneGraphNode(node);
            
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
            if (CaptureInput)
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
            NbMatrix4 Rotx = NbMatrix4.CreateRotationX(MathUtils.radians(RenderState.rotAngles.X));
            NbMatrix4 Roty = NbMatrix4.CreateRotationY(MathUtils.radians(RenderState.rotAngles.Y));
            NbMatrix4 Rotz = NbMatrix4.CreateRotationZ(MathUtils.radians(RenderState.rotAngles.Z));
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
                OpenTK.Mathematics.Vector2 deltaVec = ActiveMsState.Position - prevMousePos;
                
                //Console.WriteLine("Mouse Delta {0} {1}", deltax, deltay);
                targetCameraPos.Rotation.X = deltaVec.X;
                targetCameraPos.Rotation.Y = deltaVec.Y;
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
            tc.IsDynamic = false;
            tc.IsControllable = true;
            n.AddComponent<TransformComponent>(tc);

            //Create MeshComponent
            MeshComponent mc = new()
            {
                Mesh = GetPrimitiveMesh((ulong) "default_cross".GetHashCode()),
                Material = GetMaterialByName("crossMat")
            };
            
            n.AddComponent<MeshComponent>(mc);

            return n;
        }
        
        public SceneGraphNode CreateMeshNode(string name, NbMesh mesh, MeshMaterial mat)
        {
            SceneGraphNode n = new(SceneNodeType.MESH)
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
                Mesh = mesh,
                Material = mat
            };
            
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
            tc.IsDynamic = false;
            tc.IsControllable = true;
            n.AddComponent<TransformComponent>(tc);

            //Create MeshComponent
            MeshComponent mc = new()
            {
                Mesh = GetPrimitiveMesh((ulong)"default_cross".GetHashCode()),
                Material = GetMaterialByName("crossMat")
            };
            
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
            Primitive seg = new LineSegment(n.Children.Count, new NbVector3(1.0f, 0.0f, 0.0f));
            MeshComponent mc = new()
            {
                Mesh = new()
                {
                    Data = seg.GetData(),
                    MetaData = seg.GetMetaData()
                },
                Material = Common.RenderState.engineRef.GetMaterialByName("jointMat")
            };
            n.AddComponent<MeshComponent>(mc);
            
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
            LineSegment ls = new LineSegment(n.Children.Count, new NbVector3(1.0f, 0.0f, 0.0f));
            MeshComponent mc = new()
            {
                Mesh = new()
                {
                    MetaData = ls.GetMetaData(),
                    Data = ls.GetData()
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


        #region AssetDisposal
        public void RecursiveSceneGraphNodeDispose(SceneGraphNode node)
        {
            foreach (SceneGraphNode child in node.Children)
                RecursiveSceneGraphNodeDispose(child);
            node.Dispose();
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
