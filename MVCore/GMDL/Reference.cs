using System;
using System.Collections.Generic;
using System.Text;
using libMBIN.NMS.Toolkit;


namespace MVCore.GMDL
{
    public class Reference : Locator
    {
        public Entity ref_scene; //holds the referenced scene

        public Reference()
        {
            Type = TYPES.REFERENCE;
        }

        public Reference(Reference input)
        {
            //Copy info
            base.copyFrom(input);

            ref_scene = input.ref_scene.Clone();
            ref_scene.Parent = this;
            Children.Add(ref_scene);
        }

        public void copyFrom(Reference input)
        {
            base.copyFrom(input); //Copy base stuff
            this.ref_scene = input.ref_scene;
        }

        
        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                //Free other resources here
                base.Dispose(disposing);
            }

            //Free unmanaged resources
            disposed = true;
        }
    }
}
