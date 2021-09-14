using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using OpenTK;
using OpenTK.Input;
using OpenTK.Graphics;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Graphics.OpenGL4;
using MVCore;
using MVCore.Systems;
using MVCore.Common;
using MVCore.Input;
using MVCore.Primitives;
using MVCore.Utils;
using System.Timers;
using GLSLHelper;
using libMBIN.NMS.Toolkit;

using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;

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
        public ResourceManager resMgr;

        //Init Systems
        private EntityRegistrySystem registrySys;
        public TransformationSystem transformSys;
        public ActionSystem actionSys;
        public AnimationSystem animationSys;
        public SceneManagementSystem sceneManagementSys;
        public RenderingSystem renderSys; //TODO: Try to make it private. Noone should have a reason to access it
        private readonly RequestHandler reqHandler;
        private readonly NativeWindow windowHandler;

        //Rendering 
        public EngineRenderingState rt_State;

        //Input
        public BaseGamepadHandler gpHandler;

        //Keyboard State
        private readonly KeyboardState kbState;
        private readonly MouseState mouseState;

        //Camera Stuff
        public CameraPos targetCameraPos;
        public Vector2 prevMousePos;


        //Use public variables for now because getters/setters are so not worth it for our purpose
        public float light_angle_y = 0.0f;
        public float light_angle_x = 0.0f;
        public float light_distance = 5.0f;
        public float light_intensity = 1.0f;
        public float scale = 1.0f;

        //Palette
        Dictionary<string, Dictionary<string, Vector4>> palette;

        public Engine(NativeWindow win) : base(EngineSystemEnum.CORE_SYSTEM)
        {
            //Store Window handler
            windowHandler = win;
            kbState = win.KeyboardState;
            mouseState = win.MouseState;

            prevMousePos = mouseState.Position;

            //gpHandler = new PS4GamePadHandler(0); //TODO: Add support for PS4 controller
            reqHandler = new RequestHandler();

            RenderState.activeGamepad = gpHandler;

            //Systems Init
            renderSys = new RenderingSystem(); //Init renderManager of the engine
            registrySys = new EntityRegistrySystem();
            actionSys = new ActionSystem();
            animationSys = new AnimationSystem();
            transformSys = new TransformationSystem();
            sceneManagementSys = new SceneManagementSystem();

            renderSys.SetEngine(this);
            registrySys.SetEngine(this);
            actionSys.SetEngine(this);
            animationSys.SetEngine(this);
            transformSys.SetEngine(this);
            sceneManagementSys.SetEngine(this);

            //Initialize Resource Manager
            resMgr = new ResourceManager();
            InitializeResourceManager();

            //Assign new palette to GLControl
            //Todo get rid of palettes they have no place here
            palette = Import.NMS.Palettes.createPalettefromBasePalettes(ref resMgr);



            //Set Start Status
            rt_State = EngineRenderingState.UNINITIALIZED;
        }

        ~Engine()
        {
            Log("Goodbye!", LogVerbosityLevel.INFO);
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

        public Scene CreateScene()
        {
            return sceneManagementSys.CreateScene();
        }

        #region ResourceManager

        public void InitializeResourceManager()
        {
            AddDefaultMaterials();
        }

        private void AddDefaultMaterials()
        {
            //Cross Material
            MeshMaterial mat;

            mat = new();
            mat.Name = "crossMat";
            mat.add_flag(MaterialFlagEnum._F07_UNLIT);
            mat.add_flag(MaterialFlagEnum._F21_VERTEXCOLOUR);
            Uniform uf = new()
            {
                Name = "gMaterialColourVec4",
                Values = new(1.0f, 1.0f, 1.0f, 1.0f)
            };
            mat.Uniforms.Add(uf);
            mat.init();
            RegisterEntity(mat);

            //Joint Material
            mat = new MeshMaterial
            {
                Name = "jointMat"
            };
            mat.add_flag(MaterialFlagEnum._F07_UNLIT);

            uf = new Uniform();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new(1.0f, 0.0f, 0.0f, 1.0f);
            mat.Uniforms.Add(uf);
            mat.init();
            RegisterEntity(mat);

            //Light Material
            mat = new()
            {
                Name = "lightMat"
            };
            mat.add_flag(MaterialFlagEnum._F07_UNLIT);

            uf = new();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new(1.0f, 1.0f, 0.0f, 1.0f);
            mat.Uniforms.Add(uf);
            mat.init();
            RegisterEntity(mat);

            //Collision Material
            mat = new();
            mat.Name = "collisionMat";
            mat.add_flag(MaterialFlagEnum._F07_UNLIT);

            uf = new();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new(0.8f, 0.8f, 0.2f, 1.0f);
            mat.Uniforms.Add(uf);
            mat.init();
            RegisterEntity(mat);

        }


        #endregion

        //Asset Getter
        public MeshMaterial GetMaterialByName(string name)
        {
            return registrySys.GetEntityTypeList(EntityType.Material).Find(x => ((MeshMaterial) x).Name == name) as MeshMaterial;
        }

        public SceneGraphNode GetSceneNodeByName(string name)
        {
            return registrySys.GetEntityTypeList(EntityType.SceneNode).Find(x=>((SceneGraphNode) x).Name == name) as SceneGraphNode;
        }
        
        public SceneGraphNode GetSceneNodeByNameType(SceneNodeType type, string name)
        {
            return registrySys.GetEntityTypeList(EntityType.SceneNode).Find(x=>((SceneGraphNode) x).Name == name && ((SceneGraphNode) x).Type == type) as SceneGraphNode;
        }

        public GLSLShaderSource GetShaderSourceByFilePath(string path)
        {
            return registrySys.GetEntityTypeList(EntityType.ShaderSource)
                .Find(x => ((GLSLShaderSource)x).SourceFilePath == path) as GLSLShaderSource;
        }

        public int GetLightCount()
        {
            return registrySys.GetEntityTypeList(EntityType.LightComponent).Count;
        }

        public List<Entity> GetEntityTypeList(EntityType type)
        {
            return registrySys.GetEntityTypeList(type);
        }
        
    

    public void init(int width, int height)
        {
            //Init Gizmos
            //gizTranslate = new TranslationGizmo();
            //activeGizmo = gizTranslate;
            if (!resMgr.initialized)
            {
                throw new Exception("Resource Manager not initialized");
                //resMgr.Init();
            }

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
            renderSys.init(resMgr, width, height);
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
                Log("Handling Request " + req.type, LogVerbosityLevel.HIDEBUG);

                lock (req)
                {
                    switch (req.type)
                    {
                        case THREAD_REQUEST_TYPE.ENGINE_QUERY_GLCONTROL_STATUS:
                            if (rt_State == EngineRenderingState.UNINITIALIZED)
                                req_status = THREAD_REQUEST_STATUS.ACTIVE;
                            else
                                req_status = THREAD_REQUEST_STATUS.FINISHED;
                                //At this point the renderer is up and running
                            break;
                        case THREAD_REQUEST_TYPE.ENGINE_INIT_RESOURCE_MANAGER:
                            resMgr.Init();
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.ENGINE_OPEN_NEW_SCENE:
                            rt_addRootScene((string)req.arguments[0]);
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
#if DEBUG               
                        case THREAD_REQUEST_TYPE.ENGINE_OPEN_TEST_SCENE:
                            rt_addTestScene((int)req.arguments[0]);
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
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
                            */
                            break;
                        case THREAD_REQUEST_TYPE.ENGINE_UPDATE_SCENE:
                            throw new Exception("Not Implemented Yet!");
                            break;
                        case THREAD_REQUEST_TYPE.ENGINE_COMPILE_ALL_SHADERS:
                            resMgr.CompileMainShaders();
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
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
                        case THREAD_REQUEST_TYPE.ENGINE_GIZMO_PICKING:
                            throw new Exception("not yet implemented");
                            break;
                        case THREAD_REQUEST_TYPE.ENGINE_TERMINATE_RENDER:
                            rt_State = EngineRenderingState.EXIT;
                            resMgr.Cleanup(); //Free Resources
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
                            Log(string.Format("Not supported Request {0}", req.type), LogVerbosityLevel.HIDEBUG);
                            break;
                    }
                }

                req.status = req_status;
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
            Import.NMS.Palettes.set_palleteColors(ref resMgr);

            //Clear Systems
            actionSys.CleanUp();
            animationSys.CleanUp();

            //Clear Resources
            resMgr.Cleanup();
            resMgr.Init();
            RenderState.activeResMgr = resMgr;
            ModelProcGen.procDecisions.Clear();

            RenderState.rootObject = null;
            RenderState.activeModel = null;
            
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
            SceneGraphNode l = new(SceneNodeType.LIGHT)
            {
                Name = "Light 1",
                IsRenderable = true
            };

            //TODO add Light Component with the following properties
            //Color = new Vector3(1.0f, 1.0f, 1.0f),
            //Intensity = 100.0f,
            //Falloff = ATTENUATION_TYPE.QUADRATIC


            TransformationSystem.SetEntityLocation(l, new Vector3(0.2f, 0.2f, -2.0f));
            RegisterEntity(l);
            scene.Children.Add(l);

            SceneGraphNode l1 = new(SceneNodeType.LIGHT)
            {
                Name = "Light 2",
                IsRenderable = true
            };

            //TODO add Light Component with the following properties
            //Color = new Vector3(1.0f, 1.0f, 1.0f),
            //Intensity = 100.0f,
            //Falloff = ATTENUATION_TYPE.QUADRATIC

            TransformationSystem.SetEntityLocation(l1, new Vector3(0.2f, -0.2f, -2.0f));
            RegisterEntity(l1);
            scene.Children.Add(l1);

            SceneGraphNode l2 = new(SceneNodeType.LIGHT)
            {
                Name = "Light 3",
                IsRenderable = true
            };

            //TODO add Light Component with the following properties
            //Color = new Vector3(1.0f, 1.0f, 1.0f),
            //Intensity = 100.0f,
            //Falloff = ATTENUATION_TYPE.QUADRATIC

            TransformationSystem.SetEntityLocation(l2, new Vector3(-0.2f, 0.2f, -2.0f));
            RegisterEntity(l2);
            scene.Children.Add(l2);

            SceneGraphNode l3 = new(SceneNodeType.LIGHT)
            {
                Name = "Light 4",
                IsRenderable = true
            };

            //TODO add Light Component with the following properties
            //Color = new Vector3(1.0f, 1.0f, 1.0f),
            //Intensity = 100.0f,
            //Falloff = ATTENUATION_TYPE.QUADRATIC

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
            mc.MeshVao.vao = (new Primitives.Sphere(new Vector3(), 2.0f, 40)).getVAO();
            
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
                
            mat.init();
            RegisterEntity(mat);
            
            scene.Children.Add(sphere);

            //Explicitly add default light to the rootObject
            scene.Children.Add(l);

            //Save scene path to resourcemanager
            RenderState.activeResMgr.GLScenes["TEST_SCENE_1"] = scene; //Use input path

            //Populate RenderManager
            renderSys.populate(null);

            scene.IsSelected = true;
            RenderState.rootObject = scene;
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

        private void rt_addRootScene(string filename)
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            Import.NMS.Palettes.set_palleteColors(ref resMgr);

            //Clear Systems
            actionSys.CleanUp();
            animationSys.CleanUp();

            //Clear Resources
            resMgr.Cleanup();
            resMgr.Init();
            RenderState.activeResMgr = resMgr;
            ModelProcGen.procDecisions.Clear();
            
            RenderState.rootObject = null;
            RenderState.itemCounter = 0;
            RenderState.activeModel = null;
            
            //Clear RenderStats
            RenderStats.ClearStats();

            //Stop animation if on
            bool animToggleStatus = RenderState.settings.renderSettings.ToggleAnimations;
            RenderState.settings.renderSettings.ToggleAnimations = false;
            
            //Setup new object
            SceneGraphNode root = Import.NMS.Importer.ImportScene(filename);

            //Explicitly add default light to the rootObject
            //TODO: add default light
            
            
            //Todo make that through the systems update functions

            //root.update(); //Refresh all transforms
            //root.setupSkinMatrixArrays();

            //Populate RenderManager
            renderSys.populate(null);

            //Clear Instances
            //renderSys.clearInstances();
            //root.updateMeshInfo(); //Update all mesh info

            root.IsSelected = true;
            RenderState.rootObject = root;
            //RenderState.activeModel = root; //Set the new scene as the new activeModel
            
            //Restart anim worker if it was active
            RenderState.settings.renderSettings.ToggleAnimations = animToggleStatus;
        
        }
        
        public void SendRequest(ref ThreadRequest r)
        {
            reqHandler.AddRequest(ref r);
        }

        public override void OnFrameUpdate(double dt)
        {
            handleRequests(); //Handle engine requests

            //Calculate new Camera State
            Camera.CalculateNextCameraState(RenderState.activeCam, targetCameraPos);

            //Update systems
            transformSys.OnFrameUpdate(dt);
            sceneManagementSys.OnFrameUpdate(dt);
            
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
            sceneManagementSys.OnRenderUpdate(dt);

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
            bool state = kbState.IsKeyDown(k);
            return state ? 1 : 0;
        }

        public void UpdateInput(double dt, bool capture_input)
        {
            //Reset Inputs
            targetCameraPos.Reset();
            
            if (capture_input)
            {
                keyboardController(dt);
                mouseController(dt);
                //gpController();
            } 

        }

        public void keyboardController(double dt)
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
            bool state = mouseState.IsButtonDown(k);
            return state ? 1 : 0;
        }

        public void mouseController(double dt)
        {
            //targetCameraPos.Rotation.Xy += new Vector2(0.55f, 0);
            if (mouseState.WasButtonDown(MouseButton.Left))
            {
                Vector2 deltaVec = mouseState.Position - prevMousePos;
                
                //Console.WriteLine("Mouse Delta {0} {1}", deltax, deltay);
                targetCameraPos.Rotation = deltaVec;
            }

            prevMousePos = mouseState.Position;

        }


        #endregion
    }
}
