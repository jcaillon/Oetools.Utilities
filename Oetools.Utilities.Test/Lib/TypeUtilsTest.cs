#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (TypeUtilsTest.cs) is part of Oetools.Utilities.Test.
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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Test.Lib {
    
    [TestClass]
    public class TypeUtilsTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(TypeUtilsTest)));
                     
        [ClassInitialize]
        public static void Init(TestContext context) {
            Cleanup();
            Utils.CreateDirectoryIfNeeded(TestFolder);
        }


        [ClassCleanup]
        public static void Cleanup() {
            Utils.DeleteDirectoryIfExists(TestFolder, true);
        }

        [TestMethod]
        public void ReplacePlaceHoldersInAllPublicProperties_Test() {
            var instance = new Obj1 {
                Prop1 = new Obj2 {
                    Prop1 = "cool1",
                    Prop2 = new List<Obj3> {
                        new Obj3 {
                            Prop1 = "cool2",
                            Prop2 = null
                        },
                        null,
                        new Obj3 {
                            Prop1 = null,
                            Prop2 = "cool3"
                        }
                    },
                    Prop3 = new [] {
                        "cool9",
                        null,
                        "cool10"
                    }
                },
                Prop2 = new List<Obj3> {
                    new Obj3 {
                        Prop1 = "cool4",
                        Prop2 = null
                    },
                    null,
                    new Obj3 {
                        Prop1 = null,
                        Prop2 = "cool5"
                    }
                },
                Prop3 = "cool6",
                Prop4 = 10,
                Prop5 = new List<string> {
                    "cool7",
                    null,
                    "cool8"
                }
            };
            Utils.ForEachPublicPropertyStringInObject(typeof(Obj1), instance, (t, s) => {
                return s?.Replace("cool", "nice");
            });
            Assert.AreEqual("nice1", instance.Prop1.Prop1);
            Assert.AreEqual("nice2", instance.Prop1.Prop2.ToList()[0].Prop1);
            Assert.AreEqual("nice3", instance.Prop1.Prop2.ToList()[2].Prop2);
            Assert.AreEqual("nice9", instance.Prop1.Prop3[0]);
            Assert.AreEqual("nice10", instance.Prop1.Prop3[2]);
            Assert.AreEqual("nice4", instance.Prop2[0].Prop1);
            Assert.AreEqual("nice5", instance.Prop2[2].Prop2);
            Assert.AreEqual("nice6", instance.Prop3);
            Assert.AreEqual("nice7", instance.Prop5[0]);
            Assert.AreEqual("nice8", instance.Prop5[2]);
            
            // test [ReplacePlaceHolder(SkipReplace = true)]
            
            var instance2 = new Obj4 {
                Prop1 = new Obj2 {
                    Prop1 = "cool1",
                    Prop2 = new List<Obj3> {
                        new Obj3 {
                            Prop1 = "cool2",
                            Prop2 = null
                        },
                        null,
                        new Obj3 {
                            Prop1 = null,
                            Prop2 = "cool3"
                        }
                    },
                    Prop3 = new [] {
                        "cool9",
                        null,
                        "cool10"
                    }
                },
                Prop2 = new List<Obj3> {
                    new Obj3 {
                        Prop1 = "cool4",
                        Prop2 = null
                    },
                    null,
                    new Obj3 {
                        Prop1 = null,
                        Prop2 = "cool5"
                    }
                },
                Prop3 = "cool6",
                Prop4 = 10,
                Prop5 = new List<string> {
                    "cool7",
                    null,
                    "cool8"
                }
            };
            Utils.ForEachPublicPropertyStringInObject(typeof(Obj4), instance2, (t, s) => {
                return s?.Replace("cool", "nice");
            });
            Assert.AreEqual("cool1", instance2.Prop1.Prop1);
            Assert.AreEqual("cool2", instance2.Prop1.Prop2.ToList()[0].Prop1);
            Assert.AreEqual("cool3", instance2.Prop1.Prop2.ToList()[2].Prop2);
            Assert.AreEqual("cool9", instance2.Prop1.Prop3[0]);
            Assert.AreEqual("cool10", instance2.Prop1.Prop3[2]);
            Assert.AreEqual("nice4", instance2.Prop2[0].Prop1);
            Assert.AreEqual("nice5", instance2.Prop2[2].Prop2);
            Assert.AreEqual("cool6", instance2.Prop3);
            Assert.AreEqual("nice7", instance2.Prop5[0]);
            Assert.AreEqual("nice8", instance2.Prop5[2]);
        }

        private class Obj1 {
            
            public Obj2 Prop1 { get; set; }
            
            public List<Obj3> Prop2 { get; set; }
            
            public string Prop3 { get; set; }
            
            public int Prop4 { get; set; }
            
            public List<string> Prop5 { get; set; }
        }

        private class Obj2 {
            
            public string Prop1 { get; set; }
            
            public IEnumerable<Obj3> Prop2 { get; set; }
            
            public string[] Prop3 { get; set; }
        }

        private class Obj3 {
            
            public string Prop1 { get; set; }
            
            public string Prop2 { get; set; }
        }
        
        private class Obj4 {
            
            [ReplacePlaceHolder(SkipReplace = true)]
            public Obj2 Prop1 { get; set; }
            
            public List<Obj3> Prop2 { get; set; }
            
            [ReplacePlaceHolder(SkipReplace = true)]
            public string Prop3 { get; set; }
            
            public int Prop4 { get; set; }
            
            public List<string> Prop5 { get; set; }
        }
    }
}