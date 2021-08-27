using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;
using MVCore.Common;
using MVCore.Systems;

namespace MVCore
{
    public class Locator : Model
    {
        public GLInstancedMeshVao meshVao;

        public Locator()
        {
            //Set type
            Type = TYPES.LOCATOR;
            
            //Assemble geometry in the constructor
            meshVao = RenderState.activeResMgr.GLPrimitiveMeshVaos["default_cross"];
            instanceId = GLMeshBufferManager.AddInstance(ref meshVao, this);
        }

        public void copyFrom(Locator input)
        {
            actionComponentID = input.actionComponentID;
            base.copyFrom(input); //Copy stuff from base class
        }

        protected Locator(Locator input) : base(input)
        {
            this.copyFrom(input);
        }

        public override Model Clone()
        {
            Locator new_s = new Locator();
            new_s.copyFrom(this);
            
            //Clone children
            foreach (Model child in Children)
            {
                Model new_child = child.Clone();
                new_child.parent = new_s;
                new_s.Children.Add(new_child);
            }

            return new_s;
        }

        public override void update()
        {
            base.update();
            recalculateAABB(); //Update AABB
        }

        public override void updateMeshInfo(bool lod_filter=false)
        {
            if (!renderable || lod_filter)
            {
                base.updateMeshInfo(lod_filter);
                return;
            }


            Matrix4 worldMat = TransformationSystem.GetEntityWorldMat(this);
            bool fr_status = RenderState.activeCam.frustum_occlude(meshVao, worldMat * RenderState.rotMat);
            bool occluded_status = !fr_status && RenderState.settings.renderSettings.UseFrustumCulling;

            //Recalculations && Data uploads
            if (!occluded_status)
            {
                instanceId = GLMeshBufferManager.AddInstance(ref meshVao, this);
            }

            base.updateMeshInfo();
        }


        #region IDisposable Support
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    meshVao = null; //VAO will be deleted from the resource manager since it is a common mesh
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }
        #endregion

    }
}
