#version 330
/* Copies incoming vertex color without change.
 * Applies the transformation matrix to vertex position.
 */

layout(location=0) in vec4 vPosition;
layout(location=1) in vec3 vcolor;
uniform mat4 mvp, worldMat;

out vec3 color;

void main()
{
    //Set color
    color = vcolor;
	gl_Position = mvp * vPosition;
}