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
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Attributes;

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
        public void SetDefaultValues_Test() {
            var ojb = new Obj8();
            ojb.Prop6 = new Obj2 {
                Prop1 = "already defined"
            };
            Utils.SetDefaultValues(ojb);
            Assert.IsNotNull(ojb.Prop1);
            Assert.IsNotNull(ojb.Prop2);
            Assert.IsNull(ojb.Prop3);
            Assert.AreEqual(10, ojb.Prop4);
            Assert.IsNull(ojb.Prop5);
            Assert.AreEqual("nice", ojb.Prop1.Prop1);
            Assert.IsNull(ojb.Prop1.Prop2);
            Assert.IsNull(ojb.Prop1.Prop3);
            Assert.IsNull(ojb.Prop2[0].Prop1);
            Assert.AreEqual("cool", ojb.Prop2[0].Prop2);
            Assert.AreEqual("already defined", ojb.Prop6.Prop1);
            
        }

        [TestMethod]
        public void DeepCopyPublicProperties_Test_IgnoreAttribute() {
            var obj = new Obj6 {
                Prop1 = "string1",
                Prop2 = "second2"
            };
            var obj7 = new Obj7 {
                Prop1 = "original1",
                Prop2 = "original2",
            };

            obj.DeepCopy(obj7);
            
            Assert.AreEqual("string1", obj.Prop1);
            Assert.AreEqual("second2", obj.Prop2);
            
            Assert.AreEqual("original1", obj7.Prop1);
            Assert.AreEqual("second2", obj7.Prop2);
        }

        [TestMethod]
        public void DeepCopyPublicProperties_Test() {
            var instance = new Obj5 {
                Prop2 = new List<Obj3> {
                    new Obj3 {
                        Prop1 = "cool1",
                        Prop2 = null
                    },
                    null,
                    new Obj3 {
                        Prop1 = null,
                        Prop2 = "cool2"
                    }
                },
                Prop3 = "cool3",
                Prop4 = 10,
                Prop5 = new List<string> {
                    "cool4",
                    null,
                    "cool5"
                },
                Prop6 = new[] {
                    "cool6", null, "cool7"
                },
                Prop7 = new Obj3 {
                    Prop1 = "cool8",
                    Prop2 = "cool9"
                },
                Prop8 = 45,
                Prop9 = null
            };
            var copy = instance.DeepCopy<Obj5>(null);
            Assert.IsNotNull(copy);
            copy.Prop8 = 8;
            Utils.ForEachPublicPropertyStringInObject(copy.GetType(), copy, (t, s) => {
                return s?.Replace("cool", "nice");
            });
            Assert.AreEqual("nice1", copy.Prop2[0].Prop1);
            Assert.AreEqual(null, copy.Prop2[0].Prop2);
            Assert.AreEqual(null, copy.Prop2[1]);
            Assert.AreEqual(null, copy.Prop2[2].Prop1);
            Assert.AreEqual("nice2", copy.Prop2[2].Prop2);
            Assert.AreEqual("nice3", copy.Prop3);
            Assert.AreEqual("nice4", copy.Prop5[0]);
            Assert.AreEqual(null, copy.Prop5[1]);
            Assert.AreEqual("nice5", copy.Prop5[2]);
            Assert.AreEqual("nice6", copy.Prop6[0]);
            Assert.AreEqual(null, copy.Prop6[1]);
            Assert.AreEqual("nice7", copy.Prop6[2]);
            Assert.AreEqual("nice8", copy.Prop7.Prop1);
            Assert.AreEqual("nice9", copy.Prop7.Prop2);
            Assert.AreEqual(8, copy.Prop8);
            Assert.AreEqual(null, copy.Prop9);

            Assert.AreEqual("cool1", instance.Prop2[0].Prop1);
            Assert.AreEqual(null, instance.Prop2[0].Prop2);
            Assert.AreEqual(null, instance.Prop2[1]);
            Assert.AreEqual(null, instance.Prop2[2].Prop1);
            Assert.AreEqual("cool2", instance.Prop2[2].Prop2);
            Assert.AreEqual("cool3", instance.Prop3);
            Assert.AreEqual("cool4", instance.Prop5[0]);
            Assert.AreEqual(null, instance.Prop5[1]);
            Assert.AreEqual("cool5", instance.Prop5[2]);
            Assert.AreEqual("cool6", instance.Prop6[0]);
            Assert.AreEqual(null, instance.Prop6[1]);
            Assert.AreEqual("cool7", instance.Prop6[2]);
            Assert.AreEqual("cool8", instance.Prop7.Prop1);
            Assert.AreEqual("cool9", instance.Prop7.Prop2);
            Assert.AreEqual(45, instance.Prop8);
            Assert.AreEqual(null, copy.Prop9);

            Console.WriteLine("done in {0} ms", TestHelper.Time(() => {
                var timerTest = new List<object>();
                for (int i = 0; i < 1000; i++) {
                    timerTest.Add(instance.DeepCopy<Obj5>(null));
                }
            }).Milliseconds.ToString());

            // replace some properties in copy by new values from instance
            instance = new Obj5 {
                Prop3 = "new1",
                Prop5 = new List<string> {
                    "new2",
                    "new3"
                },
                Prop8 = 45
            };

            instance.DeepCopy(copy);
            Assert.AreEqual("nice1", copy.Prop2[0].Prop1);
            Assert.AreEqual(null, copy.Prop2[0].Prop2);
            Assert.AreEqual(null, copy.Prop2[1]);
            Assert.AreEqual(null, copy.Prop2[2].Prop1);
            Assert.AreEqual("nice2", copy.Prop2[2].Prop2);
            Assert.AreEqual("new1", copy.Prop3);
            Assert.AreEqual("new2", copy.Prop5[0]);
            Assert.AreEqual("new3", copy.Prop5[1]);
            Assert.AreEqual("nice6", copy.Prop6[0]);
            Assert.AreEqual(null, copy.Prop6[1]);
            Assert.AreEqual("nice7", copy.Prop6[2]);
            Assert.AreEqual("nice8", copy.Prop7.Prop1);
            Assert.AreEqual("nice9", copy.Prop7.Prop2);
            Assert.AreEqual(45, copy.Prop8);
            Assert.AreEqual(null, copy.Prop9);
        }

        [TestMethod]
        public void Overload_instances() {
            var instanceGlobalDefault = new Obj5 {
                Prop2 = new List<Obj3> {
                    new Obj3 {
                        Prop1 = "cool1"
                    },
                    new Obj3 {
                        Prop2 = "cool2"
                    }
                },
                Prop4 = 10,
                Prop5 = new List<string> {
                    "cool4",
                    "cool5"
                },
                Prop6 = new[] {
                    "cool6", null, "cool7"
                },
                Prop7 = new Obj3 {
                    Prop2 = "cool9"
                },
                Prop8 = 45
            };
            var instanceOverload = new Obj5 {
                Prop2 = new List<Obj3> {
                    new Obj3 {
                        Prop2 = "cool3"
                    },
                    new Obj3 {
                        Prop1 = "cool4"
                    }
                },
                Prop3 = "cool2",
                Prop4 = 25,
                Prop7 = new Obj3 {
                    Prop1 = "cool1"
                }
            };

            var copy = instanceGlobalDefault.GetDeepCopy();
            instanceOverload.DeepCopy(copy);
            
            Assert.AreEqual("cool3", copy.Prop2[0].Prop2);
            Assert.AreEqual("cool4", copy.Prop2[1].Prop1);
            Assert.AreEqual("cool2", copy.Prop3);
            Assert.AreEqual(25, copy.Prop4);
            Assert.AreEqual("cool1", copy.Prop7.Prop1);
            
            Assert.AreEqual("cool4", copy.Prop5[0]);
            Assert.AreEqual("cool5", copy.Prop5[1]);
            Assert.AreEqual("cool6", copy.Prop6[0]);
            Assert.AreEqual(null, copy.Prop6[1]);
            Assert.AreEqual("cool7", copy.Prop6[2]);
            Assert.AreEqual("cool9", copy.Prop7.Prop2);
            Assert.AreEqual(0, copy.Prop8);
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
                    Prop3 = new[] {
                        "cool9", null, "cool10"
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
                    Prop3 = new[] {
                        "cool9", null, "cool10"
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
            public static string GetDefaultProp1() => "nice";

            public IEnumerable<Obj3> Prop2 { get; set; }

            public string[] Prop3 { get; set; }
        }

        private class Obj3 {
            public string Prop1 { get; set; }

            public string Prop2 { get; set; }
            public static string GetDefaultProp2() => "cool";
        }

        private class Obj4 {
            [ReplaceStringProperty(SkipReplace = true)]
            public Obj2 Prop1 { get; set; }

            public List<Obj3> Prop2 { get; set; }

            [ReplaceStringProperty(SkipReplace = true)]
            public string Prop3 { get; set; }

            public int Prop4 { get; set; }

            public List<string> Prop5 { get; set; }
        }

        private class Obj5 {
            public List<Obj3> Prop2 { get; set; }

            public string Prop3 { get; set; }

            public int Prop4 { get; set; }

            public List<string> Prop5 { get; set; }

            public string[] Prop6 { get; set; }

            public Obj3 Prop7 { get; set; }

            public byte Prop8 { get; set; }

            public string[] Prop9 { get; set; }
        }

        private class Obj6 {
            public virtual string Prop1 { get; set; }
            public string Prop2 { get; set; }
        }

        private class Obj7 : Obj6 {
            [DeepCopy(Ignore = true)]
            public override string Prop1 { get; set; }
        }
        
        private class Obj8 {
            public Obj2 Prop1 { get; set; }
            public static Obj2 GetDefaultProp1() => new Obj2();

            public List<Obj3> Prop2 { get; set; }
            public static List<Obj3> GetDefaultProp2() => new List<Obj3> { new Obj3() };

            public string Prop3 { get; set; }

            public int? Prop4 { get; set; }
            public static int GetDefaultProp4() => 10;

            public List<string> Prop5 { get; set; }
            
            public Obj2 Prop6 { get; set; }
            public static Obj2 GetDefaultProp6() => new Obj2();
        }
    }
}