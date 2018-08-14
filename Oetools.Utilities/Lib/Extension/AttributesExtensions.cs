#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (AttributesExtension.cs) is part of Oetools.Utilities.Test.
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
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Oetools.Utilities.Lib.Extension {
    
    public static class AttributesExtensions {
        
        /// <summary>
        /// Use : var name = player.GetAttributeFrom DisplayAttribute>("PlayerDescription").Name;
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static T GetAttributeFrom<T>(this object instance, string propertyName) where T : Attribute {
            var attrType = typeof(T);
            var fieldInfo = instance.GetType().GetField(propertyName);
            if (fieldInfo == null) {
                var propertyInfo = instance.GetType().GetProperty(propertyName);
                if (propertyInfo == null) {
                    return (T) Convert.ChangeType(null, typeof(T));
                }
                return (T) propertyInfo.GetCustomAttributes(attrType, false).FirstOrDefault();
            }
            return (T) fieldInfo.GetCustomAttributes(attrType, false).FirstOrDefault();
        }
        
        /// <summary>
        /// Returns the attribute array for the given Type T and the given value,
        /// not to self : don't use that on critical path -> reflection is costly
        /// </summary>
        public static T[] GetAttributes<T>(this Enum value) where T : Attribute {
            Type type = value.GetType();
            string name = Enum.GetName(type, value);
            if (name != null) {
                FieldInfo field = type.GetField(name);
                if (field != null) {
                    var attributeArray = (T[]) Attribute.GetCustomAttributes(field, typeof(T), true);
                    return attributeArray;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the attribute for the given Type T and the given value,
        /// not to self : dont use that on critical path -> reflection is costly
        /// </summary>
        public static T GetAttribute<T>(this Enum value) where T : Attribute {
            Type type = value.GetType();
            string name = Enum.GetName(type, value);
            if (name != null) {
                FieldInfo field = type.GetField(name);
                if (field != null) {
                    var attribute = Attribute.GetCustomAttribute(field, typeof(T), true) as T;
                    return attribute;
                }
            }
            return null;
        }

        /// <summary>
        /// Decorate enum values with [Description("Description for Foo")] and get their description with x.Foo.GetDescription()
        /// </summary>
        public static string GetDescription(this Enum value) {
            var attr = value.GetAttribute<DescriptionAttribute>();
            return attr != null ? attr.Description : null;
        }
    }
}