#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (FileListTest.cs) is part of Oetools.Utilities.Test.
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
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Lib {
    
    [TestClass]
    public class FileListTest {

        [TestMethod]
        public void Test_performances() {
            List<string> files = new List<string>();
            for (int i = 0; i < 10000; i++) {
                files.Add(Path.Combine("C:\\folder\\subfolder\\again\\even\\more\\subfolder\\to\\makeit\\super\\long\\and\\realistic", $"file{i}.ext"));
            }
            for (int i = 0; i < 10000; i++) {
                files.Add(Path.Combine("D:\\folder\\subfolder\\again\\even\\more\\subfolder\\to\\makeit\\super\\long\\and\\realistic", $"file{i}.ext"));
            }
            for (int i = 0; i < 10000; i++) {
                files.Add(Path.Combine("C:\\folder\\subfolder\\again\\even\\THIS FOLDER CHANGES\\subfolder\\to\\makeit\\super\\long\\and\\realistic", $"file{i}.ext"));
            }

            var fileList = new FileList<UoeFileToCompile>();
            foreach (var file in files) {
                fileList.Add(new UoeFileToCompile(file));
            }
            
            Console.WriteLine("FileList done in {0} ms", TestHelper.Time(() => {
                foreach (var file in files) {
                    string found = null;
                    found = fileList[file].SourceFilePath;
                    Assert.AreEqual(file, found);
                }
            }).Milliseconds.ToString());
            
            var dic = new Dictionary<string, UoeFileToCompile>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files) {
                dic.Add(file, new UoeFileToCompile(file));
            }
            
            Console.WriteLine("HashSet done in {0} ms", TestHelper.Time(() => {
                foreach (var file in files) {
                    string found = null;
                    if (dic.ContainsKey(file)) {
                        found = dic[file].SourceFilePath;
                    }
                    Assert.AreEqual(file, found);
                }
            }).Milliseconds.ToString());
            
            //Console.WriteLine("Compare done in {0} ms", TestHelper.Time(() => {
            //    foreach (var file in files) {
            //        string found = null;
            //        foreach (var file1 in files) {
            //            if (file1.EqualsCi(file)) {
            //                found = file1;
            //                break;
            //            }
            //        }
            //        Assert.AreEqual(file, found);
            //    }
            //}).Milliseconds.ToString());
        }

        [TestMethod]
        public void Full_test() {
            var fileList = new FileList<UoeFileToCompile>();
            for (int i = 0; i < 1000; i++) {
                fileList.Add(new UoeFileToCompile(i.ToString()));
            }

            var j = 0;
            foreach (var compile in fileList) {
                Assert.AreEqual(j.ToString(), compile.SourceFilePath);
                j++;
            }
            
            Assert.AreEqual(1000, fileList.Count);
            
            for (int i = 0; i < 1000; i++) {
                Assert.AreEqual(i.ToString(), fileList[i.ToString()].SourceFilePath);
            }
            
            j = 0;
            foreach (var compile in fileList) {
                Assert.AreEqual(j.ToString(), fileList[compile].SourceFilePath);
                j++;
            }
            
            for (int i = 0; i < 1000; i++) {
                Assert.AreEqual(true, fileList.Contains(i.ToString()));
            }
            
            j = 0;
            foreach (var compile in fileList) {
                Assert.AreEqual(true, fileList.Contains(compile));
                j++;
            }
            
            for (int i = 0; i < 1000; i++) {
                Assert.AreEqual(false, fileList.TryAdd(new UoeFileToCompile(i.ToString())));
            }
            
            for (int i = 0; i < 1000; i++) {
                Assert.AreEqual(true, fileList.Remove(new UoeFileToCompile(i.ToString())));
            }
            
            for (int i = 0; i < 1000; i++) {
                Assert.AreEqual(true, fileList.TryAdd(new UoeFileToCompile(i.ToString())));
            }
            
            fileList.Clear();
            
            Assert.AreEqual(0, fileList.Count);

            var tempList = new List<UoeFileToCompile>();
            for (int i = 0; i < 1000; i++) {
                tempList.Add(new UoeFileToCompile(i.ToString()));
            }
            fileList.AddRange(tempList);
            
            Assert.AreEqual(1000, fileList.Count);
        }
    }
}