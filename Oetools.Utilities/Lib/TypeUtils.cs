#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (TypeUtils.cs) is part of Oetools.Utilities.
// 
// Oetools.Utilities is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Oetools.Utilities.Lib.Attributes;

namespace Oetools.Utilities.Lib {
    
    public static partial class Utils {
        
        /// <summary>
        /// Browse every public properties of an object searching for string properties (can also dive into classes and Ienumerable of classes)
        /// allows to replace the current string value by another one
        /// </summary>
        /// <param name="instanceType"></param>
        /// <param name="instance"></param>
        /// <param name="stringReplacementFunction"></param>
        /// <exception cref="ArgumentException"></exception>
        public static void ForEachPublicPropertyStringInObject(Type instanceType, object instance, Func<PropertyInfo, string, string> stringReplacementFunction) {
            var properties = instanceType.GetProperties();
            foreach (var property in properties) {
                if (!property.CanRead || !property.CanWrite || property.PropertyType.IsNotPublic) {
                    continue;
                }
                if (Attribute.GetCustomAttribute(property, typeof(ReplaceStringProperty), true) is ReplaceStringProperty attribute && attribute.SkipReplace) {
                    continue;
                }
                
                var obj = property.GetValue(instance);
                switch (obj) {
                    case string strObj:
                        property.SetValue(instance, stringReplacementFunction(property, strObj));
                        break;
                    case IEnumerable listItem:
                        if (listItem is IList<string> ilistOfStrings) {
                            for (int i = 0; i < ilistOfStrings.Count; i++) {
                                ilistOfStrings[i] = stringReplacementFunction(property, ilistOfStrings[i]);
                            }
                        } else if (property.PropertyType.UnderlyingSystemType.GenericTypeArguments.Length > 0) {
                            foreach (var item in listItem) {
                                if (item != null) {
                                    ForEachPublicPropertyStringInObject(item.GetType(), item, stringReplacementFunction);
                                }
                            }
                        }
                        break;
                    default:
                        if (property.PropertyType.IsClass && obj != null) {
                            ForEachPublicPropertyStringInObject(property.PropertyType, obj, stringReplacementFunction);
                        }
                        break;
                }
            }
        }
        
        private const string GetDefaultMethodPrefix = "GetDefault";

        /// <summary>
        /// Allows to set default values to certain public properties of an object, a static method retuning the type of the property
        /// and named GetDefaultXXX (<see cref="GetDefaultMethodPrefix"/>, where XXX is the property name) must be defined;
        /// does not replace non null values
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static void SetDefaultValues(object obj) {
            if (obj == null) {
                return;
            }
            var objType = obj.GetType();
            foreach (var method in objType.GetMethods().Where(m => m.IsStatic && m.Name.StartsWith(GetDefaultMethodPrefix, StringComparison.CurrentCulture))) {
                var prop = objType.GetProperty(method.Name.Substring(GetDefaultMethodPrefix.Length));
                if (prop != null) {
                    var propValue = prop.GetValue(obj);
                    if (propValue == null) {
                        propValue = method.Invoke(null, null);
                        prop.SetValue(obj, propValue); // invoke static method
                    }
                    switch (propValue) {
                        case string strObj:
                            SetDefaultValues(strObj);
                            break;
                        case IEnumerable listItem:
                            if (prop.PropertyType.UnderlyingSystemType.GenericTypeArguments.Length > 0) {
                                foreach (var item in listItem) {
                                    if (item != null) {
                                        SetDefaultValues(item);
                                    }
                                }
                            }
                            break;
                        default:
                            if (prop.PropertyType.IsClass && propValue != null) {
                                SetDefaultValues(propValue);
                            }
                            break;
                    }
                }
            }
        }
    }
}