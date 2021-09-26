/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

 
/* Copies incoming fragment color without change. */

//Diffuse Textures
uniform sampler2DArray InTex;
uniform float texture_depth;
uniform float mipmap;

in vec2 uv0;
out vec4 fragColour; 

void main()
{
	//fragColour = texelFetch(InTex, ivec3(gl_FragCoord.xy, texture_depth), mipmap);
    fragColour = textureLod(InTex, vec3(uv0, texture_depth), mipmap);
}
