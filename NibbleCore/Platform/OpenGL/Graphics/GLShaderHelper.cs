using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NbCore;
using NbCore.Math;
using NbCore.Common;
using System.Windows;
using NbCore.Utils;


namespace NbCore.Platform.Graphics.OpenGL { 

    [Flags]
    public enum SHADER_MODE
    {
        DEFAULT,
        DEFFERED,
        FORWARD,
        DECAL
    }
    
    public enum SHADER_TYPE
    {
        NULL_SHADER = 0x0,
        MESH_FORWARD_SHADER,
        MESH_DEFERRED_SHADER,
        DECAL_SHADER,
        DEBUG_MESH_SHADER,
        GIZMO_SHADER,
        PICKING_SHADER,
        BBOX_SHADER,
        LOCATOR_SHADER,
        JOINT_SHADER,
        CAMERA_SHADER,
        TEXTURE_MIX_SHADER,
        PASSTHROUGH_SHADER,
        RED_FILL_SHADER,
        LIGHT_SHADER,
        TEXT_SHADER,
        MATERIAL_SHADER,
        GBUFFER_SHADER,
        LIGHT_PASS_LIT_SHADER,
        LIGHT_PASS_UNLIT_SHADER, //Stupid but keeping that for testing...
        BRIGHTNESS_EXTRACT_SHADER,
        GAUSSIAN_HORIZONTAL_BLUR_SHADER,
        GAUSSIAN_VERTICAL_BLUR_SHADER,
        ADDITIVE_BLEND_SHADER,
        FXAA_SHADER,
        TONE_MAPPING,
        INV_TONE_MAPPING,
        BWOIT_COMPOSITE_SHADER
    }

    public enum ShaderSourceType
    {
        Static,
        Dynamic
    }

    public class GLSLShaderSource : Entity
    {
        public string Name = "";
        private List<GLSLShaderSource> _dynamicTextParts = new();
        private List<string> _staticTextParts = new();
        public string SourceFilePath = "";
        public string SourceText = "";
        private List<string> _Directives = new();
        private FileSystemWatcher _watcher;
        public string ResolvedText = ""; //Full shader text after resolving
        public string ActualShaderSource = "";
        public int shader_object_id = -1;
        public bool Resolved = false;
        public bool Processed = false;
        public ShaderSourceType SourceType;

        public string CompilationLog = ""; //Keep track of the generated log during shader compilation
        
        public List<GLSLShaderConfig> Shaders = new(); //Keeps track of all the Shaders that the current source is used by
        private List<GLSLShaderSource> ReferToSources = new(); //Keep source texts that the current text refers to
        private List<GLSLShaderSource> ReferencedBySources = new(); //Keep source texts that reference this source
        
        //Static random generator used in temp file name generation
        private static readonly Random rand_gen = new(999991);

        //Default shader versions
        public const string version = "#version 450\n #extension GL_ARB_explicit_uniform_location : enable\n" +
                                       "#extension GL_ARB_separate_shader_objects : enable\n" +
                                       "#extension GL_ARB_texture_query_lod : enable\n" +
                                       "#extension GL_ARB_gpu_shader5 : enable\n";

        public GLSLShaderSource() : base(EntityType.ShaderSource)
        {
            SourceType = ShaderSourceType.Static;
        }

        public GLSLShaderSource(string text) : base(EntityType.ShaderSource)
        {
            SourceType = ShaderSourceType.Static;
            SourceText = text;
            Name = "Shader_" + RenderState.engineRef.GetShaderSourceCount();
            //Automatically register to engine
            RenderState.engineRef.RegisterEntity(this);
        }

        public GLSLShaderSource(string filepath, bool watchFile) : base(EntityType.ShaderSource)
        {
            SourceType = ShaderSourceType.Dynamic;
            
            SourceFilePath = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, filepath);
            SourceText = File.ReadAllText(filepath);
            if (watchFile)
            {
                addFileWatcher(SourceFilePath);
            }
            //Automatically register to engine
            RenderState.engineRef.RegisterEntity(this);
        }

        private void addFileWatcher(string filepath)
        {
            FileSystemWatcher fw = new FileSystemWatcher();
            fw.Changed += new FileSystemEventHandler(file_changed);
            fw.Path = Path.GetDirectoryName(filepath);
            fw.Filter = Path.GetFileName(filepath);
            fw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            fw.EnableRaisingEvents = true;
            _watcher = fw;
        }

        public void AddDirective(string s)
        {
            _Directives.Add(s);
        }
        
        public void Compile(ShaderType type, string append_text = "")
        {
            if (!Resolved)
                Resolve();
            
            CompilationLog = ""; //Reset compilation log
            shader_object_id = GL.CreateShader(type);
            
            //Compile Shader
            GL.ShaderSource(shader_object_id, version + "\n" + append_text + "\n" + ResolvedText);
            
            //Get resolved shader text
            GL.GetShaderSource(shader_object_id, 32768, out int actual_shader_length, out ActualShaderSource);
            
            GL.CompileShader(shader_object_id);
            GL.GetShaderInfoLog(shader_object_id, out string info);

            CompilationLog += GLShaderHelper.NumberLines(ActualShaderSource) + "\n";
            CompilationLog += info + "\n";
                
            GL.GetShader(shader_object_id, ShaderParameter.CompileStatus, out int status_code);
            if (status_code != 1)
            {
                Console.WriteLine(GLShaderHelper.NumberLines(ActualShaderSource));
                
                Callbacks.showError("Failed to compile shader for the model. Contact Dev",
                    "Shader Compilation Error");
                GLShaderHelper.throwCompilationError(CompilationLog +
                    GLShaderHelper.NumberLines(ActualShaderSource) + "\n" + info);
            }
                
        }

        public void Resolve()
        {
            if (!Processed)
                Process();

            if (Resolved)
                return;

            ResolvedText = "";

            //Add Directives
            foreach (string dir in _Directives)
                ResolvedText += "#define " + dir + '\n';
            
            if (SourceType == ShaderSourceType.Static)
            {
                ResolvedText += SourceText;
            }
            else
            {
                int dynamicPartId = 0;
                for (int i = 0; i < _staticTextParts.Count; i++)
                {
                    if (_staticTextParts[i] == "[FETCH_DYNAMIC]")
                    {
                        if (!_dynamicTextParts[dynamicPartId].Resolved)
                            _dynamicTextParts[dynamicPartId].Resolve();
                        ResolvedText += _dynamicTextParts[dynamicPartId].ResolvedText;
                        dynamicPartId++;
                    } else
                        ResolvedText += _staticTextParts[i];
                }    
            }

            Resolved = true;
        }
        
        public void Process()
        {
            _dynamicTextParts.Clear();
            _staticTextParts.Clear();
            Resolved = false;

            if (SourceType == ShaderSourceType.Static)
            {
                Processed = true;
                return;
            } else //Dynamic Sources
            {
                //Parse source file
                StringReader sr = new StringReader(SourceText);
                string dirpath = Path.GetDirectoryName(SourceFilePath);
                string line;
                string[] split;
                string staticpart = "";
                while ((line = sr.ReadLine()) != null)
                {
                    //string line = sr.ReadLine();
                    string original_line = line;
                    line = line.TrimStart(new char[] { ' ' });

                    //Check for preprocessor directives
                    if (line.StartsWith("#include"))
                    {
                        //Save static part
                        if (staticpart != "")
                        {
                            _staticTextParts.Add(staticpart);
                            staticpart = "";
                        }
                            
                        split = line.Split(' ');

                        if (split.Length != 2)
                            throw new ApplicationException("Wrong Usage of #include directive");

                        //get included filepath
                        string npath = split[1].Trim('"');
                        npath = Path.Combine(dirpath, npath);
                        //Add dynamic source
                        //Check if Shader Source exists for this path
                        GLSLShaderSource ss = NbCore.Common.RenderState.engineRef.GetShaderSourceByFilePath(npath);
                        if (ss == null)
                        {
                            ss = new GLSLShaderSource(npath, true);
                        }
                        if (!ss.Processed)
                            ss.Process();
                        _dynamicTextParts.Add(ss);
                        _staticTextParts.Add("[FETCH_DYNAMIC]");
                    }
                    else
                    {
                        staticpart += original_line + '\n';
                    }
                }
                sr.Close();
                
                //Save last static part
                if (staticpart != "")
                {
                    _staticTextParts.Add(staticpart);
                }
            }

            Processed = true;
        }

        private void file_changed(object sender, FileSystemEventArgs e)
        {
            FileSystemWatcher fw = (FileSystemWatcher) sender;
            string path = Path.Combine(fw.Path, fw.Filter);
            Console.WriteLine("Reloading {0}", path);

            while (true)
            {
                try
                {
                    FileStream fileStream = File.Open(SourceFilePath,
                        FileMode.Open, FileAccess.Read, FileShare.None);
                    break;
                } catch
                {
                    continue;
                }
            }

            SourceText = File.ReadAllText(SourceFilePath);
            
            Process();
            Resolve();
            
        }

        private string Parser(string path, bool initWatchers)
        {
            //Make sure that the input file is indeed a file
            StreamReader sr;
            string[] split;
            string relpath = "";
            string text = "";
            string tmp_file = "tmp_" + rand_gen.Next().ToString();
            Console.WriteLine("Using temp file {0}", tmp_file);
            bool use_tmp_file = false;
            if (path.EndsWith(".glsl"))
            {
                string execPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                //string execPath = "G:\\Projects\\Model Viewer C#\\Model Viewer\\Viewer_Unit_Tests\\bin\\Debug";
                path = Path.Combine(execPath, path);
                Console.WriteLine(path);
                //Check if file exists
                if (!File.Exists(path))
                {
                    //Because of shader files coming either in raw or path format, I should check for resources in
                    //the local Shaders folder as well
                    string basename = Path.GetFileName(path);
                    string dirname = Path.GetDirectoryName(path);
                    path = Path.Combine(dirname, "Shaders", basename);
                    if (!File.Exists(path))
                        throw new ApplicationException("Preprocessor: File not found. Check the input filepath");
                }

                //Add filewatcher
                if (initWatchers)
                    addFileWatcher(path);

                split = Path.GetDirectoryName(path).Split(Path.PathSeparator);
                relpath = split[split.Length - 1];

                //FileStream fs = new FileStream(path, FileMode.Open);
                sr = new StreamReader(path);
            }
            else
            {
                //Shader has been provided in a raw string
                //Save it to a temp file
                File.WriteAllText(tmp_file, path);
                sr = new StreamReader(tmp_file);
                use_tmp_file = true;
            }

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                //string line = sr.ReadLine();
                string outline = line;
                line = line.TrimStart(new char[] { ' ' });

                //Check for preprocessor directives
                if (line.StartsWith("#include"))
                {
                    split = line.Split(' ');

                    if (split.Length != 2)
                        throw new ApplicationException("Wrong Usage of #include directive");

                    //get included filepath
                    string npath = split[1].Trim('"');
                    npath = npath.TrimStart('/');
                    npath = Path.Combine(relpath, npath);
                    outline = Parser(npath, initWatchers);
                }
                //Skip Comments
                else if (line.StartsWith("///")) continue;

                //Finally append the parsed text
                text += outline + '\n';
                //sw.WriteLine(outline);
            }
            //CLose readwrites

            sr.Close();
            if (use_tmp_file)
            {
                File.Delete(tmp_file);
            }
            return text;
        }

        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    _dynamicTextParts.Clear();
                    _staticTextParts.Clear();
                }

                disposed = true;
                base.Dispose(disposing);
            }
        }
    }

    public struct GLSLSamplerState
    {
        public int TextureID;
        public NbTextureTarget Target;

        public GLSLSamplerState(int id, NbTextureTarget target)
        {
            TextureID = id;
            Target = target;
        }
    }

    public struct GLSLShaderState
    {
        public Dictionary<string, NbVector3> Vec3s;
        public Dictionary<string, NbVector4> Vec4s;
        public Dictionary<string, float> Floats;
        public Dictionary<string, GLSLSamplerState> Samplers;

        public static GLSLShaderState Create()
        {
            GLSLShaderState state;
            state.Vec3s = new();
            state.Vec4s = new();
            state.Floats = new();
            state.Samplers = new();

            return state;
        }

        public void AddUniform(string name, NbVector3 vec)
        {
            Vec3s[name] = vec;
        }

        public void AddUniform(string name, NbVector4 vec)
        {
            Vec4s[name] = vec;
        }

        public void AddUniform(string name, float val)
        {
            Floats[name] = val;
        }

        public void AddSampler(string name, GLSLSamplerState val)
        {
            Samplers[name] = val;
        }

        public void Clear()
        {
            Vec3s.Clear();
            Vec4s.Clear();
            Floats.Clear();
            Samplers.Clear();
        }

    }


    public class GLSLShaderConfig : Entity
    {
        public string Name = "";

        //Store the raw shader text objects temporarily
        public GLSLShaderSource VSText;
        public GLSLShaderSource FSText;
        public GLSLShaderSource GSText;
        public GLSLShaderSource TCSText;
        public GLSLShaderSource TESText;

        public List<string> directives = new();

        //Store the raw shader text temporarily
        public SHADER_TYPE shader_type = SHADER_TYPE.NULL_SHADER;

        public SHADER_MODE ShaderMode = SHADER_MODE.DEFAULT;
        //Publically hold the filepaths of the shaders
        //For now I am keeping the paths in the filewatcher
        
        //Program ID
        public int ProgramID = -1;
        public int Hash = -1; //Should contain the hashcode of all the material related preprocessor flags (is set externally)
        //Shader Compilation log
        public string CompilationLog = "";

        public GLSLShaderState CurrentState = GLSLShaderState.Create(); //Empty state

        //Keep active uniforms
        public Dictionary<string, int> uniformLocations = new Dictionary<string, int>();

        public GLSLShaderConfig(SHADER_TYPE type, GLSLShaderSource vvs, 
            GLSLShaderSource ffs, GLSLShaderSource ggs, GLSLShaderSource ttcs, GLSLShaderSource ttes, List<string> directives) : base(EntityType.Shader)
        {
            shader_type = type; //Set my custom shader type for recognition
            
            //Store objects
            FSText = ffs;
            GSText = ggs;
            VSText = vvs;
            TESText = ttes;
            TCSText = ttcs;
            
            foreach (string d in directives)
                this.directives.Add(d);
            
        }

        public void ClearCurrentState()
        {
            CurrentState.Clear();
        }
    }


    public static class GLShaderHelper
    {
        static public string NumberLines(string s)
        {
            if (s == "")
                return s;
                
            string n_s = "";
            string[] split = s.Split('\n');

            for (int i = 0; i < split.Length; i++)
                n_s += (i + 1).ToString() + ": " + split[i] + "\n";
            
            return n_s;
        }

        //Shader Compilation

        //GLPreparation
        public static int calculateShaderHash(List<string> includes)
        {
            string hash = "";
            
            for (int i = 0; i < includes.Count; i++)
                hash += includes[i].ToString();
            
            if (hash == "")
                hash = "DEFAULT";

            return hash.GetHashCode();
        }

        public static GLSLShaderConfig compileShader(GLSLShaderSource vs, GLSLShaderSource fs, 
                                                     GLSLShaderSource gs, GLSLShaderSource tcs,
                                                     GLSLShaderSource tes, List<string> defines,
            List<string> includes, SHADER_TYPE type, SHADER_MODE mode)
        {

            List<string> extraDirectives = new();
            
            //General Directives are provided here
            if ((mode & SHADER_MODE.DEFFERED) == SHADER_MODE.DEFFERED)
                extraDirectives.Add("_D_DEFERRED_RENDERING");
            
            //Pass extra definitions
            foreach (string  f in defines)
                extraDirectives.Add(f);
            
            //Material Flags are provided here
            foreach (string  f in includes)
                extraDirectives.Add(f);
            
            GLSLShaderConfig shader_conf = new GLSLShaderConfig(type, vs, fs, gs, tcs, tes, extraDirectives);
            compileShader(shader_conf);
            shader_conf.Hash = calculateShaderHash(extraDirectives);
            
            return shader_conf;
        }

        //This method attaches UBOs to shader binding points
        public static void attachUBOToShaderBindingPoint(GLSLShaderConfig shader_conf, string var_name, int binding_point)
        {
            int shdr_program_id = shader_conf.ProgramID;
            int ubo_index = GL.GetUniformBlockIndex(shdr_program_id, var_name);
            GL.UniformBlockBinding(shdr_program_id, ubo_index, binding_point);
        }

        public static void attachSSBOToShaderBindingPoint(GLSLShaderConfig shader_conf, string var_name, int binding_point)
        {
            //Binding Position 0 - Matrices UBO
            int shdr_program_id = shader_conf.ProgramID;
            int ssbo_index = GL.GetProgramResourceIndex(shdr_program_id, ProgramInterface.ShaderStorageBlock, var_name);
            GL.ShaderStorageBlockBinding(shader_conf.ProgramID, ssbo_index, binding_point);
        }

        public static void reportUBOs(GLSLShaderConfig shader_conf)
        {
            //Print Debug Information for the UBO
            // Get named blocks info
            int test_program = shader_conf.ProgramID;
            GL.GetProgram(test_program, GetProgramParameterName.ActiveUniformBlocks, out int count);

            for (int i = 0; i < count; ++i)
            {
                // Get blocks name
                GL.GetActiveUniformBlockName(test_program, i, 256, out int length, out string block_name);
                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockDataSize, out int block_size);
                Console.WriteLine("Block {0} Data Size {1}", block_name, block_size);

                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockBinding, out int block_bind_index);
                Console.WriteLine("    Block Binding Point {0}", block_bind_index);

                GL.GetInteger(GetIndexedPName.UniformBufferBinding, block_bind_index, out int info);
                Console.WriteLine("    Block Bound to Binding Point: {0} {{", info);

                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockActiveUniforms, out int block_active_uniforms);
                int[] uniform_indices = new int[block_active_uniforms];
                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockActiveUniformIndices, uniform_indices);


                int[] uniform_types = new int[block_active_uniforms];
                int[] uniform_offsets = new int[block_active_uniforms];
                int[] uniform_sizes = new int[block_active_uniforms];

                //Fetch Parameters for all active Uniforms
                GL.GetActiveUniforms(test_program, block_active_uniforms, uniform_indices, ActiveUniformParameter.UniformType, uniform_types);
                GL.GetActiveUniforms(test_program, block_active_uniforms, uniform_indices, ActiveUniformParameter.UniformOffset, uniform_offsets);
                GL.GetActiveUniforms(test_program, block_active_uniforms, uniform_indices, ActiveUniformParameter.UniformSize, uniform_sizes);

                for (int k = 0; k < block_active_uniforms; ++k)
                {
                    GL.GetActiveUniformName(test_program, uniform_indices[k], 256, out int actual_name_length, out string name);
                    Console.WriteLine("\t{0}", name);

                    Console.WriteLine("\t\t    type: {0}", uniform_types[k]);
                    Console.WriteLine("\t\t    offset: {0}", uniform_offsets[k]);
                    Console.WriteLine("\t\t    size: {0}", uniform_sizes[k]);

                    /*
                    GL.GetActiveUniforms(test_program, i, ref uniform_indices[k], ActiveUniformParameter.UniformArrayStride, out uniArrayStride);
                    Console.WriteLine("\t\t    array stride: {0}", uniArrayStride);

                    GL.GetActiveUniforms(test_program, i, ref uniform_indices[k], ActiveUniformParameter.UniformMatrixStride, out uniMatStride);
                    Console.WriteLine("\t\t    matrix stride: {0}", uniMatStride);
                    */
                }
                Console.WriteLine("}}");
            }

        }

        public static void compileShader(GLSLShaderConfig config)
        {
            if (config.ProgramID != -1)
                GL.DeleteProgram(config.ProgramID);
            CreateShaders(config);
        }


        //Shader Creation
        static public void CreateShaders(GLSLShaderConfig config)
        {
            bool gsflag = false;
            bool tsflag = false;

            if (config.GSText != null)
                gsflag = true;
                
            if (!((config.TCSText == null) & (config.TESText == null))) tsflag = true;

            //Convert directives array to string
            string directivestring = "";
            foreach (string dir in config.directives)
                directivestring += "#define " + dir + '\n';
            
            //Compile vertex shader
            
            if (config.VSText != null)
            {
                config.VSText.Compile(ShaderType.VertexShader, directivestring);
            }

            if (config.FSText != null)
            {
                config.FSText.Compile(ShaderType.FragmentShader, directivestring);
            }

            if (config.GSText != null)
            {
                config.GSText.Compile(ShaderType.GeometryShader);
            }

            if (config.TESText != null)
            {
                config.TESText.Compile(ShaderType.TessEvaluationShader);
            }

            if (config.TCSText != null)
            {
                config.TCSText.Compile(ShaderType.TessControlShader);
            }
            
            //Create new program
            config.ProgramID = GL.CreateProgram();

            //Attach shaders to program
            GL.AttachShader(config.ProgramID, config.VSText.shader_object_id);
            GL.AttachShader(config.ProgramID, config.FSText.shader_object_id);

            if (gsflag)
                GL.AttachShader(config.ProgramID, config.GSText.shader_object_id);
            
            if (tsflag)
            {
                GL.AttachShader(config.ProgramID, config.TCSText.shader_object_id);
                GL.AttachShader(config.ProgramID, config.TCSText.shader_object_id);
            }

            GL.LinkProgram(config.ProgramID);

            //Check Linking
            GL.GetProgramInfoLog(config.ProgramID, out string info);
            config.CompilationLog += info + "\n";
            
                
            GL.GetProgram(config.ProgramID, GetProgramParameterName.LinkStatus, out int status_code);
            if (status_code != 1)
                throwCompilationError(config.CompilationLog);

            ShaderCompilationLog(config);
            loadActiveUniforms(config);
        }


        static private void loadActiveUniforms(GLSLShaderConfig shader_conf)
        {
            
            GL.GetProgram(shader_conf.ProgramID, GetProgramParameterName.ActiveUniforms, out int active_uniforms_count);

            shader_conf.uniformLocations.Clear(); //Reset locataions
            shader_conf.CompilationLog += "Active Uniforms: " + active_uniforms_count.ToString() + "\n";
            for (int i = 0; i < active_uniforms_count; i++)
            {
                int bufSize = 64;
                int loc;

                GL.GetActiveUniform(shader_conf.ProgramID, i, bufSize, out int size, out int length, out ActiveUniformType type, out string name);
                loc = GL.GetUniformLocation(shader_conf.ProgramID, name);
                shader_conf.uniformLocations[name] = loc; //Store location
                
                if (RenderState.enableShaderCompilationLog)
                {
                    string info_string = String.Format("Uniform # {0} Location: {1} Type: {2} Name: {3} Length: {4} Size: {5}",
                    i, loc, type.ToString(), name, length, size);
                    shader_conf.CompilationLog += info_string + "\n";
                }
            }
        }
        
        static public void modifyShader(GLSLShaderConfig shader_conf, GLSLShaderSource shaderText)
        {
            throw new Exception("TODO");
            /*
            Console.WriteLine("Actually Modifying Shader");

            int[] attached_shaders = new int[20];
            GL.GetAttachedShaders(shader_conf.ProgramID, 20, out int count, attached_shaders);

            for (int i = 0; i < count; i++)
            {
                int[] shader_params = new int[10];
                GL.GetShader(attached_shaders[i], OpenTK.Graphics.OpenGL4.ShaderParameter.ShaderType, shader_params);

                if (shader_params[0] == (int) shaderText.Type)
                {
                    Console.WriteLine("Found modified shader");

                    //Trying to compile shader
                    shaderText.Compile();

                    //Attach new shader back to program
                    GL.DetachShader(shader_conf.ProgramID, attached_shaders[i]);
                    GL.AttachShader(shader_conf.ProgramID, shaderText.shader_object_id);
                    GL.LinkProgram(shader_conf.ProgramID);

                    GL.GetProgram(shader_conf.ProgramID, GetProgramParameterName.LinkStatus, out int status_code);
                    if (status_code != 1)
                    {
                        Console.WriteLine("Unable to link the new shader. Reverting to the old shader");
                        return;
                    }

                    //Delete old shader and reload uniforms
                    loadActiveUniforms(shader_conf); //Re-load active uniforms
                    Console.WriteLine("Shader was modified successfully");
                    break;
                }
            }
            Console.WriteLine("Shader was not found...");
            */
        }


        public static void ShaderCompilationLog(GLSLShaderConfig conf)
        {
            string log_file = "shader_compilation_log.out";

            if (!File.Exists(log_file))
                File.Create(log_file).Close();
            
            while (!FileUtils.IsFileReady(log_file))
            {
                Console.WriteLine("Log File not ready yet");
            };
            
            StreamWriter sr = new StreamWriter(log_file, true);
            sr.WriteLine("### COMPILING " + conf.shader_type + "###");
            sr.Write(conf.CompilationLog);
            sr.Close();
            //Console.WriteLine(conf.log);
        }

        public static void throwCompilationError(string log)
        {
            //Lock execution until the file is available
            string log_file = "shader_compilation_log.out";

            if (!File.Exists(log_file))
                File.Create(log_file);

            while (!FileUtils.IsFileReady(log_file))
            {
                Console.WriteLine("Log File not ready yet");
            };
            
            StreamWriter sr = new StreamWriter(log_file);
            sr.Write(log);
            sr.Close();
            Console.WriteLine(log);
            Callbacks.Assert(false, "Shader Compilation Failed. Check Log");
        }
    }
}


