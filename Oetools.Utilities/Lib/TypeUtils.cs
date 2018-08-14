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
using System.Xml.Serialization;

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
                if (Attribute.GetCustomAttribute(property, typeof(ReplacePlaceHolder), true) is ReplacePlaceHolder attribute && attribute.SkipReplace) {
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

        // TODO : need to work on this, this is a copy/paste draft
        private static object DeepCopy(object obj, Type targetType) {
            if (obj != null) {
                object objCopy = Activator.CreateInstance(targetType);
                var props = obj.GetType().GetProperties();
                foreach (var propertyInfo in props) {
                    var targetProperty = targetType.GetProperties().Where(x => x.Name == propertyInfo.Name).First();
                    if (targetProperty.GetCustomAttributes(typeof(XmlIgnoreAttribute), false).Length > 0) {
                        continue;
                    }
                    if (propertyInfo.PropertyType.IsClass) {
                        if (propertyInfo.PropertyType.GetInterface("IList", true) != null) {
                            var list = (IList) Activator.CreateInstance(targetProperty.PropertyType);
                            targetProperty.SetValue(objCopy, list);
                            var sourceList = propertyInfo.GetValue(obj) as IList;
                            foreach (var o in sourceList) {
                                    list.Add(DeepCopy(o, targetProperty.PropertyType.UnderlyingSystemType.GenericTypeArguments[0]));
                            }
                        } else if (propertyInfo.PropertyType == typeof(string)) {
                            targetProperty.SetValue(objCopy, propertyInfo.GetValue(obj));
                        } else {
                            targetProperty.SetValue(objCopy, DeepCopy(propertyInfo.GetValue(obj), targetProperty.PropertyType));
                        }
                    } else {
                        targetProperty.SetValue(objCopy, propertyInfo.GetValue(obj));
                    }
                }
                return objCopy;
            }
            return null;
        }
    }
}