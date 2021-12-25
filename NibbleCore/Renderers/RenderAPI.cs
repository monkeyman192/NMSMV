using System.Collections.Generic;
using NbCore.Common;
using NbCore.Math;
using NbOpenGLAPI;

namespace NbCore
{
    public enum NbBufferMask
    {
        Color,
        Depth
    }

    public interface IRenderApi
    {
        public void Init();
        public void SetProgram(int progra_id);
        public void ResizeViewport(int width, int height);
        public void AddMesh(NbMesh mesh);
        public void EnableMaterialProgram(MeshMaterial mat);
        public void EnableShaderProgram(GLSLShaderConfig shader);
        public void SetCameraData(Camera cam);
        public void SetRenderSettings(RenderSettings settings);
        public void SetCommonDataPerFrame(FBO gBuffer, NbMatrix4 rotMat, double time);
        public void SetLightDataPerFrame(List<Entity> lights);
        public void UploadFrameData();
        
        //Mesh Buffer Methods
        public void PrepareMeshBuffers();
        public void UnbindMeshBuffers();
        
        //Rendering Methods
        public void RenderQuad(NbMesh quadMesh, GLSLShaderConfig shaderConf, GLSLShaderState state);
        public void RenderMesh(NbMesh mesh); //Direct mesh rendering, without any shader, uniform uploads
        public void RenderMesh(NbMesh mesh, MeshMaterial mat);
        public void RenderLocator(NbMesh mesh, MeshMaterial mat);
        public void RenderJoint(NbMesh mesh, MeshMaterial mat);
        public void RenderCollision(NbMesh mesh, MeshMaterial mat);
        public void RenderLight(NbMesh mesh, MeshMaterial mat);
        public void RenderLightVolume(NbMesh mesh, MeshMaterial mat);

        //Framebuffer Methods
        public void ClearDrawBuffer(NbBufferMask mask);
        public void BindDrawFrameBuffer(FBO framebuffer, int[] drawBuffers);
        public FBO CreateFrameBuffer(int width, int height);

        //Misc
        public void SyncGPUCommands(); 
    }


}