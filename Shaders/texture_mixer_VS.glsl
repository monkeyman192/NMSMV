/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

layout(location = 0) in vec4 vPosition;
layout(location = 1) in vec3 vColor;

varying vec2 uv0;
varying vec3 color;

void main()
{
	color = vColor.xyz;
    uv0 = vPosition.xy * vec2(0.5, 0.5) + vec2(0.5, 0.5);
    //Render to UV coordinate
    float w,h;
    w = 1.0;
    h = 1.0;
    mat4 projMat = mat4(2.0/w, 0.0,  0.0, 0.0,
                        0.0, 2.0/h,  0.0, 0.0,
                        0.0, 0.0, -2.0/1.0, 0.0,
                        0.0, 0.0,  0.0, 1.0);

    gl_Position = vec4(vPosition.xyz, 1.0);
}