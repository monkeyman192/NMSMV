﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Model_Viewer.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Model_Viewer.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to #version 330
        ////* Copies incoming fragment color without change. */
        ///in vec3 color;
        ///void main()
        ///{	
        ///	gl_FragColor = vec4(color, 1.0);
        ///}.
        /// </summary>
        internal static string joint_frag {
            get {
                return ResourceManager.GetString("joint_frag", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to #version 330
        ////* Copies incoming vertex color without change.
        /// * Applies the transformation matrix to vertex position.
        /// */
        ///
        ///layout(location=0) in vec4 vPosition;
        ///layout(location=1) in vec3 vcolor;
        ///uniform mat4 mvp, worldMat;
        ///
        ///out vec3 color;
        ///
        ///void main()
        ///{
        ///    //Set color
        ///    color = vcolor;
        ///	gl_Position = mvp * vPosition;
        ///}.
        /// </summary>
        internal static string joint_vert {
            get {
                return ResourceManager.GetString("joint_vert", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to #version 330
        ////* Copies incoming vertex color without change.
        ///*/
        ///void main()
        ///{
        ///	//Yellow point lights
        ///	gl_FragColor = vec4(1.0, 1.0, 0.0, 1.0);
        ///}.
        /// </summary>
        internal static string light_frag {
            get {
                return ResourceManager.GetString("light_frag", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to #version 330
        ///
        ///layout(location=0) in vec4 vPosition;
        ///uniform mat4 look, proj;
        ///uniform vec3 theta;
        ///
        ///void main()
        ///{
        ///	vec3 angles = radians( theta );
        ///    vec3 c = cos( angles );
        ///    vec3 s = sin( angles );
        ///    
        ///	// Remeber: thse matrices are column-major
        ///    mat4 rx = mat4( 1.0,  0.0,  0.0, 0.0,
        ///            		0.0,  c.x,  s.x, 0.0,
        ///            		0.0, -s.x,  c.x, 0.0,
        ///            		0.0,  0.0,  0.0, 1.0 );
        ///
        ///    mat4 ry = mat4( c.y, 0.0, -s.y, 0.0,
        ///            0.0, 1.0,  0.0, 0.0,
        ///            s.y [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string light_vert {
            get {
                return ResourceManager.GetString("light_vert", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /* Copies incoming fragment color without change. */
        ///in vec3 color;
        ///void main()
        ///{	
        ///	gl_FragColor = vec4(color, 1.0);
        ///}.
        /// </summary>
        internal static string locator_frag {
            get {
                return ResourceManager.GetString("locator_frag", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to #version 330
        ///#extension GL_ARB_explicit_uniform_location : enable
        ///#extension GL_ARB_separate_shader_objects : enable
        ///
        ///layout(location=0) in vec4 vPosition;
        ///layout(location=1) in vec3 vcolor;
        ///uniform mat4 mvp, worldMat;
        ///out vec3 color;
        ///
        ///void main()
        ///{
        ///    //Set color
        ///    color = vcolor;
        ///    gl_Position = mvp * worldMat * vPosition;
        ///}.
        /// </summary>
        internal static string locator_vert {
            get {
                return ResourceManager.GetString("locator_vert", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to #version 330
        ///#extension GL_ARB_explicit_uniform_location : enable
        ///flat in int object_id;
        /////flat int object_id;
        ///layout(location=0) out vec4 outcolor;
        ///
        ///void main()
        ///{	
        ///	
        ///	//Calculate fragcolor from object_id
        ///	vec4 color = vec4(0.0, 0.0, 0.0, 1.0);
        ///
        ///	color.r = float(object_id &amp; 0xFF) /255.0;
        ///	color.g = float((object_id&gt;&gt;8) &amp; 0xFF) /255.0;
        ///	color.b = 0.0;
        ///
        ///	//pickColor = color;
        ///	outcolor = color;
        ///}.
        /// </summary>
        internal static string pick_frag {
            get {
                return ResourceManager.GetString("pick_frag", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to #version 330
        ///#extension GL_ARB_explicit_uniform_location : enable
        ///#extension GL_ARB_separate_shader_objects : enable
        ////* Copies incoming vertex color without change.
        /// * Applies the transformation matrix to vertex position.
        /// */
        ///layout(location=0) in vec4 vPosition;
        ///layout(location=1) in vec2 uvPosition0;
        ///layout(location=2) in vec4 nPosition; //normals
        ///layout(location=3) in vec4 tPosition; //tangents
        ///layout(location=4) in vec4 bPosition; //bitangents
        ///layout(location=5) in vec4 blendIndices;
        ///layout( [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string pick_vert {
            get {
                return ResourceManager.GetString("pick_vert", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /* Copies incoming fragment color without change. */
        ///uniform sampler2D diffuseTex;
        ///varying vec2 uv0;
        ///varying float dx,dy;
        ///
        ///void main()
        ///{	
        ///	
        ///	/*
        ///		Character Maps are usually full white textures with
        ///		black characters. The Shader should invert the colors
        ///		and use the input color to recolour the letters
        ///	*/
        ///
        ///	vec4 color = textureLod(diffuseTex, uv0, 0.0);
        ///	//color = vec4(vec3(1.0, 1.0, 1.0) - color.rgb, color.a);
        ///
        ///	//color -= dFdx(color) * dx;
        ///	//color -= dFdy(color) * dy;
        ///	//color += dFdx [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string text_frag {
            get {
                return ResourceManager.GetString("text_frag", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /* Simple Quad Rendering Shader
        /// */
        ///attribute vec4 vPosition;
        ///attribute vec4 uvPosition;
        /////Outputs
        ///varying vec2 uv0;
        ///varying float dx, dy;
        ///uniform mat4 projMat;
        ///uniform float w, h;
        /////Text Transforms
        ///uniform vec2 pos;
        ///uniform float scale;
        ///
        ///
        ///
        ///void main()
        ///{
        ///    //uv0 = vPosition.xy * vec2(0.5, 0.5) + vec2(0.5, 0.5);
        ///    uv0 = uvPosition.xy;
        ///    uv0.y = 1.0 - uv0.y;
        ///    dx = 2.0/w;
        ///    dy = 2.0/h;
        ///    //Render to UV coordinate
        ///    mat4 projmat = mat4(400.0/w, 0.0,    0.0, 0.0,
        ///           [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string text_vert {
            get {
                return ResourceManager.GetString("text_vert", resourceCulture);
            }
        }
    }
}
