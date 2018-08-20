#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (TypeExtensions.cs) is part of Oetools.Utilities.
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
using System.Reflection;

namespace Oetools.Utilities.Lib.Extension {
    
    public static class TypeExtension {
        
        /// <summary>
        /// Set a value to this instance, by its property name
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool SetValueOf(this object instance, string propertyName, object value) {
            var fieldInfo = instance.GetType().GetField(propertyName);
            if (fieldInfo == null) {
                var propertyInfo = instance.GetType().GetProperty(propertyName);
                if (propertyInfo == null) {
                    return false;
                }
                propertyInfo.SetValue(instance, value, null);
                return true;
            }
            fieldInfo.SetValue(instance, value);

            return true;
        }

        /// <summary>
        /// Get a value from this instance, by its property name
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static object GetValueOf(this object instance, string propertyName) {
            var fieldInfo = instance.GetType().GetField(propertyName);
            if (fieldInfo == null) {
                var propertyInfo = instance.GetType().GetProperty(propertyName);
                if (propertyInfo == null) {
                    return null;
                }
                return propertyInfo.GetValue(instance, null);
            }
            return fieldInfo.GetValue(instance);
        }

        /// <summary>
        /// Returns true of the given object has the given method
        /// </summary>
        /// <param name="objectToCheck"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        public static bool HasMethod(this object objectToCheck, string methodName) {
            try {
                var type = objectToCheck.GetType();
                return type.GetMethod(methodName) != null;
            } catch (AmbiguousMatchException) {
                // ambiguous means there is more than one result,
                // which means: a method with that name does exist
                return true;
            }
        }

        /// <summary>
        /// Invoke the given method with the given parameters on the given object and returns its value
        /// Returns null if it fails
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="methodName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object InvokeMethod(this object obj, string methodName, object[] parameters) {
            try {
                //Get the method information using the method info class
                MethodInfo mi = obj.GetType().GetMethod(methodName);
                return mi != null ? mi.Invoke(obj, parameters) : null;
            } catch (Exception) {
                return null;
            }
        }
    }
}