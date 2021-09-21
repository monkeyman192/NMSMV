using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MVCore.Utils;
using System.Net;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System.Reflection;


namespace Viewer_Unit_Tests
{
    [TestClass]
    public class UnitTest5
    {
        [TestMethod]
        public void fetchLibMBINDLL()
        {

            HTMLUtils.fetchLibMBINDLL();
            
        }
    }
}
