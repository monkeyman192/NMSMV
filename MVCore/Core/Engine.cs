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
using System.Timers;
using MVCore.Input;
using GLSLHelper;
using MVCore.Utils;
using Model_Viewer;
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

    public class Engine
    {
        public ResourceManager resMgr;

        //Init Systems
        public EntityRegistrySystem registrySys;
        public TransformationSystem transformSys;
        public ActionSystem actionSys;
        public AnimationSystem animationSys;
        public RenderingSystem renderSys;//TODO: Try to make it private. Noone should have a reason to access it
        private RequestHandler reqHandler;
        private NativeWindow windowHandler;

        //Rendering 
        public EngineRenderingState rt_State;

        //Input
        public BaseGamepadHandler gpHandler;
        
        //Keyboard State
        private KeyboardState kbState;
        private MouseState mouseState;
        private Vector2 mouseClickedPos;
        
        //Camera Stuff
        public CameraPos targetCameraPos;
        //public int movement_speed = 1;
        
        //Use public variables for now because getters/setters are so not worth it for our purpose
        public float light_angle_y = 0.0f;
        public float light_angle_x = 0.0f;
        public float light_distance = 5.0f;
        public float light_intensity = 1.0f;
        public float scale = 1.0f;

        //Palette
        Dictionary<string, Dictionary<string, Vector4>> palette;

        public Engine(NativeWindow win)
        {
            //Store Window handler
            windowHandler = win;
            kbState = win.KeyboardState;
            mouseState = win.MouseState;

            //gpHandler = new PS4GamePadHandler(0); //TODO: Add support for PS4 controller
            reqHandler = new RequestHandler();

            RenderState.activeGamepad = gpHandler;

            //Assign new palette to GLControl
            palette = Palettes.createPalettefromBasePalettes();

            //Systems Init
            renderSys = new RenderingSystem(); //Init renderManager of the engine
            registrySys = new EntityRegistrySystem();
            actionSys = new ActionSystem();
            animationSys = new AnimationSystem();
            transformSys = new TransformationSystem();
            
            renderSys.SetEngine(this);
            registrySys.SetEngine(this);
            actionSys.SetEngine(this);
            animationSys.SetEngine(this);
            transformSys.SetEngine(this);
            
            //Set Start Status
            rt_State = EngineRenderingState.UNINITIALIZED;
        }

        ~Engine()
        {
            Log("Goodbye!", LogVerbosityLevel.INFO);
        }

        private void Log(string msg, LogVerbosityLevel lvl)
        {
            Callbacks.Log("* ENGINE : " + msg, lvl);
        }

        public void init(int width, int height)
        {
            //Init Gizmos
            //gizTranslate = new TranslationGizmo();
            //activeGizmo = gizTranslate;
            resMgr = RenderState.activeResMgr;
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
            
            //Register Camer to Entity Registry
            registrySys.RegisterEntity(cam);
            //Add TransfomComponent to Transformation System
            transformSys.RegisterEntity(cam, true);
            transformSys.AddDynamicEntity(cam);
            
            //Set global reference to cam
            RenderState.activeCam = cam;


            //TESTING Add sample future states for the camera
            
            /*
            TransformController tcontroller = transformSys.GetEntityTransformController(cam);
            Vector3 sample_pos = new Vector3();
            Quaternion sample_rot = new();
            Vector3 sample_scale = new Vector3(1.0f);
            for (int i=0; i< 500; i++)
            {
                tcontroller.AddFutureState(sample_pos, sample_rot, sample_scale);
                sample_pos.X -= 0.5f;
            }
            */

            //Initialize the render manager
            renderSys.init(resMgr, width, height);
            rt_State = EngineRenderingState.ACTIVE;

            Log("Initialized", LogVerbosityLevel.INFO);
        }

        public void handleRequests()
        {
            //Log(string.Format(" {0} Open Requests ", reqHandler.getOpenRequestNum()), LogVerbosityLevel.HIDEBUG);
            if (reqHandler.hasOpenRequests())
            {
                ThreadRequest req = reqHandler.Fetch();
                THREAD_REQUEST_STATUS req_status = THREAD_REQUEST_STATUS.FINISHED;
                Log("Handling Request " + req.type, LogVerbosityLevel.HIDEBUG);

                lock (req)
                {
                    switch (req.type)
                    {
                        case THREAD_REQUEST_TYPE.QUERY_GLCONTROL_STATUS_REQUEST:
                            if (rt_State == EngineRenderingState.UNINITIALIZED)
                                req_status = THREAD_REQUEST_STATUS.ACTIVE;
                            else
                                req_status = THREAD_REQUEST_STATUS.FINISHED;
                                //At this point the renderer is up and running
                            break;
                        case THREAD_REQUEST_TYPE.INIT_RESOURCE_MANAGER:
                            resMgr.Init();
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST:
                            rt_addRootScene((string)req.arguments[0]);
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
#if DEBUG               
                        case THREAD_REQUEST_TYPE.NEW_TEST_SCENE_REQUEST:
                            rt_addTestScene((int)req.arguments[0]);
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
#endif
                        case THREAD_REQUEST_TYPE.CHANGE_MODEL_PARENT_REQUEST:
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
                        case THREAD_REQUEST_TYPE.UPDATE_SCENE_REQUEST:
                            Scene req_scn = (Scene)req.arguments[0];
                            req_scn.update();
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GL_COMPILE_ALL_SHADERS_REQUEST:
                            resMgr.CompileMainShaders();
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.MOUSEPOSITION_INFO_REQUEST:
                            Vector4[] t = (Vector4[])req.arguments[2];
                            renderSys.getMousePosInfo((int)req.arguments[0], (int)req.arguments[1],
                                ref t);
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GL_RESIZE_REQUEST:
                            rt_ResizeViewport((int)req.arguments[0], (int)req.arguments[1]);
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GL_MODIFY_SHADER_REQUEST:
                            GLShaderHelper.modifyShader((GLSLShaderConfig)req.arguments[0],
                                         (GLSLShaderText)req.arguments[1]);
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GIZMO_PICKING_REQUEST:
                            //TODO: Send the nessessary arguments to the render manager and mark the active gizmoparts
                            Gizmo g = (Gizmo)req.arguments[0];
                            renderSys.gizmoPick(ref g, (Vector2)req.arguments[1]);
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.TERMINATE_REQUEST:
                            rt_State = EngineRenderingState.EXIT;
                            resMgr.Cleanup(); //Free Resources
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GL_PAUSE_RENDER_REQUEST:
                            rt_State = EngineRenderingState.PAUSED;
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GL_RESUME_RENDER_REQUEST:
                            rt_State = EngineRenderingState.ACTIVE;
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.NULL: //Is this ever used?
                            req_status = THREAD_REQUEST_STATUS.FINISHED;
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
            Palettes.set_palleteColors();

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
            //Clear Gizmos
            RenderState.activeGizmo = null;

            //Clear RenderStats
            RenderStats.ClearStats();


            //Stop animation if on
            bool animToggleStatus = RenderState.settings.renderSettings.ToggleAnimations;
            RenderState.settings.renderSettings.ToggleAnimations = false;

            //Setup new object
            Scene scene = new();
            scene.name = "DEFAULT SCENE";


            //Add Lights
            Light l = new()
            {
                Name = "Light 1",
                Color = new Vector3(1.0f, 1.0f, 1.0f),
                IsRenderable = true,
                Intensity = 100.0f,
                Attenuation = "QUADRATIC"
            };
            l.localPosition = new Vector3(0.2f, 0.2f, -2.0f);

            RenderState.activeResMgr.GLlights.Add(l);
            scene.children.Add(l);

            Light l1 = new()
            {
                Name = "Light 2",
                Color = new Vector3(1.0f, 1.0f, 1.0f),
                IsRenderable = true,
                Intensity = 100.0f,
                Attenuation = "QUADRATIC"
            };

            l.localPosition = new Vector3(0.2f, -0.2f, -2.0f);
            RenderState.activeResMgr.GLlights.Add(l1);
            scene.children.Add(l1);

            Light l2 = new()
            {
                Name = "Light 3",
                Color = new Vector3(1.0f, 1.0f, 1.0f),
                IsRenderable = true,
                Intensity = 100.0f,
                Attenuation = "QUADRATIC"
            };
            
            l2.localPosition = new Vector3(-0.2f, 0.2f, -2.0f);
            Common.RenderState.activeResMgr.GLlights.Add(l2);
            scene.children.Add(l2);

            Light l3 = new()
            {
                Name = "Light 4",
                Color = new Vector3(1.0f, 1.0f, 1.0f),
                IsRenderable = true,
                Intensity = 100.0f,
                Attenuation = "QUADRATIC"
            };

            l3.localPosition = new Vector3(-0.2f, -0.2f, -2.0f);
            Common.RenderState.activeResMgr.GLlights.Add(l2);
            scene.children.Add(l3);

            //Generate a Sphere and center it in the scene
            Mesh sphere = new();
            sphere.Name = "Test Sphere";
            sphere.parent = scene;
            sphere.setParentScene(scene);
            MeshMetaData sphere_metadata = new MeshMetaData();


            int bands = 80;

            sphere_metadata.batchcount = bands * bands * 6;
            sphere_metadata.batchstart_graphics = 0;
            sphere_metadata.vertrstart_graphics = 0;
            sphere_metadata.vertrend_graphics = (bands + 1) * (bands + 1) - 1;
            sphere_metadata.indicesLength = DrawElementsType.UnsignedInt;

            sphere.meshVao = new GLInstancedMeshVao(sphere_metadata);
            sphere.meshVao.type = TYPES.MESH;
            sphere.meshVao.vao = (new Primitives.Sphere(new Vector3(), 2.0f, 40)).getVAO();


            //Sphere Material
            Material mat = new();
            mat.Name = "default_scn";
            
            Uniform uf = new();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new(1.0f,0.0f,0.0f,1.0f);
            mat.Uniforms.Add(uf);

            uf = new();
            uf.Name = "gMaterialParamsVec4";
            uf.Values = new();
            uf.Values.x = 0.15f; //Roughness
            uf.Values.y = 0.0f;
            uf.Values.z = 0.2f; //Metallic
            uf.Values.t = 0.0f;
            mat.Uniforms.Add(uf);
                
            mat.init();
            resMgr.GLmaterials["test_mat1"] = mat;
            sphere.meshVao.material = mat;
            sphere.instanceId = GLMeshBufferManager.AddInstance(ref sphere.meshVao, sphere); //Add instance
            
            scene.children.Add(sphere);

            //Explicitly add default light to the rootObject
            scene.children.Add(resMgr.GLlights[0]);

            scene.updateLODDistances();
            scene.update(); //Refresh all transforms
            scene.setupSkinMatrixArrays();

            //Save scene path to resourcemanager
            RenderState.activeResMgr.GLScenes["TEST_SCENE_1"] = scene; //Use input path

            //Populate RenderManager
            renderSys.populate(scene);

            //Clear Instances
            renderSys.clearInstances();
            scene.updateMeshInfo(); //Update all mesh info

            scene.selected = 1;
            RenderState.rootObject = scene;
            //RenderState.activeModel = root; //Set the new scene as the new activeModel


            //Reinitialize gizmos
            RenderState.activeGizmo = new TranslationGizmo();

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
            Palettes.set_palleteColors();

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
            //Clear Gizmos
            RenderState.activeGizmo = null;

            //Clear RenderStats
            RenderStats.ClearStats();

            //Stop animation if on
            bool animToggleStatus = RenderState.settings.renderSettings.ToggleAnimations;
            RenderState.settings.renderSettings.ToggleAnimations = false;
            
            //Setup new object
            Model root = GEOMMBIN.LoadObjects(filename);

            //Explicitly add default light to the rootObject
            root.children.Add(resMgr.GLlights[0]);

            root.updateLODDistances();
            root.update(); //Refresh all transforms
            root.setupSkinMatrixArrays();

            //Populate RenderManager
            renderSys.populate(root);

            //Clear Instances
            renderSys.clearInstances();
            root.updateMeshInfo(); //Update all mesh info

            root.selected = 1;
            RenderState.rootObject = root;
            //RenderState.activeModel = root; //Set the new scene as the new activeModel
            

            //Reinitialize gizmos
            RenderState.activeGizmo = new TranslationGizmo();

            //Restart anim worker if it was active
            RenderState.settings.renderSettings.ToggleAnimations = animToggleStatus;
        
        }
        
        public void sendRequest(ref ThreadRequest r)
        {
            reqHandler.sendRequest(ref r);
        }

        public void updateLightPosition(int light_id)
        {
            Light light = resMgr.GLlights[light_id];
            light.updatePosition(new Vector3((float)(light_distance * Math.Cos(MathUtils.radians(light_angle_x)) *
                                                            Math.Sin(MathUtils.radians(light_angle_y))),
                                                (float)(light_distance * Math.Sin(MathUtils.radians(light_angle_x))),
                                                (float)(light_distance * Math.Cos(MathUtils.radians(light_angle_x)) *
                                                            Math.Cos(MathUtils.radians(light_angle_y)))));
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

        public void UpdateInput()
        {
            keyboardController();
            mouseController();
            //gpController();
        }

        public void keyboardController()
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
        }

        //Mouse Methods

        private int mouseDownStateToInt(MouseButton k)
        {
            bool state = mouseState.IsButtonDown(k);
            return state ? 1 : 0;
        }

        public void mouseController()
        {
            if (mouseState.WasButtonDown(MouseButton.Left))
            {
                float deltax = Math.Min(Math.Max(mouseState.Delta.X, -10), 10);
                float deltay = Math.Min(Math.Max(mouseState.Delta.Y, -10), 10);

                //Console.WriteLine("Mouse Delta {0} {1}", deltax, deltay);
                
                targetCameraPos.Rotation.Xy += new Vector2(deltax, deltay);
            }
            
        }


        #endregion




    }
}
