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

        /// <summary>
        /// Returns true if the constructor has a parameter-less constructor
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool HasDefaultConstructor(this Type t) {
            return t.IsValueType || t.GetConstructor(Type.EmptyTypes) != null;
        }
        
        /// <summary>
        /// Returns a new object that have the same public property values
        /// </summary>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetDeepCopy<T>(this object obj) where T : class {
            return obj.DeepCopy<T>(null);
        }

        /// <summary>
        /// Returns a new object that have the same public property values
        /// </summary>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetDeepCopy<T>(this T obj) where T : class {
            return obj.DeepCopy<T>(null);
        }
        
        /// <summary>
        /// Copies all the public properties of one object to another
        /// </summary>
        /// <remarks>
        /// If a property is null in the source object, it won't affect the target object (i.e. either null if we created the
        /// target object or the old target value if it had any)
        /// </remarks>
        /// <param name="sourceObj"></param>
        /// <param name="targetObj"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static T DeepCopy<T>(this object sourceObj, T targetObj) where T : class {
            var targetType = typeof(T);
            if (targetType.IsInterface) {
                throw new Exception($"Can't deep copy an interface, need a implementation of type {targetType}.");
            }
            if (targetObj == null && !HasDefaultConstructor(targetType)) {
                throw new Exception($"Can't deep copy to a new instance without a default constructor for type {targetType}.");
            }
            return (T) DeepCopyPublicProperties(sourceObj, targetType, targetObj);
        }
        
        /// <summary>
        /// Copies all the public properties of one object to another
        /// </summary>
        /// <remarks>
        /// If a property is null in the source object, it won't affect the target object (i.e. either null if we created the
        /// target object or the old target value if it had any)
        /// </remarks>
        /// <param name="sourceObj"></param>
        /// <param name="targetType"></param>
        /// <param name="targetObj"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static object DeepCopyPublicProperties(object sourceObj, Type targetType, object targetObj = null) {
            if (sourceObj == null) {
                return null;
            }
            if (!targetType.IsClass) {
                return sourceObj;
            }
            if (targetType == typeof(string)) {
                return sourceObj;
            }
            if (targetObj == null) {
                targetObj = Activator.CreateInstance(targetType);
            }

            var sourceProperties = sourceObj.GetType().GetProperties();
            var targetProperties = targetType.GetProperties();
            foreach (var sourceProperty in sourceProperties) {
                if (!sourceProperty.CanRead || sourceProperty.PropertyType.IsNotPublic) {
                    continue;
                }
                var targetProperty = targetProperties.FirstOrDefault(x => x.Name == sourceProperty.Name);
                if (targetProperty == null || !targetProperty.CanWrite || targetProperty.PropertyType.IsNotPublic) {
                    continue;
                }
                if (Attribute.GetCustomAttribute(targetProperty, typeof(DeepCopy), true) is DeepCopy attribute && attribute.Ignore) {
                    continue;
                }

                if (sourceProperty.PropertyType != targetProperty.PropertyType) {
                    continue;
                }
                
                var obj = sourceProperty.GetValue(sourceObj);
                if (obj == null) {
                    continue;
                }

                switch (obj) {
                    case string _:
                        targetProperty.SetValue(targetObj, obj);
                        break;
                    case IList listItem:
                        if (sourceProperty.PropertyType.IsArray) {
                            var subtype = sourceProperty.PropertyType.GetElementType();
                            if (subtype == null) {
                                throw new Exception($"Unknown element type of array {sourceProperty.Name}.");
                            }
                            var array = Array.CreateInstance(subtype, listItem.Count);
                            targetProperty.SetValue(targetObj, array);
                            for (int i = 0; i < listItem.Count; i++) {
                                array.SetValue(listItem[i] != null ? DeepCopyPublicProperties(listItem[i], listItem[i].GetType()) : null, i);
                            }
                        } else if (sourceProperty.PropertyType.UnderlyingSystemType.GenericTypeArguments.Length > 0) {
                            var list = (IList) Activator.CreateInstance(targetProperty.PropertyType);
                            targetProperty.SetValue(targetObj, list);
                            foreach (var item in listItem) {
                                list.Add(item != null ? DeepCopyPublicProperties(item, item.GetType()) : null);
                            }
                        }
                        break;
                    default:
                        if (sourceProperty.PropertyType.IsClass) {
                            targetProperty.SetValue(targetObj, DeepCopyPublicProperties(obj, targetProperty.PropertyType));
                        } else {
                            targetProperty.SetValue(targetObj, obj);
                        }
                        break;
                }
            }
            return targetObj;
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