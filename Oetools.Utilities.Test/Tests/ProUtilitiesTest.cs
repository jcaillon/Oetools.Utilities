#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UtilsTest.cs) is part of Oetools.Utilities.Test.
// 
// Oetools.Utilities.Test is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities.Test is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities.Test. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Openedge;

namespace Oetools.Utilities.Test.Tests {
    
    [TestClass]
    public class ProUtilitiesTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(ProUtilitiesTest)));

        [ClassInitialize]
        public static void Init(TestContext context) {           
            Cleanup();
            Directory.CreateDirectory(TestFolder);
        }
        
        [ClassCleanup]
        public static void Cleanup() {
            if (Directory.Exists(TestFolder)) {
                Directory.Delete(TestFolder, true);
            }
        }
        
        [TestMethod]
        public void GetProPathFromIniFile_TestEnvVarReplacement() {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return;
            }
            
            var iniPath = Path.Combine(TestFolder, "test.ini");
            File.WriteAllText(iniPath, "[Startup]\nPROPATH=t:\\error:exception\";C:\\Windows,%TEMP%;z:\\nooooop");

            var list = ProUtilities.GetProPathFromIniFile(iniPath, TestFolder);

            Assert.AreEqual(2, list.Count);
            Assert.IsTrue(list.ToList().Exists(s => s.Equals("C:\\Windows")));
            Assert.IsTrue(list.ToList().Exists(s => s.Equals(Environment.GetEnvironmentVariable("TEMP"))));
        }
        
        [TestMethod]
        public void GetProPathFromBaseDirectory_Test() {
            Directory.CreateDirectory(Path.Combine(TestFolder, "test1"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test2"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test3"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test3", "test4"));
            var dirInfo = Directory.CreateDirectory(Path.Combine(TestFolder, "test1_hidden"));
            dirInfo.Attributes |= FileAttributes.Hidden;
            dirInfo = Directory.CreateDirectory(Path.Combine(TestFolder, "test2_hidden"));
            dirInfo.Attributes |= FileAttributes.Hidden;

            var list = ProUtilities.GetProPathFromBaseDirectory(TestFolder);

            Assert.AreEqual(4, list.Count);
            Assert.IsFalse(list.ToList().Exists(s => s.Contains("_hidden")));
        }

    }
}