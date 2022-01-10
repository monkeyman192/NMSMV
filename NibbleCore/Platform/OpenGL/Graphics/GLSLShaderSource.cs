using System;
using System.Collections.Generic;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using NbCore.Common;
using System.Reflection;


namespace NbCore.Platform.Graphics.OpenGL
{
    public class GLSLShaderSource : Entity
    {
        public string Name = "";
        private List<GLSLShaderSource> _dynamicTextParts = new();
        private List<string> _staticTextParts = new();
        public string SourceFilePath = "";
        public string SourceText = "";
        private List<string> _Directives = new();
        private FileSystemWatcher _watcher;
        private HashSet<string> _watchFiles = new();
        private DateTime LastReadTime;
        public string ResolvedText = ""; //Full shader text after resolving
        public string ActualShaderSource = "";
        public int shader_object_id = -1;
        public bool Resolved = false;
        public bool Processed = false;
        public ShaderSourceType SourceType;

        public string CompilationLog = ""; //Keep track of the generated log during shader compilation

        public List<GLSLShaderConfig> ReferencedByShaders = new(); //Keeps track of all the Shaders that the current source is used by
        public List<GLSLShaderSource> ReferencedSources = new(); //Keep source texts that the current text refers to
        public List<GLSLShaderSource> ReferencedBySources = new(); //Keep source texts that reference this source

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

            SourceFilePath = Utils.FileUtils.FixPath(filepath);
            SourceText = File.ReadAllText(SourceFilePath);
            LastReadTime = DateTime.Now;
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
            fw.Changed += file_changed;
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

            if (shader_object_id != -1)
                GL.DeleteShader(shader_object_id);
            
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
                    }
                    else
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
            }
            else //Dynamic Sources
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
                        GLSLShaderSource ss = RenderState.engineRef.GetShaderSourceByFilePath(npath);
                        if (ss == null)
                        {
                            Console.WriteLine($"Loading new dynamic source {npath}");
                            ss = new GLSLShaderSource(npath, true);
                        }
                        if (!ss.Processed)
                            ss.Process();
                        
                        ReferencedSources.Add(ss);
                        ss.ReferencedBySources.Add(this);
                        
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
            FileSystemWatcher fw = (FileSystemWatcher)sender;
            string path = Path.Combine(fw.Path, fw.Filter);

            lock (_watchFiles)
            {
                if (_watchFiles.Count > 0)
                {
                    Console.WriteLine($"reload in process..");
                    return;
                }
                else
                {
                    Console.WriteLine($"Adding File..");
                    _watchFiles.Add(path);
                }   
            }

            int openfiletries = 0;
            while (true)
            {
                try
                {
                    string NewSourceText = File.ReadAllText(SourceFilePath);

                    if (NewSourceText == SourceText)
                        Console.WriteLine($"Same Source, nothing to do...");
                    else
                    {
                        Console.WriteLine($"Reloading {path} Change: {e.ChangeType.ToString()}");
                        SourceText = NewSourceText;
                        Process();
                        Resolve();

                        foreach (GLSLShaderSource rc in ReferencedBySources)
                        {
                            rc.Resolved = false;
                            rc.Resolve();

                            foreach (GLSLShaderConfig shader in rc.ReferencedByShaders)
                            {
                                RenderState.engineRef.renderSys.ShaderMgr.AddShaderForCompilation(shader);
                            }

                        };

                        foreach (GLSLShaderConfig shader in ReferencedByShaders)
                        {
                            RenderState.engineRef.renderSys.ShaderMgr.AddShaderForCompilation(shader);
                        }

                    }
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception {ex.Message}");
                }
                finally
                {
                    openfiletries++;
                }
            }
            
            lock (_watchFiles)
            {
                _watchFiles.Clear();
            }
            Console.WriteLine($"Shader Reload Complete.");
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

}
