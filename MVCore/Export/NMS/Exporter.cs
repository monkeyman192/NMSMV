using libMBIN.NMS.Toolkit;

namespace MVCore.Export.NMS
{
    public static class NMSExporter
    {
        public static void exportSceneGraphNodeToEXML(SceneGraphNode node)
        {
            TkSceneNodeData template = exportSceneGraphNodeToTemplate(node);

            if (template != null)
            {
                string filename = template.Name.Value.ToUpper() + ".SCENE.EXML";
                template.WriteToExml(filename);
                Common.Callbacks.showInfo("Scene successfully exported to " + filename, "Info");
            }
        }
        
        public static void exportSceneGraphNodeToMBIN(SceneGraphNode node)
        {
            TkSceneNodeData template = exportSceneGraphNodeToTemplate(node);

            if (template != null)
            {
                string filename = template.Name.Value.ToUpper() + ".SCENE.MBIN";
                template.WriteToMbin(filename);
                Common.Callbacks.showInfo("Scene successfully exported to " + filename, "Info");
            }
        }
        
        public static TkSceneNodeData exportSceneGraphNodeToTemplate(SceneGraphNode node)
        {
            if (node != null)
            {
                //TODO Bring Back MBIN Export Support
                TkSceneNodeData temp = new();
                
                //TODO
                //Populate temp with data from node
                
                return temp;
            }
            
            return null;
        }
        
        
        
    }
}