using System;
using System.Collections.Generic;
using Assimp;
using MVCore;
using MVCore.Systems;
using MVCore.Utils;

namespace MVCore.Export
{
    public class AssimpExporter
    {
        public static Node assimpExport(Entity m, ref Assimp.Scene scn, ref Dictionary<int, int> meshImportStatus)
        {

            //Default shit
            //Create assimp node
            Node node = new Node(m.Name);
            node.Transform = MathUtils.convertMatrix(TransformationSystem.GetEntityLocalMat(this));

            //Handle animations maybe?
            if (m.HasComponent<AnimComponent>())
            {
                AnimComponent cmp = m.GetComponent<AnimComponent>() as AnimComponent;
                cmp.assimpExport(ref scn);
            }
            
            foreach (Entity child in m.Children)
            {
                Node c = assimpExport(child, ref scn, ref meshImportStatus);
                node.Children.Add(c);
            }

            return node;
        }


    }
}
